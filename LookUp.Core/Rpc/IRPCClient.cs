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

        Task<Block> GetBlockAsync(uint256 blockId, CancellationToken cancellationToken = default);

        Task<Block> GetBlockAsync(uint blockHeight, CancellationToken cancellationToken = default);

        Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash, CancellationToken cancellationToken = default);

        Task StopAsync(CancellationToken cancellationToken = default);

        Task<BlockchainInfo> GetBlockchainInfoAsync(CancellationToken cancellationToken = default);

        Task<uint256[]> GetRawMempoolAsync(CancellationToken cancellationToken = default);

        Task<MemPoolInfo> GetMempoolInfoAsync(CancellationToken cancel = default);

        Task<GetTxOutResponse?> GetTxOutAsync(uint256 txid, int index, bool includeMempool = true, CancellationToken cancellationToken = default);

        IRPCClient PrepareBatch();

        Task<VerboseBlockInfo> GetVerboseBlockAsync(uint256 blockId, CancellationToken cancellationToken = default);
    }
}
