// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Runtime;

namespace CoreWCF.Security
{
    public sealed class UserNamePasswordServiceCredential
    {
        internal const UserNamePasswordValidationMode DefaultUserNamePasswordValidationMode = UserNamePasswordValidationMode.Windows;
        internal const bool DefaultCacheLogonTokens = false;
        internal const int DefaultMaxCachedLogonTokens = 128;
        internal const string DefaultCachedLogonTokenLifetimeString = "00:15:00";
        internal static readonly TimeSpan DefaultCachedLogonTokenLifetime = TimeSpan.Parse(DefaultCachedLogonTokenLifetimeString, CultureInfo.InvariantCulture);
        private UserNamePasswordValidationMode _validationMode = DefaultUserNamePasswordValidationMode;
        private UserNamePasswordValidator _validator;
        private readonly object _membershipProvider;
        private bool _includeWindowsGroups = SspiSecurityTokenProvider.DefaultExtractWindowsGroupClaims;
        private bool _cacheLogonTokens = DefaultCacheLogonTokens;
        private int _maxCachedLogonTokens = DefaultMaxCachedLogonTokens;
        private TimeSpan _cachedLogonTokenLifetime = DefaultCachedLogonTokenLifetime;
        private bool _isReadOnly;

        internal UserNamePasswordServiceCredential()
        {
            // empty
        }

        internal UserNamePasswordServiceCredential(UserNamePasswordServiceCredential other)
        {
            _includeWindowsGroups = other._includeWindowsGroups;
            _membershipProvider = other._membershipProvider;
            _validationMode = other._validationMode;
            _validator = other._validator;
            _cacheLogonTokens = other._cacheLogonTokens;
            _maxCachedLogonTokens = other._maxCachedLogonTokens;
            _cachedLogonTokenLifetime = other._cachedLogonTokenLifetime;
            _isReadOnly = other._isReadOnly;
        }

        public UserNamePasswordValidationMode UserNamePasswordValidationMode
        {
            get
            {
                return _validationMode;
            }
            set
            {
                UserNamePasswordValidationModeHelper.Validate(value);
                ThrowIfImmutable();
                if (value == UserNamePasswordValidationMode.MembershipProvider)
                {
                    throw new PlatformNotSupportedException("MembershipProvider not supported");
                }

                _validationMode = value;
            }
        }

        public UserNamePasswordValidator CustomUserNamePasswordValidator
        {
            get
            {
                return _validator;
            }
            set
            {
                ThrowIfImmutable();
                _validator = value;
            }
        }

        public bool IncludeWindowsGroups
        {
            get
            {
                return _includeWindowsGroups;
            }
            set
            {
                ThrowIfImmutable();
                _includeWindowsGroups = value;
            }
        }

        public bool CacheLogonTokens
        {
            get
            {
                return _cacheLogonTokens;
            }
            set
            {
                ThrowIfImmutable();
                _cacheLogonTokens = value;
            }
        }

        public int MaxCachedLogonTokens
        {
            get
            {
                return _maxCachedLogonTokens;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.ValueMustBeGreaterThanZero));
                }
                ThrowIfImmutable();
                _maxCachedLogonTokens = value;
            }
        }

        public TimeSpan CachedLogonTokenLifetime
        {
            get
            {
                return _cachedLogonTokenLifetime;
            }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SRCommon.TimeSpanMustBeGreaterThanTimeSpanZero));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SRCommon.SFxTimeoutOutOfRangeTooBig));
                }
                ThrowIfImmutable();
                _cachedLogonTokenLifetime = value;
            }
        }

        internal UserNamePasswordValidator GetUserNamePasswordValidator()
        {
            if (_validationMode == UserNamePasswordValidationMode.MembershipProvider)
            {
                throw new PlatformNotSupportedException("MembershipProvider not supported");
            }
            else if (_validationMode == UserNamePasswordValidationMode.Custom)
            {
                if (_validator == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.MissingCustomUserNamePasswordValidator));
                }
                return _validator;
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
        }

        internal void MakeReadOnly()
        {
            _isReadOnly = true;
        }

        private void ThrowIfImmutable()
        {
            if (_isReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }
        }
    }
}
