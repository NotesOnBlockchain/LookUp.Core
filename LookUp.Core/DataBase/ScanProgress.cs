using System.ComponentModel.DataAnnotations;

namespace LookUp.Scanner.DataBase
{
    // Single row that records how far the Scanner got. It lives in the database so that it is saved in the same
    // transaction as the block's messages: progress can never move past messages that weren't stored.
    public class ScanProgress
    {
        public const int SingletonID = 1;

        [Key]
        public int ID { get; set; } = SingletonID;

        // Height of the next block to scan; every block below it has been fully stored.
        public int NextBlockHeight { get; set; }
    }
}
