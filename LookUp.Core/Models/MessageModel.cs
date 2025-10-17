using NBitcoin;

namespace LookUp.Core.Models
{
    public record MessageModel(uint256 TransactionID, string Message, string Hex, uint256 blockHash, uint blockIndex, DateTimeOffset BlockMinedAt);
}
