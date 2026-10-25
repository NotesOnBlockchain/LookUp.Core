using LookUp.Core.Rpc;
using LookUp.Core.Rpc.Models;
using LookUp.Scanner.DataBase;
using LookUp.Scanner.Services;
using LookUp.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace LookUp.Tests.IntegrationTests
{
    // One scan pass at a time against a mocked node and the real database.
    [Collection(PostgresCollection.Name)]
    [Trait("Category", "Integration")]
    public class ScannerServiceTests : IAsyncLifetime
    {
        private const int TipHeight = 3;

        private readonly PostgresFixture _db;
        private readonly Mock<IRPCClient> _rpc = new();
        private readonly Dictionary<int, VerboseBlockInfo> _blocks = new();
        private readonly ScanStatus _scanStatus = new();
        private readonly ScannerService _scanner;

        public ScannerServiceTests(PostgresFixture db)
        {
            _db = db;

            for (int height = 0; height <= TipHeight; height++)
            {
                _blocks[height] = TestData.Block(height, $"Note from block {height}");
            }

            _rpc.Setup(r => r.GetBlockchainInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(TestData.SyncedNode(TipHeight));
            _rpc.Setup(r => r.GetBlockByHeightAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int height, CancellationToken _) => _blocks[height]);

            _scanner = new ScannerService(_rpc.Object, _db.Services.GetRequiredService<IServiceScopeFactory>(), _scanStatus);
        }

        public Task InitializeAsync() => _db.ResetAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task SetNextBlockHeightAsync(int nextBlockHeight)
        {
            await using var db = _db.CreateContext();
            var repository = new MessageRepository(db);

            if (await repository.GetNextBlockHeightAsync(CancellationToken.None) is null)
            {
                await repository.CreateScanProgressAsync(nextBlockHeight, CancellationToken.None);
            }
            else
            {
                await db.ScanProgress.ExecuteUpdateAsync(s => s.SetProperty(p => p.NextBlockHeight, nextBlockHeight));
            }
        }

        private async Task<(List<string> Messages, int? NextBlockHeight)> ReadStoredAsync()
        {
            await using var db = _db.CreateContext();
            var messages = await db.Messages.OrderBy(m => m.BlockMinedAt).Select(m => m.Message).ToListAsync();
            return (messages, await new MessageRepository(db).GetNextBlockHeightAsync(CancellationToken.None));
        }

        [Fact]
        public async Task ScansEveryBlockWithAConfirmationButNotTheTip()
        {
            await SetNextBlockHeightAsync(0);

            await _scanner.ScanAsync(CancellationToken.None);

            var (messages, nextBlockHeight) = await ReadStoredAsync();
            Assert.Equal(new[] { "Note from block 0", "Note from block 1", "Note from block 2" }, messages);
            Assert.Equal(TipHeight, nextBlockHeight);
            Assert.Equal(TipHeight, _scanStatus.Get().NextBlockHeight);
            _rpc.Verify(r => r.GetBlockByHeightAsync(TipHeight, It.IsAny<CancellationToken>()), Times.Never);    // A 1-block reorg could still replace it.
        }

        [Fact]
        public async Task BlockThatFailsIsRetriedOnTheNextPassWithoutLosingOrRepeatingMessages()
        {
            await SetNextBlockHeightAsync(0);
            _rpc.SetupSequence(r => r.GetBlockByHeightAsync(1, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("RPC connection reset"))
                .ReturnsAsync(_blocks[1]);

            await Assert.ThrowsAsync<HttpRequestException>(() => _scanner.ScanAsync(CancellationToken.None));

            var (messages, nextBlockHeight) = await ReadStoredAsync();
            Assert.Equal(new[] { "Note from block 0" }, messages);
            Assert.Equal(1, nextBlockHeight);
            Assert.Equal(1, _scanStatus.Get().NextBlockHeight);

            await _scanner.ScanAsync(CancellationToken.None);

            (messages, nextBlockHeight) = await ReadStoredAsync();
            Assert.Equal(new[] { "Note from block 0", "Note from block 1", "Note from block 2" }, messages);
            Assert.Equal(TipHeight, nextBlockHeight);
        }

        [Fact]
        public async Task RescanningStoredBlocksDoesNotStoreTheirMessagesTwice()
        {
            await SetNextBlockHeightAsync(0);
            await _scanner.ScanAsync(CancellationToken.None);

            await SetNextBlockHeightAsync(0);
            await _scanner.ScanAsync(CancellationToken.None);

            var (messages, nextBlockHeight) = await ReadStoredAsync();
            Assert.Equal(new[] { "Note from block 0", "Note from block 1", "Note from block 2" }, messages);
            Assert.Equal(TipHeight, nextBlockHeight);
        }

        // Being up to date counts as progress, otherwise the health check would call an idle scanner stalled.
        [Fact]
        public async Task UpToDateScannerReportsProgressWithoutFetchingBlocks()
        {
            await SetNextBlockHeightAsync(TipHeight);

            await _scanner.ScanAsync(CancellationToken.None);

            Assert.Equal(TipHeight, _scanStatus.Get().NextBlockHeight);
            _rpc.Verify(r => r.GetBlockByHeightAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MissingScanProgressStopsTheScanInsteadOfStartingFromGenesis()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => _scanner.ScanAsync(CancellationToken.None));

            var (messages, nextBlockHeight) = await ReadStoredAsync();
            Assert.Empty(messages);
            Assert.Null(nextBlockHeight);
            _rpc.Verify(r => r.GetBlockByHeightAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
