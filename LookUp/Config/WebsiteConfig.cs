using LookUp.Serialization;
using static LookUp.Serialization.Encode;

namespace LookUp.Config
{
    public class WebsiteConfig : ConfigBase
    {
        public WebsiteConfig(string filePath, string apiKey) : base(filePath, apiKey)
        {
        }

        public WebsiteConfig(string filePath, string apiKey, string backendUri) : base(filePath, apiKey)
        {
            BandendUri = backendUri;
        }
        public string BandendUri { get; init;  } = Constants.Constants.DefaultBackendUri;

        public static WebsiteConfig LoadFile(string filePath)
        {
            try
            {
                using var cfgFile = File.Open(filePath, FileMode.Open, FileAccess.Read);
                var decoder = JsonDecoder.FromStream(Decode.WebsiteConfigDecode.WebsiteConfig(filePath));
                var decodingResult = decoder(cfgFile);
                return decodingResult.Match(cfg => cfg, error => throw new InvalidOperationException(error));
            }
            catch (Exception)
            {
                var config = new WebsiteConfig(filePath, apiKey: "REPLACE-ME-WITH-YOUR-REAL-APIKEY");
                File.WriteAllTextAsync(filePath, config.EncodeAsJson());
                return config;
            }
        }

        protected override string EncodeAsJson() => JsonEncoder.ToReadableString(this, WebsiteConfigEncode.WebsiteConfig);
    }
}
