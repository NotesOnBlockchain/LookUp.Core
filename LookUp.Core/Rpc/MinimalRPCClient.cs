using LookUp.Core.Rpc.Models;
using NBitcoin;
using NBitcoin.RPC;

namespace LookUp.Core.Rpc
{
    public class MinimalRPCClient : IRPCClient
    {
        public MinimalRPCClient(RPCClient rpc)
        {
            Rpc = rpc;
        }

        public Network Network => Rpc.Network;

        protected internal RPCClient Rpc { get; }

        public RPCCredentialString CredentialString => Rpc.CredentialString;

        public virtual async Task<uint256> GetBestBlockHashAsync(CancellationToken cancellationToken = default)
        {
            return await Rpc.GetBestBlockHashAsync(cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task<BlockchainInfo> GetBlockchainInfoAsync(CancellationToken cancellationToken = default)
        {
            return await Rpc.GetBlockchainInfoAsync(cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task StopAsync(CancellationToken cancellationToken = default)
        {
            await Rpc.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        public virtual IRPCClient PrepareBatch()
        {
            return new MinimalRPCClient(Rpc.PrepareBatch());
        }

        public async Task SendBatchAsync(CancellationToken cancellationToken = default)
        {
            await Rpc.SendBatchAsync(cancellationToken);
        }

        public virtual async Task<VerboseBlockInfo> GetVerboseBlockAsync(uint256 blockId, CancellationToken cancellationToken = default)
        {
            var resp = await Rpc.SendCommandAsync(RPCOperations.getblock, cancellationToken, blockId, 2).ConfigureAwait(false);
            return RpcParser.ParseVerboseBlockResponse(resp.ResultString);
        }

        public virtual async Task<uint256> GetBlockHashAsync(int height, CancellationToken cancellationToken = default)
        {
            return await Rpc.GetBlockHashAsync(height, cancellationToken).ConfigureAwait(false);
        }
    }
}
