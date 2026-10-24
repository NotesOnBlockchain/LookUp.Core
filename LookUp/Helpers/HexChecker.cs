using System.Diagnostics.CodeAnalysis;
using System.Text;
using LookUp.Models;

namespace LookUp.Helpers
{
    public static class HexChecker
    {
        public const double MinLetterRatio = 0.4;
        public const int MinMessageLength = 3;
        public const int MaxMessageLength = 80;
        public const string AllowedPunctuation = ".,!?;:-_+=()[]{}\"'@#$%^&*/\\|<>";
        public const string BannedMessagePrefix = "0x";

        public static readonly string[] BannedExactMessages = { "BRC20PROG" };

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
            "to:PAXG(ERC20)",
            "to:AVAX(C-Chain)",
            "to:DAI",
            "to:XAUT(ERC20)",
            "to:BTCB",
            "to:GALA(ERC20)",
            "from:8900POL(POL):",
            "USDT(ERC20)",
            "USDT(TRON)",
            "USDT(SOL)",
            "USDT(BSC)",
            "=:tr",
            "=:l:ltc",
            "TRON.USDT",
            "TRON.USDC",
            "SYMB",
            "ETH.USDT",
            "BSC.USDT",
            "ETH.USDC",
            "to:SOL",
            "to:S(Sonic):",
            "to:TON",
            "to:FET(ERC20)",
            "to:TWT(BSC)",
            "to:PEPE",
            "to:HYPE(HyperEVM)",
            "to:SUI",
            "to:DEEP(SUI)",
            "\"p\":\"brc-20\",\"op\":\"mint\"",
            "to:GRAM",
            "to:POL",
            "to:PYUSD",
            "to:CAKE",
            "EVST1:"
        };

        public static bool FilterOutMessages(string hex, [NotNullWhen(true)] out string? message)
        {
            message = null;

            // 1. Block known protocol prefixes
            if (BannedHexPrefixes.Any(hex.ToLowerInvariant().StartsWith))
                return false;

            // 2. Convert hex to byte[]
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

            // 3. Filter out shitcoin contracts.
            if (BannedMessageParts.Any(message.Contains))
                return false;

            // 4. Require human-readable ratio
            int letters = message.Count(char.IsLetter);
            if (letters < message.Length * MinLetterRatio)
                return false;

            // 5. Allowed characters only
            bool allowed = message.All(c =>
                char.IsLetterOrDigit(c) ||
                char.IsWhiteSpace(c) ||
                AllowedPunctuation.Contains(c)
            );

            if (!allowed)
                return false;

            // 6. Length sanity check
            if (message.Length < MinMessageLength || message.Length > MaxMessageLength)
                return false;

            // 7. if it starts with "0x"
            if (message.StartsWith(BannedMessagePrefix, StringComparison.Ordinal))
                return false;

            if (BannedExactMessages.Contains(message))
                return false;

            return true;
        }

        // Describes FilterOutMessages in words, built from the same values it checks, so the published rules can't drift from the code.
        public static FilterRules DescribeRules() => new(
            Summary: "An OP_RETURN output is indexed only if its data passes every rule below, checked in this order.",
            Rules: new[]
            {
                "The data must not start with one of the banned hex prefixes.",
                "The data is decoded as UTF-8 text.",
                "The text must not contain any of the banned message parts.",
                $"At least {(int)(MinLetterRatio * 100)}% of the characters must be letters.",
                $"Only letters, digits, whitespace and these characters are allowed: {AllowedPunctuation}",
                $"The text must be {MinMessageLength} to {MaxMessageLength} characters long.",
                $"The text must not start with \"{BannedMessagePrefix}\".",
                $"The text must not be exactly one of: {string.Join(", ", BannedExactMessages.Select(m => $"\"{m}\""))}."
            },
            BannedHexPrefixes: BannedHexPrefixes
                .Select(hex => new BannedHexPrefix(hex, Encoding.UTF8.GetString(Convert.FromHexString(hex))))
                .ToArray(),
            BannedMessageParts: BannedMessageParts);
    }
}
