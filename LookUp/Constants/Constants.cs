namespace LookUp.Constants
{
    public static class Constants
    {
        public static readonly string DefaultMainNetBitcoinRpcUri = $"http://localhost:{DefaultMainNetBitcoinRpcPort}";
        public static readonly string DefaultTestNetBitcoinRpcUri = $"http://localhost:{DefaultTestNetBitcoinRpcPort}";
        public static readonly string DefaultRegTestBitcoinRpcUri = $"http://localhost:{DefaultRegTestBitcoinCorePort}";

        public const int DefaultMainNetBitcoinRpcPort = 8332;
        public const int DefaultTestNetBitcoinRpcPort = 48332;
        public const int DefaultRegTestBitcoinCorePort = 18443;

        public static readonly string DefaultBackendUri = $"http://localhost:37127";

        // Maximum number of messages a single search returns. The website uses it to tell visitors when results are cut off.
        public const int MaxSearchResults = 20;

        // Longer queries get an empty result from the Scanner. The website's search box enforces the same limit.
        public const int MaxSearchQueryLength = 200;

        // Shorter queries (after trimming) get an empty result from the Scanner, matching the website's minimum.
        // Message search needs at least 3 characters for PostgreSQL's trigram index; shorter patterns scan the whole table.
        public const int MinSearchQueryLength = 3;
    }
}
