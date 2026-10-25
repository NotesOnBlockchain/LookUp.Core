using Microsoft.EntityFrameworkCore;

namespace LookUp.Tests.IntegrationTests
{
    [Collection(PostgresCollection.Name)]
    [Trait("Category", "Integration")]
    public class DatabaseSchemaTests
    {
        private readonly PostgresFixture _db;

        public DatabaseSchemaTests(PostgresFixture db) => _db = db;

        // The fixture applies every migration to an empty database, so a migration that fails on PostgreSQL fails the run.
        // This catches the other half: a model change that was never turned into a migration.
        [Fact]
        public async Task MigrationsMatchTheModel()
        {
            await using var db = _db.CreateContext();

            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges(), "The model has changes that no migration covers. Add a migration.");
        }
    }
}
