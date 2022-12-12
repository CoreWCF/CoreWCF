// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Runtime;

namespace CoreWCF.Security
{
    public abstract class NonceCache
    {
        private TimeSpan _cachingTime;
        private int _maxCachedNonces;

        /// <summary>
        /// TThe max timespan after which a Nonce is deleted from the NonceCache. This value should be atleast twice the maxclock Skew added to the replayWindow size.
        /// </summary>
        public TimeSpan CachingTimeSpan
        {
            get
            {
                return _cachingTime;
            }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.Format(SRCommon.SFxTimeoutOutOfRange0)));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SRCommon.SFxTimeoutOutOfRangeTooBig));
                }

                _cachingTime = value;
            }
        }

        /// <summary>
        /// The maximum size of the NonceCache.
        /// </summary>
        public int CacheSize
        {
            get
            {
                return _maxCachedNonces;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                                                    SRCommon.ValueMustBeNonNegative));
                }
                _maxCachedNonces = value;
            }
        }

        public abstract bool TryAddNonce(byte[] nonce);
        public abstract bool CheckNonce(byte[] nonce);
    }
}
