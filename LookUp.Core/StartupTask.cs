using LookUp.Core.Rpc;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace LookUp.Core
{
    public class StartupTask
    {
        public StartupTask(IRPCClient client)
        {
            RpcClient = client;
        }
        private IRPCClient RpcClient { get; }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // Make sure we are connected to Knots
            await CheckRpcConnectionAsync(cancellationToken);
        }

        private async Task CheckRpcConnectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                var blockchainInfo = await RpcClient.GetBlockchainInfoAsync(cancellationToken);

                var blocks = blockchainInfo.Blocks;
                if (blocks == 0 && RpcClient.Network != Network.RegTest)
                {
                    throw new NotSupportedException($"{nameof(blocks)} == 0");
                }

                var headers = blockchainInfo.Headers;
                if (headers == 0 && RpcClient.Network != Network.RegTest)
                {
                    throw new NotSupportedException($"{nameof(headers)} == 0");
                }

                if (blocks != headers)
                {
                    throw new NotSupportedException("Bitcoin Node is not fully synchronized.");
                }
            }
            catch (Exception ex) 
            {
                Console.WriteLine(ex.ToString());
                throw;
            }
        }
    }
}
