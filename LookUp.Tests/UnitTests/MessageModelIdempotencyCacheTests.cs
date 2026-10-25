using LookUp.Models;
using LookUp.Scanner.Cache;
using LookUp.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;

namespace LookUp.Tests.UnitTests
{
    public class MessageModelIdempotencyCacheTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static MessageModelIdempotencyCache CreateCache(int sizeLimit = MessageModelIdempotencyCache.MaxEntries) =>
            new(new MemoryCache(new MemoryCacheOptions { SizeLimit = sizeLimit }));

        private static List<MessageModel> Result(string text) => new() { TestData.Message(text, TestData.BlockTime(0)) };

        // A search whose database query runs until the test releases it, and which counts how often it ran.
        private sealed class ControlledSearch
        {
            private readonly TaskCompletionSource<List<MessageModel>> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _calls;

            public int Calls => _calls;

            public Task<List<MessageModel>> RunAsync(string query, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _calls);
                return _result.Task;
            }

            public void Complete(List<MessageModel> result) => _result.SetResult(result);

            public void Fail(Exception exception) => _result.SetException(exception);
        }

        [Fact]
        public async Task IdenticalSearchesInFlightShareOneQuery()
        {
            var cache = CreateCache();
            var search = new ControlledSearch();
            var expected = Result("Hello world");

            var first = cache.GetCachedResultAsync("hello", search.RunAsync);
            var second = cache.GetCachedResultAsync("hello", search.RunAsync);
            search.Complete(expected);

            Assert.Same(expected, await first.WaitAsync(Timeout));
            Assert.Same(expected, await second.WaitAsync(Timeout));
            Assert.Equal(1, search.Calls);
        }

        [Fact]
        public async Task FinishedSearchIsAnsweredFromTheCacheButOtherQueriesAreNot()
        {
            var cache = CreateCache();
            var search = new ControlledSearch();
            search.Complete(Result("Hello world"));

            await cache.GetCachedResultAsync("hello", search.RunAsync);
            await cache.GetCachedResultAsync("hello", search.RunAsync);
            Assert.Equal(1, search.Calls);

            await cache.GetCachedResultAsync("Hello", search.RunAsync);    // Keys are exact; the cache doesn't guess what is equivalent.
            Assert.Equal(2, search.Calls);
        }

        [Fact]
        public async Task FailedSearchReachesEveryWaiterAndIsNotCached()
        {
            var cache = CreateCache();
            var failing = new ControlledSearch();

            var first = cache.GetCachedResultAsync("hello", failing.RunAsync);
            var waiter = cache.GetCachedResultAsync("hello", failing.RunAsync);
            failing.Fail(new TimeoutException("database timeout"));

            await Assert.ThrowsAsync<TimeoutException>(() => first.WaitAsync(Timeout));
            await Assert.ThrowsAsync<TimeoutException>(() => waiter.WaitAsync(Timeout));

            // The next request runs the query again instead of getting the cached failure.
            var retry = new ControlledSearch();
            var expected = Result("Hello world");
            retry.Complete(expected);

            Assert.Same(expected, await cache.GetCachedResultAsync("hello", retry.RunAsync));
            Assert.Equal(1, retry.Calls);
        }

        [Fact]
        public async Task WaiterGivingUpDoesNotCancelTheSharedQuery()
        {
            var cache = CreateCache();
            var search = new ControlledSearch();
            using var waiterTimeout = new CancellationTokenSource();

            var first = cache.GetCachedResultAsync("hello", search.RunAsync);
            var waiter = cache.GetCachedResultAsync("hello", search.RunAsync, waiterTimeout.Token);
            waiterTimeout.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter.WaitAsync(Timeout));

            var expected = Result("Hello world");
            search.Complete(expected);
            Assert.Same(expected, await first.WaitAsync(Timeout));
            Assert.Same(expected, await cache.GetCachedResultAsync("hello", search.RunAsync));
            Assert.Equal(1, search.Calls);
        }

        [Fact]
        public async Task FullCacheStillAnswersNewSearches()
        {
            var cache = CreateCache(sizeLimit: 1);
            var first = new ControlledSearch();
            first.Complete(Result("First"));
            await cache.GetCachedResultAsync("first", first.RunAsync);

            var second = new ControlledSearch();
            var expected = Result("Second");
            second.Complete(expected);

            Assert.Same(expected, await cache.GetCachedResultAsync("second", second.RunAsync));
            Assert.Same(expected, await cache.GetCachedResultAsync("second", second.RunAsync));    // Not cached, so it runs again.
            Assert.Equal(2, second.Calls);
        }
    }
}
