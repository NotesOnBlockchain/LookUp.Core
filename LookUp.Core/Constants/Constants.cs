namespace LookUp.Core.Constants
{
    public static class Constants
    {
        public static readonly string DefaultMainNetBitcoinRpcUri = $"http://localhost:{DefaultMainNetBitcoinRpcPort}";
        public static readonly string DefaultTestNetBitcoinRpcUri = $"http://localhost:{DefaultTestNetBitcoinRpcPort}";
        public static readonly string DefaultRegTestBitcoinRpcUri = $"http://localhost:{DefaultRegTestBitcoinCorePort}";

        public const int DefaultMainNetBitcoinRpcPort = 8332;
        public const int DefaultTestNetBitcoinRpcPort = 48332;
        public const int DefaultRegTestBitcoinCorePort = 18443;
    }
}
