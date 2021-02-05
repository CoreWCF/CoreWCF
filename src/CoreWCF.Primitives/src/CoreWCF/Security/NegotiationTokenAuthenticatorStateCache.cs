// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using CoreWCF.Runtime;

namespace CoreWCF.Security
{
    internal sealed class NegotiationTokenAuthenticatorStateCache<T> : TimeBoundedCache
        where T : NegotiationTokenAuthenticatorState
    {
        private static readonly int s_lowWaterMark = 50;
        private TimeSpan _cachingSpan;

        public NegotiationTokenAuthenticatorStateCache(TimeSpan cachingSpan, int maximumCachedState)
            : base(s_lowWaterMark, maximumCachedState, null, PurgingMode.TimerBasedPurge, TimeSpan.FromTicks(cachingSpan.Ticks >> 2), true) => _cachingSpan = cachingSpan;

        public void AddState(string context, T state)
        {
            DateTime expirationTime = TimeoutHelper.Add(DateTime.UtcNow, _cachingSpan);
            bool wasStateAdded = base.TryAddItem(context, state, expirationTime, false);
            if (!wasStateAdded)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.Format(SR.NegotiationStateAlreadyPresent, context)));
            }
            //if (TD.NegotiateTokenAuthenticatorStateCacheRatioIsEnabled())
            //{
            //    TD.NegotiateTokenAuthenticatorStateCacheRatio(base.Count, base.Capacity);
            //}
        }

        public T GetState(string context) => (GetItem(context) as T);

        public void RemoveState(string context) => TryRemoveItem(context);//if (TD.NegotiateTokenAuthenticatorStateCacheRatioIsEnabled())//{//    TD.NegotiateTokenAuthenticatorStateCacheRatio(base.Count, base.Capacity);//}


        protected override ArrayList OnQuotaReached(Hashtable cacheTable) =>
            //if (TD.NegotiateTokenAuthenticatorStateCacheExceededIsEnabled())
            //{
            //    TD.NegotiateTokenAuthenticatorStateCacheExceeded(SR.GetString(SR.CachedNegotiationStateQuotaReached, this.Capacity));
            //}
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new QuotaExceededException(SR.Format(SR.CachedNegotiationStateQuotaReached, Capacity)));

        protected override void OnRemove(object item)
        {
            ((IDisposable)item).Dispose();
            base.OnRemove(item);
        }
    }
}
