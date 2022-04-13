// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Caching.Memory;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// A default implementation of the Token replay cache that is backed by
    /// a bounded cache.
    /// </summary>''''
    /// 
    internal class DefaultTokenReplayCache : TokenReplayCache
    {
        private static readonly int s_defaultTokenReplayCacheCapacity = 500000;
        private static readonly TimeSpan s_defaultTokenReplayCachePurgeInterval = TimeSpan.FromMinutes(1);
        private readonly MemoryCache _memoryCache;

        /// <summary>
        /// Constructs the default token replay cache.
        /// </summary>
        public DefaultTokenReplayCache()
            : this(DefaultTokenReplayCache.s_defaultTokenReplayCacheCapacity, DefaultTokenReplayCache.s_defaultTokenReplayCachePurgeInterval)
        { }

        /// <summary>
        /// Constructs the default token replay cache with the specified 
        /// capacity and purge interval.
        /// </summary>
        /// <param name="capacity">The capacity of the token cache</param>
        /// <param name="purgeInterval">The time interval after which the cache must be purged</param>
        public DefaultTokenReplayCache(int capacity, TimeSpan purgeInterval)
            : base()
        {
            _memoryCache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = capacity,
                ExpirationScanFrequency = purgeInterval
            });
        }

        /// <summary>
        /// Attempt to find if a matching entry exists in the cache.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <returns>true if a matching entry is ifound in the cache, false otherwise</returns>
        public override bool Contains(string key) => _memoryCache.TryGetValue(key, out string cacheEntry);

        /// <summary>
        /// Attempt to remove an entry from the cache
        /// </summary>
        /// <param name="key">The key to the entry to remove</param>
        public override void Remove(string key) => _memoryCache.Remove(key);

        public override bool TryAdd(string securityToken, DateTime expiresOn)
        {
            if (DateTime.Equals(expiresOn, DateTime.MaxValue))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperInvalidOperation(SR.Format(SR.ID1072));
            }

            if(!_memoryCache.TryGetValue(securityToken, out string cacheEntry))
            {
                _memoryCache.Set(securityToken, securityToken,
                    new MemoryCacheEntryOptions().SetSize(1) //we are dealing with capacity
                    .SetAbsoluteExpiration(expiresOn));
            }

            return true;
        }

        public override bool TryFind(string securityToken) => Contains(securityToken);
    }
}
