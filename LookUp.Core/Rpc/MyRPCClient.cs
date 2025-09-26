using LookUp.Core.Rpc.Models;
using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Linq;

namespace LookUp.Core.Rpc
{
    public class MyRPCClient : IRPCClient
    {
        public MyRPCClient(RPCClient rpc)
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

        public virtual async Task<Block> GetBlockAsync(uint256 blockHash, CancellationToken cancellationToken = default)
        {
            return await Rpc.GetBlockAsync(blockHash, cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task<Block> GetBlockAsync(uint blockHeight, CancellationToken cancellationToken = default)
        {
            return await Rpc.GetBlockAsync(blockHeight, cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash, CancellationToken cancellationToken = default)
        {
            return await Rpc.GetBlockHeaderAsync(blockHash, cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task<BlockchainInfo> GetBlockchainInfoAsync(CancellationToken cancellationToken = default)
        {
            return await Rpc.GetBlockchainInfoAsync(cancellationToken).ConfigureAwait(false);
        }


        public virtual async Task<MemPoolInfo> GetMempoolInfoAsync(CancellationToken cancel = default)
        {
            try
            {
                var response = await Rpc.SendCommandAsync(RPCOperations.getmempoolinfo, cancel, true)
                    .ConfigureAwait(false);

                static IEnumerable<FeeRateGroup> ExtractFeeRateGroups(JToken jt) =>
                    jt switch
                    {
                        JObject jo => jo.Properties()
                            .Where(p => p.Name != "total_fees")
                            .Select(
                                p => new FeeRateGroup
                                {
                                    Group = int.Parse(p.Name),
                                    Sizes = p.Value.Value<ulong>("sizes"),
                                    Count = p.Value.Value<uint>("count"),
                                    Fees = Money.Satoshis(p.Value.Value<ulong>("fees")),
                                    From = new FeeRate(p.Value.Value<decimal>("from_feerate")),
                                    To = new FeeRate(Math.Min(50_000, p.Value.Value<decimal>("to_feerate")))
                                }),
                        _ => Enumerable.Empty<FeeRateGroup>()
                    };

                return new MemPoolInfo()
                {
                    Size = int.Parse((string)response.Result["size"]!, CultureInfo.InvariantCulture),
                    Bytes = int.Parse((string)response.Result["bytes"]!, CultureInfo.InvariantCulture),
                    Usage = int.Parse((string)response.Result["usage"]!, CultureInfo.InvariantCulture),
                    MaxMemPool =
                        double.Parse((string)response.Result["maxmempool"]!, CultureInfo.InvariantCulture),
                    MemPoolMinFee = double.Parse(
                        (string)response.Result["mempoolminfee"]!,
                        CultureInfo.InvariantCulture),
                    MinRelayTxFee = double.Parse(
                        (string)response.Result["minrelaytxfee"]!,
                        CultureInfo.InvariantCulture),
                    Histogram = ExtractFeeRateGroups(response.Result["fee_histogram"]!).ToArray()
                };
            }
            catch (RPCException ex) when (ex.RPCCode == RPCErrorCode.RPC_MISC_ERROR)
            {
                cancel.ThrowIfCancellationRequested();

                return await Rpc.GetMemPoolAsync(cancel).ConfigureAwait(false);
            }
        }

        public virtual async Task<uint256[]> GetRawMempoolAsync(CancellationToken cancellationToken = default)
        {
            return await Rpc.GetRawMempoolAsync(cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task<GetTxOutResponse?> GetTxOutAsync(uint256 txid, int index, bool includeMempool = true, CancellationToken cancellationToken = default)
        {
            return await Rpc.GetTxOutAsync(txid, index, includeMempool, cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task StopAsync(CancellationToken cancellationToken = default)
        {
            await Rpc.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        public virtual IRPCClient PrepareBatch()
        {
            return new MyRPCClient(Rpc.PrepareBatch());
        }

        public virtual async Task<VerboseBlockInfo> GetVerboseBlockAsync(uint256 blockId, CancellationToken cancellationToken = default)
        {
            var resp = await Rpc.SendCommandAsync(RPCOperations.getblock, cancellationToken, blockId, 3).ConfigureAwait(false);
            return RpcParser.ParseVerboseBlockResponse(resp.ResultString);
        }
    }
}
