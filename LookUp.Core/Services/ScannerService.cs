using LookUp.Core.DataBase;
using LookUp.Core.Models;
using LookUp.Core.Rpc;
using LookUp.Core.Rpc.Models;
using NBitcoin;
using System.Text;

namespace LookUp.Core.Services
{
    public class ScannerService : BackgroundService
    {
        private readonly int batchSize = 20;

        public ScannerService(IRPCClient rpcClient, LastScannedBlockHeight lastScanned, ScanChannel scanChannel)
        {
            RpcClient = rpcClient;
            LastScannedBlockHeight = lastScanned;
            ScanChannel = scanChannel;
           
        }

        public IRPCClient RpcClient { get; }
        public LastScannedBlockHeight LastScannedBlockHeight { get; }

        public ScanChannel ScanChannel { get; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var previousHashes = new List<uint256>();

            while (!stoppingToken.IsCancellationRequested)
            {
                var blockchainInfo = await RpcClient.GetBlockchainInfoAsync(stoppingToken);
                var tipHeight = blockchainInfo.Blocks;

                if ((int)tipHeight > LastScannedBlockHeight.BlockHeight)
                {
                    var currentAllHashes = await FetchBlockHashesAsnyc((int)tipHeight, stoppingToken);
                    var missingHashes = currentAllHashes.Except(previousHashes);

                    if (!missingHashes.Any())
                    {
                        continue;
                    }

                    var batches = missingHashes.Chunk(batchSize);

                    foreach (var batch in batches)
                    {
                        await ProcessBatchOfHashes(batch, stoppingToken);
                    }

                    previousHashes = currentAllHashes;
                    LastScannedBlockHeight.SaveLastScannedBlockHeight();
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
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

        private async Task ProcessBatchOfHashes(uint256[] batch, CancellationToken cancellationToken)
        {
            var blocks = await FetchBlocksAsync(batch.ToList(), cancellationToken);

            foreach (var block in blocks)
            {
                await ProcessBlockAsync(block);
            }

            LastScannedBlockHeight.IncreaseLastScannedBlockHeight(blocks.Count);
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

            // Check if the bytes fall between the first printable ASCII char and the last printable char.
            bool isLikelyText = bytes.All(b => b >= 0x20 && b <= 0x7E);

            if (!isLikelyText)
            {
                return;
            }

            // Decode to string
            string message = Encoding.UTF8.GetString(bytes);

            // TODO: Save to DB if there is message
            Console.WriteLine($"Processed TX: ID: {tx.Id}");
            ScanChannel.ChannelToDB.Writer.TryWrite(new MessageModel(tx.Id, message, hex, tx.BlockInfo.BlockHash, tx.BlockInfo.BlockIndex, tx.BlockInfo.BlockTime));
        }
    }
}
