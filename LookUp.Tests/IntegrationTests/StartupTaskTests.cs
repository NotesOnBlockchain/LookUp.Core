using LookUp.Core.Rpc;
using LookUp.Scanner;
using LookUp.Scanner.DataBase;
using LookUp.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NBitcoin;
using NBitcoin.RPC;

namespace LookUp.Tests.IntegrationTests
{
    [Collection(PostgresCollection.Name)]
    [Trait("Category", "Integration")]
    public class StartupTaskTests : IAsyncLifetime
    {
        private readonly PostgresFixture _db;
        private readonly Mock<IRPCClient> _rpc = new();
        private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "LookUpTests", Guid.NewGuid().ToString());
        private readonly string _legacyFilePath;

        public StartupTaskTests(PostgresFixture db)
        {
            _db = db;
            _legacyFilePath = Path.Combine(_dataDir, "LastScannedBlockHeight.txt");
            Directory.CreateDirectory(_dataDir);

            _rpc.Setup(r => r.Network).Returns(Network.Main);
            _rpc.Setup(r => r.GetBlockchainInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(TestData.SyncedNode(850000));
        }

        public Task InitializeAsync() => _db.ResetAsync();

        public Task DisposeAsync()
        {
            Directory.Delete(_dataDir, recursive: true);
            return Task.CompletedTask;
        }

        private Task RunStartupAsync()
        {
            var config = new LookUp.Config.Config(Path.Combine(_dataDir, "Config.json"));
            var startupTask = new StartupTask(_rpc.Object, _db.Services.GetRequiredService<IServiceScopeFactory>(), config);
            return startupTask.ExecuteAsync(CancellationToken.None);
        }

        private async Task<int?> GetNextBlockHeightAsync()
        {
            await using var db = _db.CreateContext();
            return await new MessageRepository(db).GetNextBlockHeightAsync(CancellationToken.None);
        }

        [Fact]
        public async Task LegacyProgressFileIsCopiedOnceAndThenIgnored()
        {
            File.WriteAllText(_legacyFilePath, "812345");

            await RunStartupAsync();
            Assert.Equal(812345, await GetNextBlockHeightAsync());

            File.WriteAllText(_legacyFilePath, "5");
            await RunStartupAsync();
            Assert.Equal(812345, await GetNextBlockHeightAsync());
        }

        [Fact]
        public async Task WithoutALegacyFileScanningStartsAtGenesis()
        {
            await RunStartupAsync();

            Assert.Equal(0, await GetNextBlockHeightAsync());
        }

        [Fact]
        public async Task UnreadableLegacyFileFailsStartupInsteadOfStartingFromGenesis()
        {
            File.WriteAllText(_legacyFilePath, "eight hundred thousand");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(RunStartupAsync);

            Assert.Contains(_legacyFilePath, ex.Message);
            Assert.Null(await GetNextBlockHeightAsync());
        }

        [Fact]
        public async Task NodeThatIsStillSyncingFailsStartup()
        {
            _rpc.Setup(r => r.GetBlockchainInfoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BlockchainInfo { Blocks = 800000, Headers = 850000 });

            await Assert.ThrowsAsync<NotSupportedException>(RunStartupAsync);

            Assert.Null(await GetNextBlockHeightAsync());
        }
    }
}
