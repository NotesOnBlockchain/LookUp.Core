using LookUp.Core.Rpc;
using LookUp.Scanner.DataBase;
using LookUp.Serialization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace LookUp.Scanner.Controllers
{
    [Route("/test")]
    public class TestController : Controller
    {
        public TestController(IRPCClient rpcClient, MessageRepository messageRepository)
        {
            RpcClient = rpcClient;
            MessageRepo = messageRepository;
        }

        public IRPCClient RpcClient { get; }
        public MessageRepository MessageRepo { get; }

        [HttpGet]
        public async Task<IActionResult> TestAsync()
        {
            var messages = MessageRepo.GetMessages();
            Console.WriteLine($"Messages Count: {messages.Count}");

            StringBuilder stringBuilder = new();

            foreach (var message in messages) 
            {
                stringBuilder.AppendLine(Encode.Message(message).ToString());
            }

            return Ok($"{stringBuilder.ToString()}");
        }
    }
}
