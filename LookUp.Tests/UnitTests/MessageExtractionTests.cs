using LookUp.Scanner.Services;
using LookUp.Tests.TestHelpers;
using NBitcoin;
using System.Text;

namespace LookUp.Tests.UnitTests
{
    public class MessageExtractionTests
    {
        private static readonly uint256 BlockHash = RandomUtils.GetUInt256();
        private static readonly DateTimeOffset BlockTime = TestData.BlockTime(1);

        [Fact]
        public void EveryOpReturnOutputOfATransactionIsCheckedOnItsOwn()
        {
            var tx = TestData.Transaction(BlockHash, BlockTime,
                TestData.Payment(),
                TestData.OpReturn("First note"),
                TestData.OpReturn("omni transfer"),    // Banned protocol prefix.
                TestData.OpReturn("Second note"));

            var messages = ScannerService.ExtractMessages(tx).ToList();

            Assert.Equal(new[] { "First note", "Second note" }, messages.Select(m => m.Message));
            Assert.Equal(new[] { TestData.Hex("First note"), TestData.Hex("Second note") }, messages.Select(m => m.Hex));
            Assert.All(messages, m => Assert.Equal(tx.Id.ToString(), m.TransactionID));
        }

        // Regression: an opcode after OP_RETURN used to be decoded as hex, which threw and failed the block on every retry.
        [Fact]
        public void OutputsWithoutAMessageAreSkippedAndTheRestIsStillRead()
        {
            var tx = TestData.Transaction(BlockHash, BlockTime,
                new Script(OpcodeType.OP_RETURN),
                new Script(OpcodeType.OP_RETURN, OpcodeType.OP_CHECKSIG),
                new Script(OpcodeType.OP_RETURN, OpcodeType.OP_13, Op.GetPushOp(Encoding.UTF8.GetBytes("rune data"))),
                new Script(OpcodeType.OP_RETURN, Op.GetPushOp(new byte[] { 0xff, 0xfe, 0x00, 0x41 })),    // Not UTF-8 text.
                TestData.OpReturn("Still here"));

            var message = Assert.Single(ScannerService.ExtractMessages(tx));

            Assert.Equal("Still here", message.Message);
        }
    }
}
