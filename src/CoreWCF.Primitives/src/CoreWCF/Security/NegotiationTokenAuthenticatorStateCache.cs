using System;
using System.Collections;
using CoreWCF.Runtime;
using CoreWCF;
using CoreWCF.Diagnostics;

namespace CoreWCF.Security
{
    sealed class NegotiationTokenAuthenticatorStateCache<T> : TimeBoundedCache
        where T : NegotiationTokenAuthenticatorState
    {
        static int lowWaterMark = 50;
        static TimeSpan purgingInterval = TimeSpan.FromMinutes(10);
        TimeSpan cachingSpan;

        public NegotiationTokenAuthenticatorStateCache(TimeSpan cachingSpan, int maximumCachedState)
            : base(lowWaterMark, maximumCachedState, null, PurgingMode.TimerBasedPurge, TimeSpan.FromTicks(cachingSpan.Ticks >> 2), true)
        {
            this.cachingSpan = cachingSpan;
        }

        public void AddState(string context, T state)
        {
            DateTime expirationTime = TimeoutHelper.Add(DateTime.UtcNow, this.cachingSpan);
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

        public T GetState(string context)
        {
            return (this.GetItem(context) as T);
        }

        public void RemoveState(string context)
        {
            this.TryRemoveItem(context);
            //if (TD.NegotiateTokenAuthenticatorStateCacheRatioIsEnabled())
            //{
            //    TD.NegotiateTokenAuthenticatorStateCacheRatio(base.Count, base.Capacity);
            //}
        }


        protected override ArrayList OnQuotaReached(Hashtable cacheTable)
        {
            //if (TD.NegotiateTokenAuthenticatorStateCacheExceededIsEnabled())
            //{
            //    TD.NegotiateTokenAuthenticatorStateCacheExceeded(SR.GetString(SR.CachedNegotiationStateQuotaReached, this.Capacity));
            //}
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new QuotaExceededException(SR.Format(SR.CachedNegotiationStateQuotaReached, this.Capacity)));
        }

        protected override void OnRemove(object item)
        {
            ((IDisposable)item).Dispose();
            base.OnRemove(item);
        }
    }
}
