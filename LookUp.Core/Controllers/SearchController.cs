using LookUp.Scanner.DataBase;
using Microsoft.AspNetCore.Mvc;

namespace LookUp.Scanner.Controllers
{
    public class SearchController : Controller
    {
        public SearchController(MessageRepository messageRepository)
        {
            MessageRepository = messageRepository;
        }

        private MessageRepository MessageRepository { get; }

        [HttpGet("search")]
        public async Task<IActionResult> SearchAsync([FromQuery] string searchContent)
        {
           var searchResult = await MessageRepository.FindAsync(searchContent);

            if (searchResult.Count == 0 || searchResult is null) 
            {
                Ok("No message found");
            }

            return Ok(searchResult);
        }
    }
}
