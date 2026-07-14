using LookUp.Serialization;
using static LookUp.Serialization.Encode;

namespace LookUp.Config
{
    public class WebsiteConfig : ConfigBase
    {
        public WebsiteConfig(string filePath) : base(filePath)
        {
        }

        public WebsiteConfig(string filePath, string backendUri) : base(filePath)
        {
            BackendUri = backendUri;
        }
        public string BackendUri { get; init;  } = Constants.Constants.DefaultBackendUri;

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
                var config = new WebsiteConfig(filePath);
                File.WriteAllTextAsync(filePath, config.EncodeAsJson());
                return config;
            }
        }

        protected override string EncodeAsJson() => JsonEncoder.ToReadableString(this, WebsiteConfigEncode.WebsiteConfig);
    }
}
