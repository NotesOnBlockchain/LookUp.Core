using NBitcoin;

namespace LookUp.Core.Models
{
    public record MessageModel(uint256 TransactionID, string Message, string Hex, uint256 BlockHash, uint BlockIndex, DateTimeOffset BlockMinedAt);
}
