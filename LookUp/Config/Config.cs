using LookUp.Serialization;
using NBitcoin;
using LookUp.Constants;
using static LookUp.Serialization.Encode;

namespace LookUp.Config
{
    public class Config : ConfigBase
    {
        public Config(string filePath, string apiKey) : base(filePath, apiKey)
        {
        }

        public Config(
            string filePath,
            string apiKey,
            Network network,
            string bitcoinRpcConnectionString,
            string mainNetBitcoinRpcUri,
            string testNetBitcoinRpcUri,
            string regTestBitcoinRpcUri,
            string sqlconnectionstring) : base(filePath, apiKey)
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
            try
            {
                using var cfgFile = File.Open(filePath, FileMode.Open, FileAccess.Read);
                var decoder = JsonDecoder.FromStream(Decode.ConfigDecode.Config(filePath));
                var decodingResult = decoder(cfgFile);
                return decodingResult.Match(cfg => cfg, error => throw new InvalidOperationException(error));
            }
            catch (Exception)
            {
                var config = new Config(filePath, apiKey: "REPLACE-ME-WITH-YOUR-REAL-APIKEY");
                File.WriteAllTextAsync(filePath, config.EncodeAsJson());
                return config;
            }
        }

        protected override string EncodeAsJson() => JsonEncoder.ToReadableString(this, ConfigEncode.Config);
    }
}
