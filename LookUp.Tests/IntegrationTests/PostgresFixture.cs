using LookUp.Scanner.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace LookUp.Tests.IntegrationTests
{
    // One throwaway PostgreSQL server for every integration test, with the Scanner's migrations applied.
    // Requires Docker. Run only the unit tests with: dotnet test --filter Category!=Integration
    public sealed class PostgresFixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

        public string ConnectionString => _container.GetConnectionString();

        // Resolves MessageRepository the way the Scanner does: one DbContext per scope.
        public ServiceProvider Services { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await _container.StartAsync();

            Services = new ServiceCollection()
                .AddDbContext<MessageDatabaseContext>(options => options.UseNpgsql(ConnectionString))
                .AddScoped<MessageRepository>()
                .BuildServiceProvider();

            await using var db = CreateContext();
            await db.Database.MigrateAsync();
        }

        public MessageDatabaseContext CreateContext() =>
            new(new DbContextOptionsBuilder<MessageDatabaseContext>().UseNpgsql(ConnectionString).Options);

        // Tests in the collection run one at a time; each starts from empty tables.
        public async Task ResetAsync()
        {
            await using var db = CreateContext();
            await db.Database.ExecuteSqlRawAsync("""TRUNCATE "Messages", "ScanProgress";""");
        }

        public async Task DisposeAsync()
        {
            await Services.DisposeAsync();
            await _container.DisposeAsync();
        }
    }

    [CollectionDefinition(Name)]
    public class PostgresCollection : ICollectionFixture<PostgresFixture>
    {
        public const string Name = "Postgres";
    }
}
