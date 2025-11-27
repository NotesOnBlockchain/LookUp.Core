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
            ":to:USDT",
            "to:USDC",
            ":to:TRX",
            ":to:XRP",
            "to:BNB",
            "to:LTC",
            "USDT(ERC20)",
            "USDT(TRON)",
            "USDT(SOL)",
            "USDT(BSC)",
            "BRC20PROG",
            "=:tr:",
            "=:l:ltc",
            "TRON.USDT",
            "TRON.USDC",
            "SYMB:"
        };

        public static bool FilterOutMessages((byte[] bytes, string hex) instance, out string? message)
        {
            message = null;
            try
            {
                message = Encoding.UTF8.GetString(instance.bytes);
            }
            catch
            {
                return false;
            }

            // 2. Block known protocol prefixes
            if (BannedHexPrefixes.Any(instance.hex.ToLowerInvariant().StartsWith))
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
