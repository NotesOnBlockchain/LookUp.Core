using LookUp.Models;
using LookUp.Scanner.DataBase;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using NBitcoin.Protocol;

namespace LookUp.Scanner.Controllers
{
    public class SearchController : Controller
    {
        public SearchController(MessageRepository messageRepository, IMemoryCache cache)
        {
            MessageRepository = messageRepository;
            Cache = cache;
        }

        private MessageRepository MessageRepository { get; }
        private IMemoryCache Cache { get; }

        [HttpGet("/search")]
        public async Task<IActionResult> SearchAsync([FromQuery] string query)
        {
            if (query.Length > 250)
                return Ok(Enumerable.Empty<Message>());

            var cacheKey = $"{query.Trim().ToLower()}";

            if (Cache.TryGetValue(cacheKey, out List<MessageModel>? cachedResult)) 
            {
                return Ok(cachedResult);
            }

            var searchResult = await MessageRepository.FindAsync(query);

            Cache.Set(cacheKey, searchResult, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
            });

            return Ok(searchResult);
        }
    }
}
