// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// A default implementation of the Token replay cache that is backed by
    /// a bounded, expiring in-memory cache.
    /// </summary>
    internal class DefaultTokenReplayCache : TokenReplayCache
    {
        private static readonly int s_defaultTokenReplayCacheCapacity = 500000;
        private static readonly TimeSpan s_defaultTokenReplayCachePurgeInterval = TimeSpan.FromMinutes(1);

        private readonly ConcurrentDictionary<string, DateTime> _items;
        private readonly int _capacity;
        private readonly TimeSpan _purgeInterval;
        private long _nextPurgeTicks;

        /// <summary>
        /// Constructs the default token replay cache.
        /// </summary>
        public DefaultTokenReplayCache()
            : this(s_defaultTokenReplayCacheCapacity, s_defaultTokenReplayCachePurgeInterval)
        { }

        /// <summary>
        /// Constructs the default token replay cache with the specified
        /// capacity and purge interval.
        /// </summary>
        /// <param name="capacity">The capacity of the token cache.</param>
        /// <param name="purgeInterval">The time interval after which expired entries are removed.</param>
        public DefaultTokenReplayCache(int capacity, TimeSpan purgeInterval)
            : base()
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, SR.Format(SR.ID0002));
            }

            if (purgeInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(purgeInterval), purgeInterval, SR.Format(SR.ID0016));
            }

            _capacity = capacity;
            _purgeInterval = purgeInterval;
            _items = new ConcurrentDictionary<string, DateTime>(StringComparer.Ordinal);
            _nextPurgeTicks = DateTime.UtcNow.Add(purgeInterval).Ticks;
        }

        /// <summary>
        /// Returns true when the cache contains a non-expired entry for the supplied key.
        /// Expired entries are treated as absent and are removed on the next purge.
        /// </summary>
        public override bool Contains(string key)
        {
            return _items.TryGetValue(key, out DateTime expiresOn) && DateTime.UtcNow < expiresOn;
        }

        /// <summary>
        /// Removes the entry with the supplied key, if present.
        /// </summary>
        public override void Remove(string key) => _items.TryRemove(key, out _);

        public override bool TryAdd(string securityToken, DateTime expiresOn)
        {
            if (DateTime.Equals(expiresOn, DateTime.MaxValue))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperInvalidOperation(SR.Format(SR.ID1072));
            }

            PurgeIfNeeded();

            // Capacity is enforced approximately: Count is sampled before TryAdd, so
            // the live count can briefly exceed _capacity by the number of in-flight
            // concurrent inserts. This is acceptable because the bound only exists to
            // prevent unbounded growth, and the slack is bounded by request concurrency.
            if (_items.Count >= _capacity)
            {
                throw new QuotaExceededException(SR.Format(SR.ID0021, _capacity));
            }

            // ConcurrentDictionary.TryAdd is an atomic add-if-absent. It is exactly the
            // primitive the replay-cache contract requires: when this returns false the
            // caller (Microsoft.IdentityModel.Tokens.Saml) raises
            // SecurityTokenReplayDetectedException. Two concurrent inserts of the same
            // key cannot both observe "absent", so duplicate tokens are reliably rejected
            // without a TOCTOU window.
            return _items.TryAdd(securityToken, expiresOn);
        }

        public override bool TryFind(string securityToken) => Contains(securityToken);

        private void PurgeIfNeeded()
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            long nextTicks = Interlocked.Read(ref _nextPurgeTicks);
            if (nowTicks < nextTicks)
            {
                return;
            }

            long newNext = DateTime.UtcNow.Add(_purgeInterval).Ticks;
            if (Interlocked.CompareExchange(ref _nextPurgeTicks, newNext, nextTicks) != nextTicks)
            {
                // Another thread already moved the purge window forward and owns this round.
                return;
            }

            Purge();
        }

        private void Purge()
        {
            DateTime now = DateTime.UtcNow;
            List<string> expiredKeys = null;
            foreach (KeyValuePair<string, DateTime> pair in _items)
            {
                if (pair.Value <= now)
                {
                    (expiredKeys ?? (expiredKeys = new List<string>())).Add(pair.Key);
                }
            }

            if (expiredKeys == null)
            {
                return;
            }

            foreach (string key in expiredKeys)
            {
                _items.TryRemove(key, out _);
            }
        }
    }
}
