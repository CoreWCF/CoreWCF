// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Security
{
    internal sealed class SecurityTimestamp
    {
        private const string DefaultFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        internal static readonly TimeSpan defaultTimeToLive = SecurityProtocolFactory.defaultTimestampValidityDuration;
        private char[] _computedCreationTimeUtc;
        private char[] _computedExpiryTimeUtc;
        private DateTime _creationTimeUtc;
        private DateTime _expiryTimeUtc;
        private readonly string _digestAlgorithm;
        private readonly byte[] _digest;

        public SecurityTimestamp(DateTime creationTimeUtc, DateTime expiryTimeUtc, string id)
            : this(creationTimeUtc, expiryTimeUtc, id, null, null)
        {
        }

        internal SecurityTimestamp(DateTime creationTimeUtc, DateTime expiryTimeUtc, string id, string digestAlgorithm, byte[] digest)
        {
            Fx.Assert(creationTimeUtc.Kind == DateTimeKind.Utc, "creation time must be in UTC");
            Fx.Assert(expiryTimeUtc.Kind == DateTimeKind.Utc, "expiry time must be in UTC");

            if (creationTimeUtc > expiryTimeUtc)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new ArgumentOutOfRangeException(nameof(expiryTimeUtc), SR.CreationTimeUtcIsAfterExpiryTime));
            }

            _creationTimeUtc = creationTimeUtc;
            _expiryTimeUtc = expiryTimeUtc;
            Id = id;

            _digestAlgorithm = digestAlgorithm;
            _digest = digest;
        }

        public DateTime CreationTimeUtc
        {
            get
            {
                return _creationTimeUtc;
            }
        }

        public DateTime ExpiryTimeUtc
        {
            get
            {
                return _expiryTimeUtc;
            }
        }

        public string Id { get; }

        public string DigestAlgorithm
        {
            get
            {
                return _digestAlgorithm;
            }
        }

        internal byte[] GetDigest()
        {
            return _digest;
        }

        internal char[] GetCreationTimeChars()
        {
            if (_computedCreationTimeUtc == null)
            {
                _computedCreationTimeUtc = ToChars(ref _creationTimeUtc);
            }
            return _computedCreationTimeUtc;
        }

        internal char[] GetExpiryTimeChars()
        {
            if (_computedExpiryTimeUtc == null)
            {
                _computedExpiryTimeUtc = ToChars(ref _expiryTimeUtc);
            }
            return _computedExpiryTimeUtc;
        }

        private static char[] ToChars(ref DateTime utcTime)
        {
            char[] buffer = new char[DefaultFormat.Length];
            int offset = 0;

            ToChars(utcTime.Year, buffer, ref offset, 4);
            buffer[offset++] = '-';

            ToChars(utcTime.Month, buffer, ref offset, 2);
            buffer[offset++] = '-';

            ToChars(utcTime.Day, buffer, ref offset, 2);
            buffer[offset++] = 'T';

            ToChars(utcTime.Hour, buffer, ref offset, 2);
            buffer[offset++] = ':';

            ToChars(utcTime.Minute, buffer, ref offset, 2);
            buffer[offset++] = ':';

            ToChars(utcTime.Second, buffer, ref offset, 2);
            buffer[offset++] = '.';

            ToChars(utcTime.Millisecond, buffer, ref offset, 3);
            buffer[offset++] = 'Z';

            return buffer;
        }

        private static void ToChars(int n, char[] buffer, ref int offset, int count)
        {
            for (int i = offset + count - 1; i >= offset; i--)
            {
                buffer[i] = (char)('0' + (n % 10));
                n /= 10;
            }
            Fx.Assert(n == 0, "Overflow in encoding timestamp field");
            offset += count;
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "SecurityTimestamp: Id={0}, CreationTimeUtc={1}, ExpirationTimeUtc={2}",
                Id,
                XmlConvert.ToString(CreationTimeUtc, XmlDateTimeSerializationMode.RoundtripKind),
                XmlConvert.ToString(ExpiryTimeUtc, XmlDateTimeSerializationMode.RoundtripKind));
        }

        /// <summary>
        /// Internal method that checks if the timestamp is fresh with respect to the
        /// timeToLive and allowedClockSkew values passed in.
        /// Throws if the timestamp is stale.
        /// </summary>
        /// <param name="timeToLive"></param>
        /// <param name="allowedClockSkew"></param>
        internal void ValidateRangeAndFreshness(TimeSpan timeToLive, TimeSpan allowedClockSkew)
        {
            // Check that the creation time is less than expiry time
            if (CreationTimeUtc >= ExpiryTimeUtc)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.TimeStampHasCreationAheadOfExpiry, CreationTimeUtc.ToString(DefaultFormat, CultureInfo.CurrentCulture), ExpiryTimeUtc.ToString(DefaultFormat, CultureInfo.CurrentCulture))));
            }

            ValidateFreshness(timeToLive, allowedClockSkew);
        }

        internal void ValidateFreshness(TimeSpan timeToLive, TimeSpan allowedClockSkew)
        {
            DateTime now = DateTime.UtcNow;
            // check that the message has not expired
            if (ExpiryTimeUtc <= TimeoutHelper.Subtract(now, allowedClockSkew))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.TimeStampHasExpiryTimeInPast, ExpiryTimeUtc.ToString(DefaultFormat, CultureInfo.CurrentCulture), now.ToString(DefaultFormat, CultureInfo.CurrentCulture), allowedClockSkew)));
            }

            // check that creation time is not in the future (modulo clock skew)
            if (CreationTimeUtc >= TimeoutHelper.Add(now, allowedClockSkew))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.TimeStampHasCreationTimeInFuture, CreationTimeUtc.ToString(DefaultFormat, CultureInfo.CurrentCulture), now.ToString(DefaultFormat, CultureInfo.CurrentCulture), allowedClockSkew)));
            }

            // check that the creation time is not more than timeToLive in the past
            if (CreationTimeUtc <= TimeoutHelper.Subtract(now, TimeoutHelper.Add(timeToLive, allowedClockSkew)))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.TimeStampWasCreatedTooLongAgo, CreationTimeUtc.ToString(DefaultFormat, CultureInfo.CurrentCulture), now.ToString(DefaultFormat, CultureInfo.CurrentCulture), timeToLive, allowedClockSkew)));
            }

            // this is a fresh timestamp
        }
    }
}
