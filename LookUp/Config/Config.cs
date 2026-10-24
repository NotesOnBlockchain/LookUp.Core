using LookUp.Serialization;
using NBitcoin;
using static LookUp.Serialization.Encode;

namespace LookUp.Config
{
    public class Config : ConfigBase
    {
        public Config(string filePath) : base(filePath)
        {
        }

        public Config(
            string filePath,
            Network network,
            string bitcoinRpcConnectionString,
            string mainNetBitcoinRpcUri,
            string testNetBitcoinRpcUri,
            string regTestBitcoinRpcUri,
            string sqlconnectionstring) : base(filePath)
        {
                Network = network;
                BitcoinRpcConnectionString = bitcoinRpcConnectionString;

                MainNetBitcoinRpcUri = mainNetBitcoinRpcUri;
                TestNetBitcoinRpcUri = testNetBitcoinRpcUri;
                RegTestBitcoinRpcUri = regTestBitcoinRpcUri;
                SQLConnectionString = sqlconnectionstring;
        }

        public Network Network { get; set; } = Network.Main;

        public string MainNetBitcoinRpcUri { get; set; } = Constants.Constants.DefaultMainNetBitcoinRpcUri;

        public string TestNetBitcoinRpcUri { get; set; } = Constants.Constants.DefaultTestNetBitcoinRpcUri;

        public string RegTestBitcoinRpcUri { get; set; } = Constants.Constants.DefaultRegTestBitcoinRpcUri;

        public string BitcoinRpcConnectionString { get; set; } = "user:password";

        public string SQLConnectionString { get; set; } = "Server=myserver;Host=myhost;Port=myport;Database=mydatabase;User Id=User;Password=Password";

        public string GetBitcoinRpcUri() =>
            Network switch
            {
                _ when Network == Network.Main => MainNetBitcoinRpcUri,
                _ when Network == Network.TestNet => TestNetBitcoinRpcUri,
                _ when Network == Network.RegTest => RegTestBitcoinRpcUri,
                _ => throw new NotSupportedException(Network.ToString())
            };

        public static Config LoadFile(string filePath)
        {
            // Only a missing file is created with defaults. An unreadable file is left untouched and fails startup,
            // otherwise a typo would silently replace the RPC and database settings with defaults.
            if (!File.Exists(filePath))
            {
                var config = new Config(filePath);
                config.ToFile();
                return config;
            }

            using var cfgFile = File.Open(filePath, FileMode.Open, FileAccess.Read);
            var decoder = JsonDecoder.FromStream(Decode.ConfigDecode.Config(filePath));
            var decodingResult = decoder(cfgFile);
            return decodingResult.Match(cfg => cfg, error => throw new InvalidOperationException($"Could not read config file '{filePath}': {error} The file was left unchanged; fix it or delete it to recreate the defaults."));
        }

        protected override string EncodeAsJson() => JsonEncoder.ToReadableString(this, ConfigEncode.Config);
    }
}
