// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using CoreWCF.Runtime;

namespace CoreWCF.Security
{
    /// <summary>
    /// This is the in-memory nonce-cache used for turnkey replay detection.
    /// The nonce cache is based on a hashtable implementation for fast lookups.
    /// The hashcode is computed based on the nonce byte array.
    /// The nonce cache periodically purges stale nonce entries.
    /// </summary>
    internal sealed class InMemoryNonceCache : NonceCache
    {
        private readonly NonceCacheImpl _cacheImpl;

        public InMemoryNonceCache(TimeSpan cachingTime, int maxCachedNonces)
        {
            CacheSize = maxCachedNonces;
            CachingTimeSpan = cachingTime;
            _cacheImpl = new NonceCacheImpl(cachingTime, maxCachedNonces);
        }

        public override bool CheckNonce(byte[] nonce)
        {
            return _cacheImpl.CheckNonce(nonce);
        }

        public override bool TryAddNonce(byte[] nonce)
        {
            return _cacheImpl.TryAddNonce(nonce);
        }

        public override string ToString()
        {
            // TODO: Switch to raw string literal text once we're on C# 11. https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/raw-string
            string s =
$@"NonceCache:
   Caching Timespan: {CachingTimeSpan}
   Capacity: {CacheSize}";
            return s;
        }

        internal sealed class NonceCacheImpl : TimeBoundedCache
        {
            private static readonly NonceKeyComparer s_comparer = new();
            private static readonly object s_dummyItem = new();

            // if there are less than lowWaterMark entries, no purging is done
            private const int LowWaterMark = 50;

            // We created a key for the nonce using the first 4 bytes, and hence the minimum length of nonce
            // that can be added to the cache.
            private const int MinimumNonceLength = 4;
            private TimeSpan _cachingTimeSpan;

            public NonceCacheImpl(TimeSpan cachingTimeSpan, int maxCachedNonces)
                : base(LowWaterMark, maxCachedNonces, s_comparer, PurgingMode.AccessBasedPurge, TimeSpan.FromTicks(cachingTimeSpan.Ticks >> 2), false)
            {
                _cachingTimeSpan = cachingTimeSpan;
            }

            public bool TryAddNonce(byte[] nonce)
            {
                if (nonce == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(nonce));

                if (nonce.Length < MinimumNonceLength)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.NonceLengthTooShort));

                DateTime expirationTime = TimeoutHelper.Add(DateTime.UtcNow, _cachingTimeSpan);
                return TryAddItem(nonce, s_dummyItem, expirationTime, false);
            }

            public bool CheckNonce(byte[] nonce)
            {
                if (nonce == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(nonce));

                if (nonce.Length < MinimumNonceLength)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.NonceLengthTooShort));

                return GetItem(nonce) != null;
            }

            /// <summary>
            /// This class provides the hash-code value for the key (nonce) of the nonce cache.
            /// The hash code is obtained from the nonce byte array  by making an int of
            /// the first 4 bytes
            /// </summary>
            internal sealed class NonceKeyComparer : IEqualityComparer, IEqualityComparer<byte[]>
            {
                public int GetHashCode(object o)
                {
                    return GetHashCode((byte[])o);
                }

                public int GetHashCode(byte[] o)
                {
                    // NonceCacheImpl guarantees that the length of o is at least 4
                    Fx.Assert(o.Length >= 4, "Nonces must be at least 4 bytes long");
                    Span<byte> nonceBytes = o;
                    return MemoryMarshal.Read<int>(nonceBytes.Slice(0, 4));
                }

                public int Compare(object x, object y)
                {
                    return Compare((byte[])x, (byte[])y);
                }

                public int Compare(byte[] x, byte[] y)
                {
                    if (ReferenceEquals(x, y))
                        return 0;

                    if (x == null)
                        return -1;
                    else if (y == null)
                        return 1;

                    byte[] nonce1 = x;
                    int length1 = nonce1.Length;
                    byte[] nonce2 = y;
                    int length2 = nonce2.Length;

                    if (length1 == length2)
                    {
                        for (int i = 0; i < length1; ++i)
                        {
                            int diff = nonce1[i] - nonce2[i];

                            if (diff != 0)
                            {
                                return diff;
                            }
                        }

                        return 0;
                    }
                    else if (length1 > length2)
                    {
                        return 1;
                    }
                    else
                    {
                        return -1;
                    }
                }

                public new bool Equals(object x, object y) => Compare(x, y) == 0;

                public bool Equals(byte[] x, byte[] y) => Compare(x, y) == 0;
            }
        }
    }
}
