using LookUp.Core.Config;


namespace LookUp.Website.Config
{
    public class WebsiteConfig : ConfigBase
    {
        public WebsiteConfig(string filePath) : base(filePath)
        {
        }

        public WebsiteConfig(string filePath, string backendUri) : base(filePath)
        {
            BandendUri = backendUri;
        }
        public string BandendUri { get; }

        public static WebsiteConfig LoadFile(string filePath)
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
                var config = new WebsiteConfig(filePath);
                File.WriteAllTextAsync(filePath, config.EncodeAsJson());
                return config;
            }
        }

        protected override string EncodeAsJson() => JsonEncoder.ToReadableString(this, ConfigEncode.Config);
    }
}
