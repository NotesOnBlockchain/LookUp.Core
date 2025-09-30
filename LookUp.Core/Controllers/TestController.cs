using LookUp.Core.Rpc;
using LookUp.Core.Serialization;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;

namespace LookUp.Core.Controllers
{
    [Route("/test")]
    public class TestController : Controller
    {
        public TestController(IRPCClient rpcClient)
        {
            RpcClient = rpcClient;
        }

        public IRPCClient RpcClient { get; }

        [HttpGet]
        public async Task<IActionResult> TestAsync()
        {
            uint256 bestBlockHash = await RpcClient.GetBestBlockHashAsync().ConfigureAwait(false);

            return Ok(JsonEncoder.ToString(bestBlockHash, Encode.UInt256));
        }
    }
}
