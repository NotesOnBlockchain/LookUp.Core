using LookUp.Core.Rpc;
using LookUp.Scanner.Cache;
using LookUp.Scanner.DataBase;
using LookUp.Scanner.Services;
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
        private readonly ScanStatus _scanStatus;

        public HealthController(IRPCClient rpcClient, MessageDatabaseContext databaseContext, ScanStatus scanStatus)
        {
            _rpcClient = rpcClient;
            _dbContext = databaseContext;
            _scanStatus = scanStatus;
        }

        [HttpGet("/health")]
        public async Task<IActionResult> GetHealth()
        {
            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            StringBuilder stringBuilder = new StringBuilder();

            bool rpcResult = await CheckRpcConnectionAsync(cts.Token);
            bool dbResult = await CheckDatabaseConnectionAsync(cts.Token);

            // A stalled scanner is reported but doesn't fail the check: the website waits for this check to pass before
            // it starts, and search keeps working while scanning is stuck.
            bool healthy = rpcResult && dbResult;
            var scan = _scanStatus.Get();

            object result = new
            {
                Rpc = rpcResult ? "healthy" : "failed!",
                Database = dbResult ? "healthy" : "failed!",
                Scanner = scan.Stalled ? "stalled!" : "healthy",
                NextBlockHeight = scan.NextBlockHeight,
                LastProgressAt = scan.LastProgressAt
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
                // A round trip to the server: opening a connection alone can be served by an idle pooled one,
                // and CanConnectAsync returns false without saying why. This throws with the reason instead.
                await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
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
