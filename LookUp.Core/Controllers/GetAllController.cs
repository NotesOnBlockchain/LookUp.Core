using LookUp.Scanner.DataBase;
using LookUp.Serialization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace LookUp.Scanner.Controllers
{
    [Route("/getmessages")]
    public class GetAllController : Controller
    {
        public GetAllController(MessageRepository messageRepository)
        {
            MessageRepo = messageRepository;
        }
        public MessageRepository MessageRepo { get; }

        [HttpGet]
        public async Task<IActionResult> GetAsync()
        {
            var messages = MessageRepo.GetMessages();

            StringBuilder stringBuilder = new();

            foreach (var message in messages)
            {
                stringBuilder.AppendLine(Encode.Message(message).ToString());
            }

            return Ok($"{stringBuilder.ToString()}");
        }
    }
}
