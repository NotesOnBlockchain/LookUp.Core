using LookUp.Core.Rpc.Models;
using NBitcoin;
using NBitcoin.RPC;

namespace LookUp.Core.Rpc
{
    public interface IRPCClient
    {
        Network Network { get; }
        RPCCredentialString CredentialString { get; }

        Task<uint256> GetBestBlockHashAsync(CancellationToken cancellationToken = default);

        Task<uint256> GetBlockHashAsync(int height, CancellationToken cancellationToken = default);

        Task StopAsync(CancellationToken cancellationToken = default);

        Task<BlockchainInfo> GetBlockchainInfoAsync(CancellationToken cancellationToken = default);

        IRPCClient PrepareBatch();

        Task SendBatchAsync(CancellationToken cancellationToken = default);

        Task<VerboseBlockInfo> GetVerboseBlockAsync(uint256 blockId, CancellationToken cancellationToken = default);

        Task<VerboseBlockInfo> GetBlockByHeightAsync(int height, CancellationToken cancellationToken = default);
    }
}
