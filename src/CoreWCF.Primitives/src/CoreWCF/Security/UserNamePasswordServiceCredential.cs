using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace CoreWCF.Security
{
    public sealed class UserNamePasswordServiceCredential
    {
        internal const UserNamePasswordValidationMode DefaultUserNamePasswordValidationMode = UserNamePasswordValidationMode.Windows;
        internal const bool DefaultCacheLogonTokens = false;
        internal const int DefaultMaxCachedLogonTokens = 128;
        internal const string DefaultCachedLogonTokenLifetimeString = "00:15:00";
        internal static readonly TimeSpan DefaultCachedLogonTokenLifetime = TimeSpan.Parse(DefaultCachedLogonTokenLifetimeString, CultureInfo.InvariantCulture);

        UserNamePasswordValidationMode validationMode = DefaultUserNamePasswordValidationMode;
        UserNamePasswordValidator validator;
        object membershipProvider;
        bool includeWindowsGroups = SspiSecurityTokenProvider.DefaultExtractWindowsGroupClaims;
        bool cacheLogonTokens = DefaultCacheLogonTokens;
        int maxCachedLogonTokens = DefaultMaxCachedLogonTokens;
        TimeSpan cachedLogonTokenLifetime = DefaultCachedLogonTokenLifetime;
        bool isReadOnly;

        internal UserNamePasswordServiceCredential()
        {
            // empty
        }

        internal UserNamePasswordServiceCredential(UserNamePasswordServiceCredential other)
        {
            includeWindowsGroups = other.includeWindowsGroups;
            membershipProvider = other.membershipProvider;
            validationMode = other.validationMode;
            validator = other.validator;
            cacheLogonTokens = other.cacheLogonTokens;
            maxCachedLogonTokens = other.maxCachedLogonTokens;
            cachedLogonTokenLifetime = other.cachedLogonTokenLifetime;
            isReadOnly = other.isReadOnly;
        }

        public UserNamePasswordValidationMode UserNamePasswordValidationMode
        {
            get
            {
                return validationMode;
            }
            set
            {
                UserNamePasswordValidationModeHelper.Validate(value);
                ThrowIfImmutable();
                if(value == UserNamePasswordValidationMode.MembershipProvider)
                {
                    throw new PlatformNotSupportedException("MembershipProvider not supported");
                }

                validationMode = value;
            }
        }

        public UserNamePasswordValidator CustomUserNamePasswordValidator
        {
            get
            {
                return validator;
            }
            set
            {
                ThrowIfImmutable();
                validator = value;
            }
        }

        public bool IncludeWindowsGroups
        {
            get
            {
                return includeWindowsGroups;
            }
            set
            {
                ThrowIfImmutable();
                includeWindowsGroups = value;
            }
        }

        public bool CacheLogonTokens
        {
            get
            {
                return cacheLogonTokens;
            }
            set
            {
                ThrowIfImmutable();
                cacheLogonTokens = value;
            }
        }

        public int MaxCachedLogonTokens
        {
            get
            {
                return maxCachedLogonTokens;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", SR.ValueMustBeGreaterThanZero));
                }
                ThrowIfImmutable();
                maxCachedLogonTokens = value;
            }
        }

        public TimeSpan CachedLogonTokenLifetime
        {
            get
            {
                return cachedLogonTokenLifetime;
            }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.SFxTimeoutOutOfRangeTooBig));
                }
                ThrowIfImmutable();
                cachedLogonTokenLifetime = value;
            }
        }

        internal UserNamePasswordValidator GetUserNamePasswordValidator()
        {
            if (validationMode == UserNamePasswordValidationMode.MembershipProvider)
            {
                throw new PlatformNotSupportedException("MembershipProvider not supported");
            }
            else if (validationMode == UserNamePasswordValidationMode.Custom)
            {
                if (validator == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.MissingCustomUserNamePasswordValidator));
                }
                return validator;
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
        }

        internal void MakeReadOnly()
        {
            isReadOnly = true;
        }

        void ThrowIfImmutable()
        {
            if (isReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }
        }

    }
}
