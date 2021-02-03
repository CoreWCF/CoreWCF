// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    // This is the in-memory cache used for caching SCTs
    internal sealed class SecurityContextTokenCache : TimeBoundedCache
    {
        // if there are less than lowWaterMark entries, no purging is done
        private const int LowWaterMark = 50;

        // frequency of purging the cache of stale entries
        // this is set to 10 mins as SCTs are expected to have long lifetimes
        private static TimeSpan s_purgingInterval = TimeSpan.FromMinutes(10);
        private const double PruningFactor = 0.20;
        private readonly bool _replaceOldestEntries = true;
        private static readonly SctEffectiveTimeComparer s_sctEffectiveTimeComparer = new SctEffectiveTimeComparer();
        private TimeSpan _clockSkew;

        public SecurityContextTokenCache(int capacity, bool replaceOldestEntries)
            : this(capacity, replaceOldestEntries, SecurityProtocolFactory.defaultMaxClockSkew)
        {
        }

        public SecurityContextTokenCache(int capacity, bool replaceOldestEntries, TimeSpan clockSkew)
            : base(LowWaterMark, capacity, null, PurgingMode.TimerBasedPurge, s_purgingInterval, true)

        {
            _replaceOldestEntries = replaceOldestEntries;
            _clockSkew = clockSkew;
        }

        public void AddContext(SecurityContextSecurityToken token)
        {
            TryAddContext(token, true);
        }

        public bool TryAddContext(SecurityContextSecurityToken token)
        {
            return TryAddContext(token, false);
        }

        private bool TryAddContext(SecurityContextSecurityToken token, bool throwOnFailure)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }

            if (!SecurityUtils.IsCurrentlyTimeEffective(token.ValidFrom, token.ValidTo, _clockSkew))
            {
                if (token.KeyGeneration == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SecurityContextExpiredNoKeyGeneration, token.ContextId));
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SecurityContextExpired, token.ContextId, token.KeyGeneration.ToString()));
                }
            }

            if (!SecurityUtils.IsCurrentlyTimeEffective(token.KeyEffectiveTime, token.KeyExpirationTime, _clockSkew))
            {
                if (token.KeyGeneration == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SecurityContextKeyExpiredNoKeyGeneration, token.ContextId));
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SecurityContextKeyExpired, token.ContextId, token.KeyGeneration.ToString()));
                }
            }

            object hashKey = GetHashKey(token.ContextId, token.KeyGeneration);
            bool wasTokenAdded = TryAddItem(hashKey, (SecurityContextSecurityToken)token.Clone(), false);
            if (!wasTokenAdded)
            {
                if (throwOnFailure)
                {
                    if (token.KeyGeneration == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ContextAlreadyRegisteredNoKeyGeneration, token.ContextId)));
                    }
                    else
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ContextAlreadyRegistered, token.ContextId, token.KeyGeneration.ToString())));
                    }
                }
            }
            return wasTokenAdded;
        }

        private object GetHashKey(UniqueId contextId, UniqueId generation)
        {
            if (generation == null)
            {
                return contextId;
            }
            else
            {
                return new ContextAndGenerationKey(contextId, generation);
            }
        }

        public void ClearContexts()
        {
            ClearItems();
        }

        public SecurityContextSecurityToken GetContext(UniqueId contextId, UniqueId generation)
        {
            if (contextId == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contextId));
            }
            object hashKey = GetHashKey(contextId, generation);
            SecurityContextSecurityToken sct = (SecurityContextSecurityToken)GetItem(hashKey);
            return sct != null ? (SecurityContextSecurityToken)sct.Clone() : null;
        }

        public void RemoveContext(UniqueId contextId, UniqueId generation, bool throwIfNotPresent)
        {
            if (contextId == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contextId));
            }
            object hashKey = GetHashKey(contextId, generation);
            if (!TryRemoveItem(hashKey) && throwIfNotPresent)
            {
                if (generation == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ContextNotPresentNoKeyGeneration, contextId)));
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ContextNotPresent, contextId, generation.ToString())));
                }
            }
        }

        private ArrayList GetMatchingKeys(UniqueId contextId)
        {
            if (contextId == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contextId));
            }
            ArrayList matchingKeys = new ArrayList(2);

            bool lockHeld = false;
            try
            {
                try { }
                finally
                {
                    CacheLock.AcquireReaderLock(-1);
                    lockHeld = true;
                }
                foreach (object key in Entries.Keys)
                {
                    bool isMatch;
                    if (key is UniqueId)
                    {
                        isMatch = (((UniqueId)key) == contextId);
                    }
                    else
                    {
                        isMatch = (((ContextAndGenerationKey)key).ContextId == contextId);
                    }
                    if (isMatch)
                    {
                        matchingKeys.Add(key);
                    }
                }
            }
            finally
            {
                if (lockHeld)
                {
                    CacheLock.ReleaseReaderLock();
                }
            }
            return matchingKeys;
        }

        public void RemoveAllContexts(UniqueId contextId)
        {
            ArrayList matchingKeys = GetMatchingKeys(contextId);
            for (int i = 0; i < matchingKeys.Count; ++i)
            {
                TryRemoveItem(matchingKeys[i]);
            }
        }

        public void UpdateContextCachingTime(SecurityContextSecurityToken token, DateTime expirationTime)
        {
            if (token.ValidTo <= expirationTime.ToUniversalTime())
            {
                return;
            }
            TryReplaceItem(GetHashKey(token.ContextId, token.KeyGeneration), token, expirationTime);
        }

        public Collection<SecurityContextSecurityToken> GetAllContexts(UniqueId contextId)
        {
            ArrayList matchingKeys = GetMatchingKeys(contextId);

            Collection<SecurityContextSecurityToken> matchingContexts = new Collection<SecurityContextSecurityToken>();
            for (int i = 0; i < matchingKeys.Count; ++i)
            {
                if (GetItem(matchingKeys[i]) is SecurityContextSecurityToken token)
                {
                    matchingContexts.Add(token);
                }
            }
            return matchingContexts;
        }

        protected override ArrayList OnQuotaReached(Hashtable cacheTable)
        {
            if (!_replaceOldestEntries)
            {
                //SecurityTraceRecordHelper.TraceSecurityContextTokenCacheFull(this.Capacity, 0);
                return base.OnQuotaReached(cacheTable);
            }
            else
            {
                List<SecurityContextSecurityToken> tokens = new List<SecurityContextSecurityToken>(cacheTable.Count);
                foreach (IExpirableItem value in cacheTable.Values)
                {
                    SecurityContextSecurityToken token = (SecurityContextSecurityToken)ExtractItem(value);
                    tokens.Add(token);
                }
                tokens.Sort(s_sctEffectiveTimeComparer);
                int pruningAmount = (int)(((double)Capacity) * PruningFactor);
                pruningAmount = pruningAmount <= 0 ? Capacity : pruningAmount;
                ArrayList keys = new ArrayList(pruningAmount);
                for (int i = 0; i < pruningAmount; ++i)
                {
                    keys.Add(GetHashKey(tokens[i].ContextId, tokens[i].KeyGeneration));
                    OnRemove(tokens[i]);
                }
                //  SecurityTraceRecordHelper.TraceSecurityContextTokenCacheFull(this.Capacity, pruningAmount);
                return keys;
            }
        }

        private sealed class SctEffectiveTimeComparer : IComparer<SecurityContextSecurityToken>
        {
            public int Compare(SecurityContextSecurityToken sct1, SecurityContextSecurityToken sct2)
            {
                if (sct1 == sct2)
                {
                    return 0;
                }
                if (sct1.ValidFrom.ToUniversalTime() < sct2.ValidFrom.ToUniversalTime())
                {
                    return -1;
                }
                else if (sct1.ValidFrom.ToUniversalTime() > sct2.ValidFrom.ToUniversalTime())
                {
                    return 1;
                }
                else
                {
                    // compare the key effective times
                    if (sct1.KeyEffectiveTime.ToUniversalTime() < sct2.KeyEffectiveTime.ToUniversalTime())
                    {
                        return -1;
                    }
                    else if (sct1.KeyEffectiveTime.ToUniversalTime() > sct2.KeyEffectiveTime.ToUniversalTime())
                    {
                        return 1;
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
        }

        protected override void OnRemove(object item)
        {
            ((IDisposable)item).Dispose();
            base.OnRemove(item);
        }

        private struct ContextAndGenerationKey
        {
            public ContextAndGenerationKey(UniqueId contextId, UniqueId generation)
            {
                Fx.Assert(contextId != null && generation != null, "");
                ContextId = contextId;
                Generation = generation;
            }

            public UniqueId ContextId { get; }

            public UniqueId Generation { get; }

            public override int GetHashCode()
            {
                return ContextId.GetHashCode() ^ Generation.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj is ContextAndGenerationKey key2)
                {
                    return (key2.ContextId == ContextId && key2.Generation == Generation);
                }
                else
                {
                    return false;
                }
            }

            public static bool operator ==(ContextAndGenerationKey a, ContextAndGenerationKey b)
            {
                if (ReferenceEquals(a, null))
                {
                    return ReferenceEquals(b, null);
                }

                return (a.Equals(b));
            }

            public static bool operator !=(ContextAndGenerationKey a, ContextAndGenerationKey b)
            {
                return !(a == b);
            }
        }
    }
}
