using LookUp.Core.Rpc;
using LookUp.Scanner.Cache;
using LookUp.Scanner.DataBase;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NBitcoin;
using System.Text;

namespace LookUp.Scanner.Controllers
{
    public class HealthController : Controller
    {
        private readonly IRPCClient _rpcClient;
        private readonly MessageDatabaseContext _dbContext;

        public HealthController(IRPCClient rpcClient, MessageDatabaseContext databaseContext)
        {
            _rpcClient = rpcClient;
            _dbContext = databaseContext;
        }

        [HttpGet("/health")]
        public async Task<IActionResult> GetHealth()
        {
            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            StringBuilder stringBuilder = new StringBuilder();

            bool rpcResult = await CheckRpcConnectionAsync(cts.Token);
            bool dbResult = await CheckDatabaseConnectionAsync(cts.Token);

            bool healthy = rpcResult && dbResult;

            object result = new
            {
                Rpc = rpcResult ? "healthy" : "failed!",
                Database = dbResult ? "healthy" : "failed!"
            };

            return healthy ? Ok(result) : StatusCode(503, result);
        }

        private async Task<bool> CheckRpcConnectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                var blockchainInfo = await _rpcClient.GetBlockchainInfoAsync(cancellationToken);

                var blocks = blockchainInfo.Blocks;
                if (blocks == 0 && _rpcClient.Network != Network.RegTest)
                {
                    throw new NotSupportedException($"{nameof(blocks)} == 0");
                }

                var headers = blockchainInfo.Headers;
                if (headers == 0 && _rpcClient.Network != Network.RegTest)
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
                Logger.Logger.LogWarning($"Health check failed! {ex}");
                return false;
            }

            return true;
        }

        private async Task<bool> CheckDatabaseConnectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _dbContext.Database.CanConnectAsync(cancellationToken);

                _dbContext.Database.OpenConnection();
                _dbContext.Database.CloseConnection();
            }
            catch (Exception ex)
            {
                Logger.Logger.LogWarning($"Health check failed! {ex}");
                return false;
            }

            return true;
        }
    }
}
