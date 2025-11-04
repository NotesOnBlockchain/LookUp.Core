using LookUp.Core.DataBase;
using LookUp.Core.Rpc;
using Microsoft.EntityFrameworkCore;
using NBitcoin;
using System.Security.Authentication;

namespace LookUp.Core
{
    public class StartupTask
    {
        public StartupTask(IRPCClient client, IServiceScopeFactory scopeFactory)
        {
            RpcClient = client;
            ScopeFactory = scopeFactory;
        }
        private IRPCClient RpcClient { get; }
        public IServiceScopeFactory ScopeFactory { get; }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // Make sure we are connected to Knots
            await CheckRpcConnectionAsync(cancellationToken);

            // Make sure we are connected to the Database
            await CheckDatabaseConnectionAsync(cancellationToken);
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

                if (!await databaseContext.Database.CanConnectAsync(cancellationToken))
                {
                    throw new AuthenticationException();
                }

                databaseContext.Database.OpenConnection();
                databaseContext.Database.CloseConnection();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Couldn't connect to database: {ex.ToString()}");
                throw;
            }
        }
    }
}
