using LookUp.Config;

namespace LookUp.Tests.UnitTests
{
    public class ConfigLoadingTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "LookUpTests", Guid.NewGuid().ToString());

        public ConfigLoadingTests() => Directory.CreateDirectory(_dir);

        public void Dispose() => Directory.Delete(_dir, recursive: true);

        [Fact]
        public void MissingWebsiteConfigIsCreatedWithDefaults()
        {
            string path = Path.Combine(_dir, "Config.json");

            var config = WebsiteConfig.LoadFile(path);

            Assert.Equal(Constants.Constants.DefaultBackendUri, config.BackendUri);
            Assert.True(File.Exists(path));
            Assert.Equal(Constants.Constants.DefaultBackendUri, WebsiteConfig.LoadFile(path).BackendUri);    // The written file reads back.
        }

        [Fact]
        public void ValidWebsiteConfigIsRead()
        {
            string path = Path.Combine(_dir, "Config.json");
            File.WriteAllText(path, """{ "BackendUri": "http://lookup-core:37127" }""");

            Assert.Equal("http://lookup-core:37127", WebsiteConfig.LoadFile(path).BackendUri);
        }

        [Theory]
        [InlineData("""{ "BackendUri": "http://lookup-core:37127", }""")]    // Trailing comma.
        [InlineData("""{ "BackendUrl": "http://lookup-core:37127" }""")]     // Misspelled key.
        [InlineData("")]
        public void BrokenWebsiteConfigThrowsAndIsLeftUnchanged(string content)
        {
            string path = Path.Combine(_dir, "Config.json");
            File.WriteAllText(path, content);

            var ex = Assert.Throws<InvalidOperationException>(() => WebsiteConfig.LoadFile(path));

            Assert.Contains(path, ex.Message);
            Assert.Equal(content, File.ReadAllText(path));
        }

        [Fact]
        public void BrokenScannerConfigThrowsAndIsLeftUnchanged()
        {
            string path = Path.Combine(_dir, "Config.json");
            string content = """{ "Network": "Main", "SQLConnectionString": "kept" }""";    // Required RPC keys are missing.
            File.WriteAllText(path, content);

            Assert.Throws<InvalidOperationException>(() => LookUp.Config.Config.LoadFile(path));
            Assert.Equal(content, File.ReadAllText(path));
        }

        [Fact]
        public void MissingScannerConfigIsCreatedWithDefaults()
        {
            string path = Path.Combine(_dir, "Config.json");

            var config = LookUp.Config.Config.LoadFile(path);

            Assert.True(File.Exists(path));
            Assert.Equal(config.SQLConnectionString, LookUp.Config.Config.LoadFile(path).SQLConnectionString);
        }
    }
}
