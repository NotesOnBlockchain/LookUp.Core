using LookUp.Core.Rpc;
using LookUp.Core.Rpc.Models;
using NBitcoin;
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
            // Process past blocks
            var blockchainInfo = await RpcClient.GetBlockchainInfoAsync(stoppingToken);
            ulong tipHeight = blockchainInfo.Blocks;

            var blockHashes = await FetchBlockHashesAsnyc((int)tipHeight, stoppingToken);

            var batches = blockHashes.Chunk(batchSize);

            foreach (var batch in batches) 
            {
                await ProcessBatchOfHashes(batch);
            }
            
            // Wait for new blocks.
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

                blockchainInfo = await RpcClient.GetBlockchainInfoAsync(stoppingToken);
                tipHeight = blockchainInfo.Blocks;

                if ((int)tipHeight > LastScannedBlockHeight)
                {
                    var currentAllHashes = await FetchBlockHashesAsnyc((int)tipHeight, stoppingToken);
                    var missingHashes = currentAllHashes.Except(blockHashes);

                    batches = missingHashes.Chunk(batchSize);

                    foreach (var batch in batches)
                    {
                        await ProcessBatchOfHashes(batch);
                    }
                }
            }
        }

        private async Task<List<uint256>> FetchBlockHashesAsnyc(int blockHeight, CancellationToken cancellationToken)
        {
            var batchClient = RpcClient.PrepareBatch();

            var tasks = Enumerable.Range(0, blockHeight).Select(x => RpcClient.GetBlockHashAsync(x, cancellationToken));

            await batchClient.SendBatchAsync(cancellationToken);

            var results = await Task.WhenAll(tasks);

            return results.ToList();
        }

        private async Task<List<VerboseBlockInfo>> FetchBlocksAsync(List<uint256> blockHashes, CancellationToken cancellationToken)
        {
            var batchClient = RpcClient.PrepareBatch();

            var tasks = blockHashes.Select(x => RpcClient.GetVerboseBlockAsync(x, cancellationToken));

            await batchClient.SendBatchAsync(cancellationToken);

            var results = await Task.WhenAll(tasks);

            return results.ToList();
        }

        private async Task ProcessBatchOfHashes(uint256[] batch)
        {
            var blocks = await FetchBlocksAsync(batch.ToList(), stoppingToken);

            foreach (var block in blocks)
            {
                await ProcessBlockAsync(block);
            }

            IncreaseLastScannedBlockHeight(blocks.Count);
            Console.WriteLine($"Scan end: LastScannedBlockHeight: {LastScannedBlockHeight}");
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
            Console.WriteLine($"Processed TX: ID: {tx.Id}");
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
