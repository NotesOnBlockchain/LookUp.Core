using LookUp.Core.Constants;
using Microsoft.Extensions.Logging;
using NBitcoin;
using static LookUp.Core.Serialization.Encode;

namespace LookUp.Core.Config
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
            string regTestBitcoinRpcUri

            ) : base(filePath)
            {
                Network = network;
                BitcoinRpcConnectionString = bitcoinRpcConnectionString;

                MainNetBitcoinRpcUri = mainNetBitcoinRpcUri;
                TestNetBitcoinRpcUri = testNetBitcoinRpcUri;
                RegTestBitcoinRpcUri = regTestBitcoinRpcUri;
        }

        public Network Network { get; set; } = Network.Main;

        public string MainNetBitcoinRpcUri { get; set; } = Constants.Constants.DefaultMainNetBitcoinRpcUri;

        public string TestNetBitcoinRpcUri { get; set; } = Constants.Constants.DefaultTestNetBitcoinRpcUri;

        public string RegTestBitcoinRpcUri { get; set; } = Constants.Constants.DefaultRegTestBitcoinRpcUri;

        public string BitcoinRpcConnectionString { get; set; } = "user:password";

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
                var decoder = Serialization.JsonDecoder.FromStream(Serialization.Decode.ConfigDecode.Config(filePath));
                var decodingResult = decoder(cfgFile);
                return decodingResult.Match(cfg => cfg, error => throw new InvalidOperationException(error));
            }
            catch (Exception)
            {
                var config = new Config(filePath);
                File.WriteAllTextAsync(filePath, config.EncodeAsJson());
                return config;
            }
        }

        protected override string EncodeAsJson() => Serialization.JsonEncoder.ToReadableString(this, ConfigEncode.Config);
    }
}
