using LookUp.Core.Rpc;
using LookUp.Models;
using LookUp.Scanner.Services;
using LookUp.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using NBitcoin;
using NBitcoin.RPC;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LookUp.Tests.IntegrationTests
{
    // The Scanner's real Startup, served in memory, with a mocked node and the scanning loop switched off.
    [Collection(PostgresCollection.Name)]
    [Trait("Category", "Integration")]
    public class ScannerApiTests : IAsyncLifetime
    {
        private readonly PostgresFixture _db;

        public ScannerApiTests(PostgresFixture db) => _db = db;

        public Task InitializeAsync() => _db.ResetAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        // A new app per test, so the search cache never carries results over from another test.
        private WebApplicationFactory<LookUp.Core.Program> CreateApp(BlockchainInfo? nodeState = null)
        {
            new LookUp.Config.Config(
                Path.Combine(TestData.DataDir, "Config.json"),
                Network.Main,
                "user:password",
                Constants.Constants.DefaultMainNetBitcoinRpcUri,
                Constants.Constants.DefaultTestNetBitcoinRpcUri,
                Constants.Constants.DefaultRegTestBitcoinRpcUri,
                _db.ConnectionString).ToFile();

            var rpc = new Mock<IRPCClient>();
            rpc.Setup(r => r.Network).Returns(Network.Main);
            rpc.Setup(r => r.GetBlockchainInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(nodeState ?? TestData.SyncedNode(850000));

            return new WebApplicationFactory<LookUp.Core.Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("datadir", TestData.DataDir);
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IRPCClient>();
                    services.AddSingleton(rpc.Object);
                    services.Remove(services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ScannerService)));
                });
            });
        }

        private async Task StoreAsync(params MessageModel[] messages)
        {
            await using var db = _db.CreateContext();
            db.Messages.AddRange(messages);
            await db.SaveChangesAsync();
        }

        // The website builds the URL with Uri.EscapeDataString and reads the body as List<MessageModel>.
        [Fact]
        public async Task SearchReturnsMessagesInTheShapeTheWebsiteReads()
        {
            var stored = TestData.Message("I am 100% sure", TestData.BlockTime(1));
            await StoreAsync(stored);
            await using var app = CreateApp();

            var response = await app.CreateClient().GetAsync($"/search?query={Uri.EscapeDataString("100% sure")}");

            response.EnsureSuccessStatusCode();
            var message = Assert.Single(await response.Content.ReadFromJsonAsync<List<MessageModel>>() ?? new());
            Assert.Equal(stored.ID, message.ID);
            Assert.Equal(stored.TransactionID, message.TransactionID);
            Assert.Equal(stored.Message, message.Message);
            Assert.Equal(stored.Hex, message.Hex);
            Assert.Equal(stored.BlockHash, message.BlockHash);
            Assert.Equal(stored.BlockMinedAt, message.BlockMinedAt);
        }

        [Theory]
        [InlineData("/search")]
        [InlineData("/search?query=ab")]
        [InlineData("/search?query=%20%20ab%20%20")]    // Spaces don't count toward the minimum.
        public async Task TooShortQueryGetsAnEmptyResult(string url)
        {
            await StoreAsync(TestData.Message("about this", TestData.BlockTime(1)));
            await using var app = CreateApp();

            var response = await app.CreateClient().GetAsync(url);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(await response.Content.ReadFromJsonAsync<List<MessageModel>>() ?? new());
        }

        [Fact]
        public async Task TooLongQueryGetsAnEmptyResult()
        {
            await StoreAsync(TestData.Message(new string('x', Constants.Constants.MaxSearchQueryLength + 10), TestData.BlockTime(1)));
            await using var app = CreateApp();
            var client = app.CreateClient();

            var atLimit = await client.GetFromJsonAsync<List<MessageModel>>($"/search?query={new string('x', Constants.Constants.MaxSearchQueryLength)}");
            var overLimit = await client.GetFromJsonAsync<List<MessageModel>>($"/search?query={new string('x', Constants.Constants.MaxSearchQueryLength + 1)}");

            Assert.Single(atLimit!);
            Assert.Empty(overLimit!);
        }

        [Fact]
        public async Task HealthIsOkWhenTheNodeIsSyncedAndTheDatabaseAnswers()
        {
            await using var app = CreateApp();

            var response = await app.CreateClient().GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var health = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("healthy", health.GetProperty("rpc").GetString());
            Assert.Equal("healthy", health.GetProperty("database").GetString());
        }

        // The website container waits for this check, so a node that is still syncing must fail it.
        [Fact]
        public async Task HealthFailsWhileTheNodeIsSyncing()
        {
            await using var app = CreateApp(new BlockchainInfo { Blocks = 800000, Headers = 850000 });

            var response = await app.CreateClient().GetAsync("/health");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var health = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("failed!", health.GetProperty("rpc").GetString());
            Assert.Equal("healthy", health.GetProperty("database").GetString());
        }
    }
}
