using LookUp.Core.Rpc;
using LookUp.Core.Rpc.Models;
using LookUp.Models;
using LookUp.Scanner.DataBase;
using LookUp.Scanner.LastScannedBlockHeight;
using NBitcoin;
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
                var blockchainInfo = await RpcClient.GetBlockchainInfoAsync(stoppingToken);
                var tipHeight = (int)blockchainInfo.Blocks;

                if (tipHeight > LastScannedBlockHeight.BlockHeight)
                {
                    var blockHashes = await FetchBlockHashesAsnyc(tipHeight, stoppingToken);
                    var unprocessedBlockHashes = blockHashes.GetRange(LastScannedBlockHeight.BlockHeight, blockHashes.Count - LastScannedBlockHeight.BlockHeight);

                    if (unprocessedBlockHashes.Count == 0)
                    {
                        Logger.Logger.LogCritical("Scanner is behind the tipHeight, but couldn't receive the missing block hashes.");
                        throw new Exception("Scanner is behind the tipHeight, but couldn't receive the missing block hashes");
                    }

                    var batches = unprocessedBlockHashes.Chunk(batchSize);

                    foreach (var batch in batches)
                    {
                        await ProcessBatchOfBlockHashes(batch, stoppingToken);
                    }
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

        private async Task ProcessBatchOfBlockHashes(uint256[] batch, CancellationToken cancellationToken)
        {
            var blocks = await FetchBlocksAsync(batch.ToList(), cancellationToken);

            foreach (var block in blocks)
            {
                await ProcessBlockAsync(block);
                LastScannedBlockHeight.IncreaseLastScannedBlockHeight((int)block.Height + 1); // +1 so we match the tipHeight. BlockHeight starts from zero, but tipHeight from 1.
            }

            LastScannedBlockHeight.IncreaseLastScannedBlockHeight(blocks.Count);
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
            
            /* TEST
            if (tx.Id == new uint256("b7550363b240637316bb300a425d2299596b50eee1b06c061781ea8eb7fc0724") ||
                tx.Id == new uint256("0a541c952ca9979cc3e45c721d602eb530919f6f12173e5befeb2a1d7d877f91") ||
                tx.Id == new uint256("8e9b50e70da2b097661710bb4baa438444b464809e2e37e260af0f5d824c6d99"))
            {
                ScanChannel.MessageChannel.Writer.TryWrite(new MessageModel(tx.Id.ToString(), "Test Message", hex, tx.BlockInfo.BlockHash.ToString(), tx.BlockInfo.BlockTime));
            } */

            if (!isLikelyText)
            {
                return;
            }

            // Decode to string
            string message = Encoding.UTF8.GetString(bytes);
            ScanChannel.MessageChannel.Writer.TryWrite(new MessageModel(new Guid(), tx.Id.ToString(), message, hex, tx.BlockInfo.BlockHash.ToString(), tx.BlockInfo.BlockTime));
        }
    }
}
