using LookUp.Core.Rpc;
using LookUp.Core.Rpc.Models;
using NBitcoin;
using Newtonsoft.Json.Bson;
using System.Text;

namespace LookUp.Core.Services
{
    public class ScannerService : BackgroundService
    {
        private int batchSize = 20;
        public ScannerService(IRPCClient rpcClient)
        {
            RpcClient = rpcClient;
        }

        public IRPCClient RpcClient { get; }

        private int LastScannedBlockHeight { get; set; } = 0;
        private object LastScannedBlockHeightLock { get; set; } = new object();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var blockchainInfo = await RpcClient.GetBlockchainInfoAsync(stoppingToken);
            ulong tipHeight = blockchainInfo.Blocks;

            while ((int)tipHeight > LastScannedBlockHeight) 
            {
                blockchainInfo = await RpcClient.GetBlockchainInfoAsync(stoppingToken).ConfigureAwait(false);
                tipHeight = blockchainInfo.Blocks;

                int start = LastScannedBlockHeight;
                int end = Math.Min(LastScannedBlockHeight + batchSize - 1, (int)tipHeight);

                var blockHashes = await FetchBlockHashesAsnyc(start, end, stoppingToken);

                var blocks = await FetchBlocksAsync(blockHashes, stoppingToken).ConfigureAwait(false);

                foreach (var block in blocks)
                {
                    await ProcessBlockAsync(block).ConfigureAwait(false);
                }

                IncreaseLastScannedBlockHeight(blocks.Count);
            }
        }

        private async Task<List<uint256>> FetchBlockHashesAsnyc(int startingHeight, int endHeight, CancellationToken cancellationToken)
        {
            var batchClient = RpcClient.PrepareBatch();

            var tasks = Enumerable.Range(startingHeight, endHeight).Select(x => RpcClient.GetBlockHashAsync(x, cancellationToken));

            await batchClient.SendBatchAsync().ConfigureAwait(false);

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            return [.. results];
        }

        private async Task<List<VerboseBlockInfo>> FetchBlocksAsync(List<uint256> blockHashes, CancellationToken cancellationToken)
        {
            var batchClient = RpcClient.PrepareBatch();

            var tasks = blockHashes.Select(x => RpcClient.GetVerboseBlockAsync(x, cancellationToken));

            await batchClient.SendBatchAsync().ConfigureAwait(false);

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            return [.. results];
        }

        private async Task ProcessBlockAsync(VerboseBlockInfo block)
        {
            var tasks = Parallel.ForEach(block.Transactions,
                new ParallelOptions { MaxDegreeOfParallelism = 40 },
                tx => ProcessTransaction(tx));
        }

        private void ProcessTransaction(VerboseTransactionInfo tx)
        {
            var opReturnOutput = tx.Outputs.FirstOrDefault(o => o.ScriptPubKey.ExtractScriptCode(-1).ToString().Contains("OP_RETURN"));

            if (opReturnOutput is null) 
            {
                return;
            }

            var script = opReturnOutput.ScriptPubKey.ExtractScriptCode(-1);
            var parts = script.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            string hex = parts[1];

            // Convert hex -> byte[]
            byte[] bytes = Enumerable.Range(0, hex.Length / 2)
                .Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16))
                .ToArray();

            // Decode to string
            string message = Encoding.UTF8.GetString(bytes);

            // TODO: Save to DB if there is message

            Console.WriteLine(message);
        }

        private void IncreaseLastScannedBlockHeight(int processedBlockCount)
        {
            lock(LastScannedBlockHeightLock)
            {
                LastScannedBlockHeight += processedBlockCount;
            }
        }
    }
}
