using LookUp.Models;
using Microsoft.Extensions.Caching.Memory;


namespace LookUp.Scanner.Cache
{
    public class IdempotencyCache
    {
        private readonly IMemoryCache _memoryCache;
        private object _memoryCacheLock = new object();

        public IdempotencyCache(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        private static MemoryCacheEntryOptions IdempotencyEntryOptions { get; } = new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };

        public async Task<List<MessageModel>> GetCachedResultAsync(string request, Func<string, Task<List<MessageModel>>> action, CancellationToken cancellationToken = default)
        {
            return await GetCachedResultAsync(request, action, IdempotencyEntryOptions, cancellationToken);
        }

        private async Task<List<MessageModel>> GetCachedResultAsync(string request, Func<string, Task<List<MessageModel>>> action, MemoryCacheEntryOptions idempotencyEntryOptions, CancellationToken cancellationToken)
        {
            bool callAction = TryAddKey(request, idempotencyEntryOptions, out TaskCompletionSource<List<MessageModel>> responseTcs);

            if (callAction) 
            {
                try
                {
                    var result = await action(request);
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

                    Logger.Logger.LogCritical($"Failed to get cached result. {ex}");
                }
            }

            return await responseTcs.Task;
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
