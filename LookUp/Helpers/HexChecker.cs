using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace LookUp.Helpers
{
    public static class HexChecker
    {
        public static readonly string[] BannedHexPrefixes =
        {
            "6f6d6e69",     // "omni"
            "434e5452",     // "CNTR"
            "746f6b656e",   // "token"
            "6f7264",       // "ord"
            "75736474",     // "usdt" in ascii
            "4f55543a",
            "636f6e73",
            "425243323050"  // BRC20PROG
        };

        public static readonly string[] BannedMessageParts =
        {
            "to:USDT",
            "to:USDC",
            "to:TRX",
            "to:XRP",
            "to:BNB",
            "to:LTC",
            "to:ETH",
            "to:ASTER(BSC)",
            "to:ZEC(BSC)",
            "to:ASTER(BSC)",
            "to:PAXG(ERC20)",
            "to:AVAX(C-Chain)",
            "to:DAI(BSC)",
            "to:XAUT(ERC20)",
            "to:BTCB",
            "to:GALA(ERC20)",
            "from:8900POL(POL):",
            "USDT(ERC20)",
            "USDT(TRON)",
            "USDT(SOL)",
            "USDT(BSC)",
            "BRC20PROG",
            "=:tr",
            "=:l:ltc",
            "TRON.USDT",
            "TRON.USDC",
            "SYMB",
            "ETH.USDT",
            "BSC.USDT",
            "ETH.USDC",
            "to:SOL",
            "\"p\":\"brc-20\",\"op\":\"mint\""
        };

        public static bool FilterOutMessages(string hex, [NotNullWhen(true)] out string? message)
        {
            message = null;

            // Convert hex to byte[]
            byte[] bytes = Enumerable.Range(0, hex.Length / 2)
                .Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16))
                .ToArray();

            try
            {
                message = Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return false;
            }

            // 2. Block known protocol prefixes
            if (BannedHexPrefixes.Any(hex.ToLowerInvariant().StartsWith))
                return false;

            // 3. Require human-readable ratio
            int letters = message.Count(char.IsLetter);
            if (letters < message.Length * 0.4) // At least 40% is a letter
                return false;

            // 4. Allowed characters only
            bool allowed = message.All(c =>
                char.IsLetterOrDigit(c) ||
                char.IsWhiteSpace(c) ||
                ".,!?;:-_+=()[]{}\"'@#$%^&*/\\|<>".Contains(c)
            );

            if (!allowed)  
                return false;

            // 5. Length sanity check
            if (message.Length < 3 || message.Length > 80)
                return false;

            if (message.Equals("BRC20PROG"))
                return false;

            if (BannedMessageParts.Any(message.Contains))
                return false;

            return true;
        }
    }
}
