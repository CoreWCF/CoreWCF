using CoreWCF.Runtime;
using System;

namespace CoreWCF.Security
{
    public abstract class NonceCache
    {
        TimeSpan cachingTime;
        int maxCachedNonces;

        /// <summary>
        /// TThe max timespan after which a Nonce is deleted from the NonceCache. This value should be atleast twice the maxclock Skew added to the replayWindow size.
        /// </summary>
        public TimeSpan CachingTimeSpan
        {
            get
            {
                return this.cachingTime;
            }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.Format(SR.SFxTimeoutOutOfRange0)));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.SFxTimeoutOutOfRangeTooBig));
                }

                this.cachingTime = value;
            }
        }

        /// <summary>
        /// The maximum size of the NonceCache.
        /// </summary>
        public int CacheSize
        {
            get
            {
                return this.maxCachedNonces;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                                                    SR.ValueMustBeNonNegative));
                }
                this.maxCachedNonces = value;

            }
        }

        public abstract bool TryAddNonce(byte[] nonce);
        public abstract bool CheckNonce(byte[] nonce);
    }
}
