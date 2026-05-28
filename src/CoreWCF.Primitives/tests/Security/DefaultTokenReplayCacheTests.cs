// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.IdentityModel.Tokens;
using Xunit;

namespace CoreWCF.Http.Tests.Security
{
    public class DefaultTokenReplayCacheTests
    {
        private static readonly Type s_cacheType = typeof(TokenReplayCache).Assembly
            .GetType("CoreWCF.IdentityModel.Tokens.DefaultTokenReplayCache", throwOnError: true);

        private static TokenReplayCache CreateCache(int capacity = 16, TimeSpan? purgeInterval = null)
        {
            TimeSpan interval = purgeInterval ?? TimeSpan.FromMinutes(1);
            try
            {
                return (TokenReplayCache)Activator.CreateInstance(
                    s_cacheType,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    args: new object[] { capacity, interval },
                    culture: null);
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                throw tie.InnerException;
            }
        }

        [Fact]
        public void TryAdd_FirstInsert_ReturnsTrue()
        {
            TokenReplayCache cache = CreateCache();
            Assert.True(cache.TryAdd("token-1", DateTime.UtcNow.AddMinutes(5)));
        }

        [Fact]
        public void TryAdd_DuplicateInsert_ReturnsFalse()
        {
            TokenReplayCache cache = CreateCache();
            DateTime expiry = DateTime.UtcNow.AddMinutes(5);
            Assert.True(cache.TryAdd("token-1", expiry));
            Assert.False(cache.TryAdd("token-1", expiry));
        }

        [Fact]
        public void TryAdd_InfiniteExpiry_Throws()
        {
            TokenReplayCache cache = CreateCache();
            Assert.Throws<InvalidOperationException>(() => cache.TryAdd("token-1", DateTime.MaxValue));
        }

        [Fact]
        public void Contains_ExpiredEntry_ReturnsFalse()
        {
            TokenReplayCache cache = CreateCache();
            cache.TryAdd("token-1", DateTime.UtcNow.AddMilliseconds(50));
            Thread.Sleep(150);
            Assert.False(cache.Contains("token-1"));
            Assert.False(cache.TryFind("token-1"));
        }

        [Fact]
        public void Remove_RemovesEntry()
        {
            TokenReplayCache cache = CreateCache();
            cache.TryAdd("token-1", DateTime.UtcNow.AddMinutes(5));
            cache.Remove("token-1");
            Assert.False(cache.Contains("token-1"));
            Assert.True(cache.TryAdd("token-1", DateTime.UtcNow.AddMinutes(5)));
        }

        [Fact]
        public void TryAdd_CapacityReached_Throws()
        {
            TokenReplayCache cache = CreateCache(capacity: 4);
            DateTime expiry = DateTime.UtcNow.AddMinutes(5);
            for (int i = 0; i < 4; i++)
            {
                Assert.True(cache.TryAdd($"token-{i}", expiry));
            }

            Assert.Throws<QuotaExceededException>(() => cache.TryAdd("token-overflow", expiry));
        }

        [Fact]
        public async Task TryAdd_ConcurrentDuplicateInserts_OnlyOneSucceeds()
        {
            TokenReplayCache cache = CreateCache(capacity: 1024);
            DateTime expiry = DateTime.UtcNow.AddMinutes(5);
            const int parallelism = 64;
            const int distinctKeys = 256;

            int totalAccepted = 0;
            ConcurrentDictionary<string, int> acceptedPerKey = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

            using (Barrier barrier = new Barrier(parallelism))
            {
                Task[] tasks = Enumerable.Range(0, parallelism).Select(_ => Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    for (int k = 0; k < distinctKeys; k++)
                    {
                        string key = $"token-{k}";
                        if (cache.TryAdd(key, expiry))
                        {
                            Interlocked.Increment(ref totalAccepted);
                            acceptedPerKey.AddOrUpdate(key, 1, (_, v) => v + 1);
                        }
                    }
                })).ToArray();

                await Task.WhenAll(tasks);
            }

            Assert.Equal(distinctKeys, totalAccepted);
            Assert.Equal(distinctKeys, acceptedPerKey.Count);
            Assert.All(acceptedPerKey.Values, count => Assert.Equal(1, count));
        }

        [Fact]
        public void Ctor_InvalidCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateCache(capacity: 0));
        }

        [Fact]
        public void Ctor_InvalidPurgeInterval_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateCache(purgeInterval: TimeSpan.Zero));
        }
    }
}
