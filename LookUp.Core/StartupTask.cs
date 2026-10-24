using LookUp.Core.Rpc;
using LookUp.Scanner.DataBase;
using LookUp.Serialization;
using Microsoft.EntityFrameworkCore;
using NBitcoin;
using System.Security.Authentication;

namespace LookUp.Scanner
{
    public class StartupTask
    {
        private const string LegacyLastScannedBlockHeightFileName = "LastScannedBlockHeight.txt";

        public StartupTask(IRPCClient client, IServiceScopeFactory scopeFactory, Config.Config config)
        {
            RpcClient = client;
            ScopeFactory = scopeFactory;
            DataDir = Path.GetDirectoryName(config.FilePath) ?? throw new InvalidOperationException($"Couldn't get the data directory of '{config.FilePath}'.");
        }
        private IRPCClient RpcClient { get; }
        public IServiceScopeFactory ScopeFactory { get; }
        private string DataDir { get; }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // Make sure we are connected to Knots
            await CheckRpcConnectionAsync(cancellationToken);

            // Make sure we are connected to the Database
            await CheckDatabaseConnectionAsync(cancellationToken);

            // Make sure the Scanner knows where to continue from
            await InitializeScanProgressAsync(cancellationToken);
        }

        private async Task CheckRpcConnectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                var blockchainInfo = await RpcClient.GetBlockchainInfoAsync(cancellationToken);

                var blocks = blockchainInfo.Blocks;
                if (blocks == 0 && RpcClient.Network != Network.RegTest)
                {
                    throw new NotSupportedException($"{nameof(blocks)} == 0");
                }

                var headers = blockchainInfo.Headers;
                if (headers == 0 && RpcClient.Network != Network.RegTest)
                {
                    throw new NotSupportedException($"{nameof(headers)} == 0");
                }

                if (blocks != headers)
                {
                    throw new NotSupportedException("Bitcoin Node is not fully synchronized.");
                }
            }
            catch (Exception ex) 
            {
                Console.WriteLine(ex.ToString());
                throw;
            }
        }

        private async Task CheckDatabaseConnectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = ScopeFactory.CreateScope();
                MessageDatabaseContext databaseContext = scope.ServiceProvider.GetService<MessageDatabaseContext>() ?? throw new Exception($"Couldn't get {typeof(MessageDatabaseContext)}");

                await databaseContext.Database.CanConnectAsync(cancellationToken);

                databaseContext.Database.OpenConnection();
                databaseContext.Database.CloseConnection();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Couldn't connect to database: {ex.ToString()}");
                throw;
            }
        }

        // Scan progress used to be kept in a file in the data directory. The first start with a database-backed
        // progress copies it over once; from then on the database is the only source and the file is ignored.
        private async Task InitializeScanProgressAsync(CancellationToken cancellationToken)
        {
            using var scope = ScopeFactory.CreateScope();
            var messageRepo = scope.ServiceProvider.GetRequiredService<MessageRepository>();

            if (await messageRepo.GetNextBlockHeightAsync(cancellationToken) is not null)
            {
                return;
            }

            int nextBlockHeight = ReadLegacyLastScannedBlockHeight(Path.Combine(DataDir, LegacyLastScannedBlockHeightFileName));
            await messageRepo.CreateScanProgressAsync(nextBlockHeight, cancellationToken);

            Logger.Logger.LogInfo($"Scan progress created in the database. Scanning continues from Block Height {nextBlockHeight}.");
        }

        private static int ReadLegacyLastScannedBlockHeight(string filePath)
        {
            // No file means nothing was ever scanned. An unreadable file fails startup instead of
            // silently restarting the scan from the genesis block.
            if (!File.Exists(filePath))
            {
                return 0;
            }

            using var lastScannedBlockFile = File.OpenRead(filePath);
            var decoder = JsonDecoder.FromStream(Decode.Int64);

            long lastScannedBlockHeight = decoder(lastScannedBlockFile).Match(
                value => value,
                error => throw new InvalidOperationException($"Could not read '{filePath}': {error} Fix the file or delete it to scan from the genesis block."));

            return checked((int)lastScannedBlockHeight);
        }
    }
}
