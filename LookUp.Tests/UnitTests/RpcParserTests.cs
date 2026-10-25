using LookUp.Core.Rpc;
using LookUp.Core.Rpc.Models;
using LookUp.Scanner.Services;
using NBitcoin;

namespace LookUp.Tests.UnitTests
{
    public class RpcParserTests
    {
        private const string BlockHash = "0000000000000000000320283a032748cef8227873ff4872689bf23f1cda83a5";
        private const string PreviousBlockHash = "0000000000000000000172014ba58d66455762add0512355ad651207918494ab";
        private const string CoinbaseTxId = "b23b2a3e43e8a19bd22d8ec7aa3f9bb04b85fc6ea1bc5e5c3b5bc3a2d6e1a4f0";
        private const string MessageTxId = "4e3f6a2b1c0d9e8f7a6b5c4d3e2f1a0b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f";
        private const string SpentTxId = "1111111111111111111111111111111111111111111111111111111111111111";
        private const string PaymentScriptHex = "0014751e76e8199196d454941c45d1b3a323f1433bd6";
        private const uint BlockUnixTime = 1713571767;

        // A getblock response at verbosity 2, plus one input with the "prevout" that verbosity 3 adds.
        private static string GetBlockResponse(bool withPreviousBlock = true) => $$"""
            {
              "hash": "{{BlockHash}}",
              "confirmations": 3,
              "height": 840000,
              "time": {{BlockUnixTime}},
              {{(withPreviousBlock ? $"\"previousblockhash\": \"{PreviousBlockHash}\"," : "")}}
              "tx": [
                {
                  "txid": "{{CoinbaseTxId}}",
                  "vin": [ { "coinbase": "03c0d10c" } ],
                  "vout": [ { "value": 3.125, "scriptPubKey": { "hex": "{{PaymentScriptHex}}", "type": "witness_v0_keyhash" } } ]
                },
                {
                  "txid": "{{MessageTxId}}",
                  "vin": [
                    { "txid": "{{SpentTxId}}", "vout": 1 },
                    { "txid": "{{SpentTxId}}", "vout": 2, "prevout": { "value": 0.5, "scriptPubKey": { "hex": "{{PaymentScriptHex}}", "type": "witness_v0_keyhash" } } }
                  ],
                  "vout": [
                    { "value": 0.0, "scriptPubKey": { "hex": "6a0c48656c6c6f20776f726c6421", "type": "nulldata" } },
                    { "value": 0.0001, "scriptPubKey": { "hex": "{{PaymentScriptHex}}", "type": "witness_v0_keyhash" } }
                  ]
                }
              ]
            }
            """;

        [Theory]
        [InlineData(false)]
        [InlineData(true)]    // The raw RPC reply wraps the block in "result".
        public void ParsesTheBlockAndItsTransactionsInOrder(bool wrappedInResult)
        {
            string json = wrappedInResult ? $$"""{ "result": {{GetBlockResponse()}}, "error": null, "id": 1 }""" : GetBlockResponse();

            VerboseBlockInfo block = RpcParser.ParseVerboseBlockResponse(json);

            Assert.Equal(uint256.Parse(BlockHash), block.Hash);
            Assert.Equal(uint256.Parse(PreviousBlockHash), block.PrevBlockHash);
            Assert.Equal(840000UL, block.Height);
            Assert.Equal(3UL, block.Confirmations);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(BlockUnixTime), block.BlockTime);

            var transactions = block.Transactions.ToList();
            Assert.Equal(new[] { CoinbaseTxId, MessageTxId }, transactions.Select(tx => tx.Id.ToString()));
            Assert.Equal(new uint[] { 0, 1 }, transactions.Select(tx => tx.BlockInfo.BlockIndex));
            Assert.All(transactions, tx => Assert.Equal(block.Hash, tx.BlockInfo.BlockHash));
            Assert.All(transactions, tx => Assert.Equal(block.BlockTime, tx.BlockInfo.BlockTime));
        }

        [Fact]
        public void ParsesEveryKindOfInputAndOutput()
        {
            var transactions = RpcParser.ParseVerboseBlockResponse(GetBlockResponse()).Transactions.ToList();

            Assert.Equal(new VerboseInputInfo.Coinbase("03c0d10c"), Assert.Single(transactions[0].Inputs));

            var inputs = transactions[1].Inputs.ToList();
            Assert.Equal(new VerboseInputInfo.None(new OutPoint(uint256.Parse(SpentTxId), 1)), inputs[0]);
            var full = Assert.IsType<VerboseInputInfo.Full>(inputs[1]);
            Assert.Equal(new OutPoint(uint256.Parse(SpentTxId), 2), full.OutPoint);
            Assert.Equal(Money.Coins(0.5m), full.PrevOut.Value);

            var outputs = transactions[1].Outputs.ToList();
            Assert.Equal(RpcPubkeyType.TxNullData, outputs[0].PubkeyType);
            Assert.Equal("6a0c48656c6c6f20776f726c6421", outputs[0].ScriptPubKey.ToHex());
            Assert.Equal(RpcPubkeyType.TxWitnessV0Keyhash, outputs[1].PubkeyType);
            Assert.Equal(Money.Coins(0.0001m), outputs[1].Value);
        }

        [Fact]
        public void GenesisBlockHasNoPreviousBlock()
        {
            var block = RpcParser.ParseVerboseBlockResponse(GetBlockResponse(withPreviousBlock: false));

            Assert.Equal(uint256.Zero, block.PrevBlockHash);
        }

        // The whole path from the node's reply to what the Scanner stores, without the database.
        [Fact]
        public void MessageFromAGetBlockResponseCarriesItsTransactionAndBlock()
        {
            var block = RpcParser.ParseVerboseBlockResponse(GetBlockResponse());

            var message = Assert.Single(block.Transactions.SelectMany(ScannerService.ExtractMessages));

            Assert.Equal("Hello world!", message.Message);
            Assert.Equal("48656c6c6f20776f726c6421", message.Hex);
            Assert.Equal(MessageTxId, message.TransactionID);
            Assert.Equal(BlockHash, message.BlockHash);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(BlockUnixTime), message.BlockMinedAt);
        }
    }
}
