using LookUp.Models;
using LookUp.Scanner.DataBase;
using LookUp.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LookUp.Tests.IntegrationTests
{
    [Collection(PostgresCollection.Name)]
    [Trait("Category", "Integration")]
    public class SaveBlockTests : IAsyncLifetime
    {
        private const int StartHeight = 10;

        private readonly PostgresFixture _db;

        public SaveBlockTests(PostgresFixture db) => _db = db;

        public async Task InitializeAsync()
        {
            await _db.ResetAsync();
            await using var db = _db.CreateContext();
            await new MessageRepository(db).CreateScanProgressAsync(StartHeight, CancellationToken.None);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task SaveBlockAsync(params MessageModel[] messages)
        {
            await using var db = _db.CreateContext();
            await new MessageRepository(db).SaveBlockAsync(messages, StartHeight + 1, CancellationToken.None);
        }

        private async Task<(List<MessageModel> Messages, int? NextBlockHeight)> ReadStoredAsync()
        {
            await using var db = _db.CreateContext();
            return (await db.Messages.OrderBy(m => m.Message).ToListAsync(), await new MessageRepository(db).GetNextBlockHeightAsync(CancellationToken.None));
        }

        [Fact]
        public async Task StoresTheMessagesAndAdvancesTheProgress()
        {
            await SaveBlockAsync(TestData.Message("First note", TestData.BlockTime(StartHeight)), TestData.Message("Second note", TestData.BlockTime(StartHeight)));

            var (messages, nextBlockHeight) = await ReadStoredAsync();
            Assert.Equal(new[] { "First note", "Second note" }, messages.Select(m => m.Message));
            Assert.Equal(StartHeight + 1, nextBlockHeight);
        }

        // A duplicate (TransactionID, Hex) would violate the unique key and roll back the whole block, failing it on every retry.
        [Fact]
        public async Task MessagesAlreadyStoredOrRepeatedInTheBlockAreSkipped()
        {
            var stored = TestData.Message("Stored earlier", TestData.BlockTime(StartHeight));
            await using (var db = _db.CreateContext())
            {
                db.Messages.Add(stored);
                await db.SaveChangesAsync();
            }

            var repeated = TestData.Message("Twice in one tx", TestData.BlockTime(StartHeight));
            var sameDataOtherTx = TestData.Message("Twice in one tx", TestData.BlockTime(StartHeight));    // Same Hex, different TxID: a different message.

            await SaveBlockAsync(
                TestData.Message(stored.Message, stored.BlockMinedAt, stored.TransactionID),
                repeated,
                TestData.Message(repeated.Message, repeated.BlockMinedAt, repeated.TransactionID),
                sameDataOtherTx);

            var (messages, nextBlockHeight) = await ReadStoredAsync();
            Assert.Equal(
                new[] { (stored.TransactionID, stored.Hex), (repeated.TransactionID, repeated.Hex), (sameDataOtherTx.TransactionID, sameDataOtherTx.Hex) }.Order(),
                messages.Select(m => (m.TransactionID, m.Hex)).Order());
            Assert.Equal(StartHeight + 1, nextBlockHeight);
        }

        [Fact]
        public async Task FailedWriteStoresNothingAndKeepsTheProgress()
        {
            var broken = TestData.Message("Broken", TestData.BlockTime(StartHeight));
            broken.BlockHash = null!;    // Violates NOT NULL, so PostgreSQL rejects the write.

            await Assert.ThrowsAsync<DbUpdateException>(() => SaveBlockAsync(TestData.Message("Valid note", TestData.BlockTime(StartHeight)), broken));

            var (messages, nextBlockHeight) = await ReadStoredAsync();
            Assert.Empty(messages);
            Assert.Equal(StartHeight, nextBlockHeight);
        }
    }
}
