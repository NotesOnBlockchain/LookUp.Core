using LookUp.Scanner.Cache;
using LookUp.Scanner.DataBase;
using Microsoft.AspNetCore.Mvc;
using NBitcoin.Protocol;

namespace LookUp.Scanner.Controllers
{
    public class SearchController : Controller
    {
        public SearchController(MessageRepository messageRepository, MessageModelIdempotencyCache idempotencyCache)
        {
            MessageRepository = messageRepository;
            IdempotencyCache = idempotencyCache;
        }

        private MessageRepository MessageRepository { get; }
        private MessageModelIdempotencyCache IdempotencyCache { get; }

        [HttpGet("/search")]
        public async Task<IActionResult> SearchAsync([FromQuery] string query)
        {
            if (query.Length > 200)
                return Ok(Enumerable.Empty<Message>());

            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));

            var searchResult = await IdempotencyCache.GetCachedResultAsync(query,
                MessageRepository.FindAsync,
                cts.Token);

            return Ok(searchResult);
        }
    }
}
