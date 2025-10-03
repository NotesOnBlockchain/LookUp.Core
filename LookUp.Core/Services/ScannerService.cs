using LookUp.Core.Rpc;
using LookUp.Core.Rpc.Models;
using NBitcoin;

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

            int start = LastScannedBlockHeight;
            int end = Math.Min(LastScannedBlockHeight + batchSize - 1, (int)tipHeight);

            var blockHashes = await FetchBlockHashesAsnyc(start, end, stoppingToken);

            var blocks = await FetchBlocksAsync(blockHashes, stoppingToken).ConfigureAwait(false);

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

            return [.. results.Where(x => x != null)];
        }
    }
}
