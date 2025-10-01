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

            var blocks = await FetchBlocksAsync(start, end, stoppingToken).ConfigureAwait(false);

        }

        private async Task<List<VerboseBlockInfo>> FetchBlocksAsync(int startingHeight, int endHeight, CancellationToken stoppingToken)
        {
            var batch = RpcClient.PrepareBatch();

            var tasks = Enumerable.Range(startingHeight, endHeight).Select(x => RpcClient.GetVerboseBlockAsync(x, stoppingToken));

            await batch.SendBatchAsync().ConfigureAwait(false);

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            return [.. results.Where(x => x != null)];
        }
    }
}
