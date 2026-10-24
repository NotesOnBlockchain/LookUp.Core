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
            // Only a missing file is created with defaults. An unreadable file is left untouched and fails startup,
            // otherwise a typo would silently replace the operator's settings with defaults.
            if (!File.Exists(filePath))
            {
                var config = new WebsiteConfig(filePath);
                config.ToFile();
                return config;
            }

            using var cfgFile = File.Open(filePath, FileMode.Open, FileAccess.Read);
            var decoder = JsonDecoder.FromStream(Decode.WebsiteConfigDecode.WebsiteConfig(filePath));
            var decodingResult = decoder(cfgFile);
            return decodingResult.Match(cfg => cfg, error => throw new InvalidOperationException($"Could not read config file '{filePath}': {error} The file was left unchanged; fix it or delete it to recreate the defaults."));
        }

        protected override string EncodeAsJson() => JsonEncoder.ToReadableString(this, WebsiteConfigEncode.WebsiteConfig);
    }
}
