using LookUp.Models;
using LookUp.Scanner.DataBase;
using LookUp.Tests.TestHelpers;

namespace LookUp.Tests.IntegrationTests
{
    [Collection(PostgresCollection.Name)]
    [Trait("Category", "Integration")]
    public class MessageSearchTests : IAsyncLifetime
    {
        private readonly PostgresFixture _db;

        public MessageSearchTests(PostgresFixture db) => _db = db;

        public Task InitializeAsync() => _db.ResetAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task StoreAsync(params MessageModel[] messages)
        {
            await using var db = _db.CreateContext();
            db.Messages.AddRange(messages);
            await db.SaveChangesAsync();
        }

        private async Task<List<MessageModel>> SearchAsync(string query)
        {
            await using var db = _db.CreateContext();
            return await new MessageRepository(db).FindAsync(query, CancellationToken.None);
        }

        [Fact]
        public async Task FindsAMessageByItsExactTransactionIdBlockHashOrHex()
        {
            var target = TestData.Message("Hello Satoshi", TestData.BlockTime(1));
            await StoreAsync(target, TestData.Message("Another note", TestData.BlockTime(2)));

            Assert.Equal(target.ID, Assert.Single(await SearchAsync(target.TransactionID)).ID);
            Assert.Equal(target.ID, Assert.Single(await SearchAsync(target.BlockHash)).ID);
            Assert.Equal(target.ID, Assert.Single(await SearchAsync(target.Hex)).ID);

            Assert.Empty(await SearchAsync(target.TransactionID[..32]));    // Identifiers match whole, not by prefix.
        }

        [Fact]
        public async Task MessageTextMatchesAnywhereIgnoringCase()
        {
            await StoreAsync(TestData.Message("Hello Satoshi", TestData.BlockTime(1)), TestData.Message("Goodbye", TestData.BlockTime(2)));

            Assert.Equal("Hello Satoshi", Assert.Single(await SearchAsync("satoshi")).Message);
            Assert.Equal("Hello Satoshi", Assert.Single(await SearchAsync("LLO SAT")).Message);
        }

        // Regression: LIKE wildcards in the query used to be passed through, so "%%%" matched every message.
        [Theory]
        [InlineData("%%%", null)]
        [InlineData("___", null)]
        [InlineData("0% s", "100% sure")]
        [InlineData("e_c", "snake_case name")]      // Unescaped, "_" would also match "one cat here".
        [InlineData(@"\pa", @"C:\path\file")]      // Unescaped, "\p" would be read as "p" and match "spa day".
        public async Task WildcardsInTheQueryAreMatchedLiterally(string query, string? expected)
        {
            await StoreAsync(
                TestData.Message("100% sure", TestData.BlockTime(1)),
                TestData.Message("snake_case name", TestData.BlockTime(2)),
                TestData.Message("one cat here", TestData.BlockTime(3)),
                TestData.Message(@"C:\path\file", TestData.BlockTime(4)),
                TestData.Message("spa day", TestData.BlockTime(5)));

            var found = (await SearchAsync(query)).Select(m => m.Message);

            Assert.Equal(expected is null ? Array.Empty<string>() : new[] { expected }, found);
        }

        [Fact]
        public async Task DateFindsEveryMessageMinedOnThatUtcDayOnce()
        {
            var day = new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero);
            await StoreAsync(
                TestData.Message("Day before", day.AddSeconds(-1)),
                TestData.Message("First second", day),
                TestData.Message("Mined on 2024-03-15", day.AddHours(12)),    // Matches by date and by text.
                TestData.Message("Last second", day.AddDays(1).AddSeconds(-1)),
                TestData.Message("Day after", day.AddDays(1)));

            var found = (await SearchAsync("2024-03-15")).Select(m => m.Message);

            Assert.Equal(new[] { "Last second", "Mined on 2024-03-15", "First second" }, found);
        }

        [Fact]
        public async Task ResultsAreTheNewestMatchesUpToTheLimit()
        {
            int stored = Constants.Constants.MaxSearchResults + 5;
            await StoreAsync(Enumerable.Range(0, stored).Select(i => TestData.Message($"Note number {i}", TestData.BlockTime(i))).ToArray());

            var found = await SearchAsync("note number");

            var expected = Enumerable.Range(0, stored).Reverse().Take(Constants.Constants.MaxSearchResults).Select(i => $"Note number {i}");
            Assert.Equal(expected, found.Select(m => m.Message));
        }
    }
}
