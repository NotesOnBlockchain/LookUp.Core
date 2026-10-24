using LookUp.Models;
using Microsoft.Extensions.Caching.Memory;


namespace LookUp.Scanner.Cache
{
    public class MessageModelIdempotencyCache
    {
        // Distinct searches the cache keeps at once. Each entry holds at most MaxSearchResults messages, a few tens of KB.
        // When it is full, new searches still run but aren't cached until older entries expire or are compacted.
        public const int MaxEntries = 1000;

        private readonly IMemoryCache _memoryCache;
        private object _memoryCacheLock = new object();

        public MessageModelIdempotencyCache(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        private static MemoryCacheEntryOptions IdempotencyEntryOptions { get; } = new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
            Size = 1
        };

        public async Task<List<MessageModel>> GetCachedResultAsync(string request, Func<string, CancellationToken, Task<List<MessageModel>>> action, CancellationToken cancellationToken = default)
        {
            return await GetCachedResultAsync(request, action, IdempotencyEntryOptions, cancellationToken);
        }

        private async Task<List<MessageModel>> GetCachedResultAsync(string request, Func<string, CancellationToken, Task<List<MessageModel>>> action, MemoryCacheEntryOptions idempotencyEntryOptions, CancellationToken cancellationToken)
        {
            bool callAction = TryAddKey(request, idempotencyEntryOptions, out TaskCompletionSource<List<MessageModel>> responseTcs);

            if (callAction) 
            {
                try
                {
                    // The query is shared with every identical request that arrives while it runs, so it stops on
                    // this request's timeout only; a visitor leaving must not cancel it for the others.
                    var result = await action(request, cancellationToken);
                    responseTcs.SetResult(result);
                    return result;
                }
                catch (Exception ex) 
                {
                    lock (_memoryCacheLock)
                    {
                        _memoryCache.Remove(request);
                    }

                    responseTcs!.SetException(ex);

                    Logger.Logger.LogCritical($"Failed to run action. TastCompSource set to expection. {ex}");
                }
            }

            // Requests waiting on another request's query still give up on their own timeout.
            return await responseTcs.Task.WaitAsync(cancellationToken);
        }

        private bool TryAddKey(string cacheKey, MemoryCacheEntryOptions options, out TaskCompletionSource<List<MessageModel>> responseTcs)
        {
            lock (_memoryCacheLock) 
            {
                if (!_memoryCache.TryGetValue(cacheKey, out TaskCompletionSource<List<MessageModel>>? tcs)) 
                {
                    // Didn't find cache entry, new entry added, action needs to be called to save the result in 'responseTcs'.

                    responseTcs = new();
                    _memoryCache.Set(cacheKey, responseTcs, options);

                    return true;
                }

                responseTcs = tcs!;
                return false;
            }
        }
    }
}
