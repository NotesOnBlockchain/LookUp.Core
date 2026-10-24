using LookUp.Core.Rpc;
using LookUp.Core.Rpc.Models;
using LookUp.Helpers;
using LookUp.Models;
using LookUp.Scanner.DataBase;
using NBitcoin;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace LookUp.Scanner.Services
{
    public class ScannerService : BackgroundService
    {
        private readonly int batchSize = 20;

        public ScannerService(IRPCClient rpcClient, IServiceScopeFactory scopeFactory)
        {
            RpcClient = rpcClient;
            ScopeFactory = scopeFactory;
        }

        public IRPCClient RpcClient { get; }
        public IServiceScopeFactory ScopeFactory { get; }

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

            if (tipHeight > nextBlockHeight)
            {
                for (int height = nextBlockHeight; height < tipHeight + 1; height++)
                {
                    VerboseBlockInfo block = await RpcClient.GetBlockByHeightAsync(height, stoppingToken);

                    await ProcessBlockAsync(block, stoppingToken);
                }
            }
        }

        private async Task ProcessBlockAsync(VerboseBlockInfo block, CancellationToken stoppingToken)
        {
            var messages = new List<MessageModel>();

            foreach (var tx in block.Transactions) 
            {
                if (TryExtractMessage(tx, out MessageModel? message))
                {
                    messages.Add(message);
                }
            }

            // A fresh scope per block keeps the DbContext from tracking every message of a long scan.
            using var scope = ScopeFactory.CreateScope();
            var messageRepo = scope.ServiceProvider.GetRequiredService<MessageRepository>();

            // +1 so we match the tipHeight. BlockHeight starts from zero, but tipHeight from 1.
            await messageRepo.SaveBlockAsync(messages, (int)block.Height + 1, stoppingToken);

            Logger.Logger.LogInfo($"Successfully scanned Block Height {block.Height}. Transaction Count: {block.Transactions.Count()}");
        }

        private static bool TryExtractMessage(VerboseTransactionInfo tx, [NotNullWhen(true)] out MessageModel? messageModel)
        {
            messageModel = null;

            var opReturnOutput = tx.Outputs.FirstOrDefault(o => o.ScriptPubKey.ExtractScriptCode(-1).ToString().Contains("OP_RETURN"));

            if (opReturnOutput is null)
            {
                return false;
            }

            var script = opReturnOutput.ScriptPubKey.ExtractScriptCode(-1);
            var parts = script.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2) 
            {
                return false;
            }

            string hex = parts[1];

            if (HexChecker.FilterOutMessages(hex, out string? message) && message is not null)
            {
                messageModel = new MessageModel(new Guid(), tx.Id.ToString(), message, hex, tx.BlockInfo.BlockHash.ToString(), tx.BlockInfo.BlockTime);
                return true;
            }

            return false;
        }
    }
}
