using LookUp.Core.Rpc.Models;
using LookUp.Models;
using NBitcoin;
using NBitcoin.RPC;
using System.Runtime.CompilerServices;
using System.Text;

namespace LookUp.Tests.TestHelpers
{
    // Builds blocks, transactions and messages the way the Scanner sees them after RpcParser.
    public static class TestData
    {
        // Logs.txt, Config.json and the data protection keys of the tests. It lives in the build output and is never
        // deleted, because the static Logger keeps writing to it from tests running in parallel.
        public static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "TestData");

        [ModuleInitializer]
        internal static void InitializeLogger() => Logger.Logger.Initialize(DataDir);

        public static DateTimeOffset BlockTime(int height) =>
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(10 * height);

        public static Script OpReturn(string text) =>
            new(OpcodeType.OP_RETURN, Op.GetPushOp(Encoding.UTF8.GetBytes(text)));

        public static Script Payment() => new Key().GetScriptPubKey(ScriptPubKeyType.Segwit);

        // The hex the Scanner stores for a message: lowercase, as NBitcoin prints the pushed data.
        public static string Hex(string text) => Convert.ToHexString(Encoding.UTF8.GetBytes(text)).ToLowerInvariant();

        public static VerboseTransactionInfo Transaction(uint256 blockHash, DateTimeOffset blockTime, params Script[] outputs) =>
            new(new TransactionBlockInfo(blockHash, blockTime, 0),
                RandomUtils.GetUInt256(),
                Array.Empty<VerboseInputInfo>(),
                outputs.Select(script => new VerboseOutputInfo(Money.Zero, script, null)).ToList());

        // One transaction per message, each with a payment output before the OP_RETURN.
        public static VerboseBlockInfo Block(int height, params string[] messages)
        {
            var hash = RandomUtils.GetUInt256();
            var time = BlockTime(height);
            var transactions = messages.Select(text => Transaction(hash, time, Payment(), OpReturn(text))).ToList();

            return new VerboseBlockInfo(RandomUtils.GetUInt256(), 1, hash, time, (ulong)height, transactions);
        }

        public static BlockchainInfo SyncedNode(int tipHeight) => new() { Blocks = (ulong)tipHeight, Headers = (ulong)tipHeight };

        public static MessageModel Message(string text, DateTimeOffset blockMinedAt, string? transactionId = null) =>
            new(Guid.NewGuid(),
                transactionId ?? RandomUtils.GetUInt256().ToString(),
                text,
                Hex(text),
                RandomUtils.GetUInt256().ToString(),
                blockMinedAt);
    }
}
