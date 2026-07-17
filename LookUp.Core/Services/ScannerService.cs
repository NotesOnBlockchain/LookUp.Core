using LookUp.Core.Rpc;
using LookUp.Core.Rpc.Models;
using LookUp.Helpers;
using LookUp.Models;
using LookUp.Scanner.DataBase;
using LookUp.Scanner.LastScannedBlockHeight;
using NBitcoin;
using System.Runtime.CompilerServices;
using System.Text;

namespace LookUp.Scanner.Services
{
    public class ScannerService : BackgroundService
    {
        private readonly int batchSize = 20;

        public ScannerService(IRPCClient rpcClient, LastScannedBlockHeightHolder lastScanned, ScanChannel scanChannel)
        {
            RpcClient = rpcClient;
            LastScannedBlockHeight = lastScanned;
            ScanChannel = scanChannel;
           
        }

        public IRPCClient RpcClient { get; }
        public LastScannedBlockHeightHolder LastScannedBlockHeight { get; }

        public ScanChannel ScanChannel { get; }

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

            if (tipHeight > LastScannedBlockHeight.BlockHeight)
            {
                for (int height = LastScannedBlockHeight.BlockHeight; height < tipHeight + 1; height++)
                {
                    VerboseBlockInfo block = await RpcClient.GetBlockByHeightAsync(height, stoppingToken);

                    await ProcessBlockAsync(block);
                }
            }
        }

        private async Task ProcessBlockAsync(VerboseBlockInfo block)
        {
            foreach (var tx in block.Transactions) 
            {
                await ProcessTransaction(tx);
            }

            LastScannedBlockHeight.IncreaseLastScannedBlockHeight((int)block.Height + 1); // +1 so we match the tipHeight. BlockHeight starts from zero, but tipHeight from 1.
            Logger.Logger.LogInfo($"Successfully scanned Block Height {block.Height}. Transaction Count: {block.Transactions.Count()}");
        }

        private async Task ProcessTransaction(VerboseTransactionInfo tx)
        {
            var opReturnOutput = tx.Outputs.FirstOrDefault(o => o.ScriptPubKey.ExtractScriptCode(-1).ToString().Contains("OP_RETURN"));

            if (opReturnOutput is null)
            {
                return;
            }

            var script = opReturnOutput.ScriptPubKey.ExtractScriptCode(-1);
            var parts = script.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2) 
            {
                return;
            }

            string hex = parts[1];

            if (HexChecker.FilterOutMessages(hex, out string? message) && message is not null)
            {
                await ScanChannel.MessageChannel.Writer.WriteAsync(new MessageModel(new Guid(), tx.Id.ToString(), message, hex, tx.BlockInfo.BlockHash.ToString(), tx.BlockInfo.BlockTime));
            }
        }
    }
}
