using System.ComponentModel.DataAnnotations;

namespace LookUp.Models
{
    public class MessageModel(Guid ID, string transactionID, string message, string hex, string blockHash, DateTimeOffset blockMinedAt)
    {
        [Key]
        public Guid ID { get; set; } = ID;
        public string TransactionID { get; set; } = transactionID;
        public string Message { get; set; } = message;
        public string Hex { get; set; } = hex;
        public string BlockHash { get; set; } = blockHash;
        public DateTimeOffset BlockMinedAt { get; set; } = blockMinedAt;
    }
}
