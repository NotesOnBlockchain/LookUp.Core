using LookUp.Core.Rpc;
using LookUp.Core.Rpc.Models;
using LookUp.Helpers;
using LookUp.Models;
using LookUp.Scanner.DataBase;
using NBitcoin;
using System.Runtime.CompilerServices;
using System.Text;

namespace LookUp.Scanner.Services
{
    public class ScannerService : BackgroundService
    {
        private readonly int batchSize = 20;

        // Blocks at the tip can still be replaced by a reorg; only scan blocks with at least this many blocks on top.
        private const int RequiredConfirmations = 1;

        public ScannerService(IRPCClient rpcClient, IServiceScopeFactory scopeFactory, ScanStatus scanStatus)
        {
            RpcClient = rpcClient;
            ScopeFactory = scopeFactory;
            ScanStatus = scanStatus;
        }

        public IRPCClient RpcClient { get; }
        public IServiceScopeFactory ScopeFactory { get; }
        public ScanStatus ScanStatus { get; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Check for new blocks then wait.
                    await ScanAsync(stoppingToken);

                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Normal shutdown
                    return;
                }
                catch (Exception ex) 
                {
                    Logger.Logger.LogWarning($"Scanner Service Loop failed: {ex}. Retrying...");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
        }

        private async Task ScanAsync(CancellationToken stoppingToken)
        {
            var blockchainInfo = await RpcClient.GetBlockchainInfoAsync(stoppingToken);
            var tipHeight = (int)blockchainInfo.Blocks;

            int nextBlockHeight;
            using (var scope = ScopeFactory.CreateScope())
            {
                var messageRepo = scope.ServiceProvider.GetRequiredService<MessageRepository>();
                nextBlockHeight = await messageRepo.GetNextBlockHeightAsync(stoppingToken)
                    ?? throw new InvalidOperationException("Scan progress is missing from the database.");
            }

            // Being up to date counts as progress, so an idle scanner isn't reported as stalled.
            if (nextBlockHeight > tipHeight - RequiredConfirmations)
            {
                ScanStatus.ReportProgress(nextBlockHeight);
            }

            for (int height = nextBlockHeight; height <= tipHeight - RequiredConfirmations; height++)
            {
                VerboseBlockInfo block = await RpcClient.GetBlockByHeightAsync(height, stoppingToken);

                await ProcessBlockAsync(block, stoppingToken);

                ScanStatus.ReportProgress(height + 1);
            }
        }

        private async Task ProcessBlockAsync(VerboseBlockInfo block, CancellationToken stoppingToken)
        {
            var messages = new List<MessageModel>();

            foreach (var tx in block.Transactions) 
            {
                messages.AddRange(ExtractMessages(tx));
            }

            // A fresh scope per block keeps the DbContext from tracking every message of a long scan.
            using var scope = ScopeFactory.CreateScope();
            var messageRepo = scope.ServiceProvider.GetRequiredService<MessageRepository>();

            // Heights start at 0 (genesis), so the next block to scan is this one + 1.
            await messageRepo.SaveBlockAsync(messages, (int)block.Height + 1, stoppingToken);

            Logger.Logger.LogInfo($"Successfully scanned Block Height {block.Height}. Transaction Count: {block.Transactions.Count()}");
        }

        // A transaction can carry several OP_RETURN outputs; each one is checked on its own.
        private static IEnumerable<MessageModel> ExtractMessages(VerboseTransactionInfo tx)
        {
            foreach (var output in tx.Outputs)
            {
                var script = output.ScriptPubKey.ExtractScriptCode(-1).ToString();

                if (!script.Contains("OP_RETURN"))
                {
                    continue;
                }

                var parts = script.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 2) 
                {
                    continue;
                }

                string hex = parts[1];

                // Non-standard scripts can follow OP_RETURN with an opcode instead of data, e.g. "OP_RETURN OP_CHECKSIG".
                // That isn't a message, and decoding it as hex would throw and fail the block on every retry.
                if (!hex.All(char.IsAsciiHexDigit))
                {
                    Logger.Logger.LogWarning($"Skipping OP_RETURN output that doesn't start with pushed data. TxID: {tx.Id}");
                    continue;
                }

                if (HexChecker.FilterOutMessages(hex, out string? message) && message is not null)
                {
                    yield return new MessageModel(new Guid(), tx.Id.ToString(), message, hex, tx.BlockInfo.BlockHash.ToString(), tx.BlockInfo.BlockTime);
                }
            }
        }
    }
}
