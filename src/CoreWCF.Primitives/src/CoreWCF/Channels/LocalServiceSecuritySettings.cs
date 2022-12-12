// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Runtime;
using CoreWCF.Security;

namespace CoreWCF.Channels
{
    public sealed class LocalServiceSecuritySettings
    {
        //Move these to NegotiationTokenAuthenticator
        internal const string defaultServerMaxNegotiationLifetimeString = "00:01:00";
        internal const string defaultServerIssuedTokenLifetimeString = "10:00:00";
        internal const string defaultServerIssuedTransitionTokenLifetimeString = "00:15:00";
        internal const int defaultServerMaxActiveNegotiations = 128;
        private int _replayCacheSize;
        private TimeSpan _replayWindow;
        private TimeSpan _maxClockSkew;
        private TimeSpan _issuedCookieLifetime;
        private int _maxStatefulNegotiations;
        private TimeSpan _negotiationTimeout;
        private int _maxCachedCookies;
        private int _maxPendingSessions;
        private TimeSpan _inactivityTimeout;
        private TimeSpan _sessionKeyRenewalInterval;
        private TimeSpan _sessionKeyRolloverInterval;
        private TimeSpan _timestampValidityDuration;

        private LocalServiceSecuritySettings(LocalServiceSecuritySettings other)
        {
            DetectReplays = other.DetectReplays;
            _replayCacheSize = other._replayCacheSize;
            _replayWindow = other._replayWindow;
            _maxClockSkew = other._maxClockSkew;
            _issuedCookieLifetime = other._issuedCookieLifetime;
            _maxStatefulNegotiations = other._maxStatefulNegotiations;
            _negotiationTimeout = other._negotiationTimeout;
            _maxPendingSessions = other._maxPendingSessions;
            _inactivityTimeout = other._inactivityTimeout;
            _sessionKeyRenewalInterval = other._sessionKeyRenewalInterval;
            _sessionKeyRolloverInterval = other._sessionKeyRolloverInterval;
            ReconnectTransportOnFailure = other.ReconnectTransportOnFailure;
            _timestampValidityDuration = other._timestampValidityDuration;
            _maxCachedCookies = other._maxCachedCookies;
            NonceCache = other.NonceCache;
        }

        public bool DetectReplays { get; set; }

        public int ReplayCacheSize
        {
            get
            {
                return _replayCacheSize;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                                                    SRCommon.ValueMustBeNonNegative));
                }
                _replayCacheSize = value;
            }
        }

        public TimeSpan ReplayWindow
        {
            get
            {
                return _replayWindow;
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

                _replayWindow = value;
            }
        }

        public TimeSpan MaxClockSkew
        {
            get
            {
                return _maxClockSkew;
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

                _maxClockSkew = value;
            }
        }

        public NonceCache NonceCache { get; set; } = null;

        public TimeSpan IssuedCookieLifetime
        {
            get
            {
                return _issuedCookieLifetime;
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

                _issuedCookieLifetime = value;
            }
        }

        public int MaxStatefulNegotiations
        {
            get
            {
                return _maxStatefulNegotiations;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                                                    SRCommon.ValueMustBeNonNegative));
                }
                _maxStatefulNegotiations = value;
            }
        }

        public TimeSpan NegotiationTimeout
        {
            get
            {
                return _negotiationTimeout;
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

                _negotiationTimeout = value;
            }
        }

        public int MaxPendingSessions
        {
            get
            {
                return _maxPendingSessions;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                                                    SRCommon.ValueMustBeNonNegative));
                }
                _maxPendingSessions = value;
            }
        }

        public TimeSpan InactivityTimeout
        {
            get
            {
                return _inactivityTimeout;
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

                _inactivityTimeout = value;
            }
        }

        public TimeSpan SessionKeyRenewalInterval
        {
            get
            {
                return _sessionKeyRenewalInterval;
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

                _sessionKeyRenewalInterval = value;
            }
        }

        public TimeSpan SessionKeyRolloverInterval
        {
            get
            {
                return _sessionKeyRolloverInterval;
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

                _sessionKeyRolloverInterval = value;
            }
        }

        public bool ReconnectTransportOnFailure { get; set; }

        public TimeSpan TimestampValidityDuration
        {
            get
            {
                return _timestampValidityDuration;
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

                _timestampValidityDuration = value;
            }
        }

        public int MaxCachedCookies
        {
            get
            {
                return _maxCachedCookies;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                                                    SRCommon.ValueMustBeNonNegative));
                }
                _maxCachedCookies = value;
            }
        }

        public LocalServiceSecuritySettings()
        {
            DetectReplays = SecurityProtocolFactory.defaultDetectReplays;
            ReplayCacheSize = SecurityProtocolFactory.defaultMaxCachedNonces;
            ReplayWindow = SecurityProtocolFactory.defaultReplayWindow;
            MaxClockSkew = SecurityProtocolFactory.defaultMaxClockSkew;
            NegotiationTimeout = TimeSpan.FromMinutes(1);
            IssuedCookieLifetime = TimeSpan.FromHours(10);
            MaxStatefulNegotiations = 128; //NegotiationTokenAuthenticator<NegotiationTokenAuthenticatorState>.defaultServerMaxActiveNegotiations;
            NegotiationTimeout = TimeSpan.FromMinutes(1);// NegotiationTokenAuthenticator<NegotiationTokenAuthenticatorState>.defaultServerMaxNegotiationLifetime;
            _maxPendingSessions = SecuritySessionServerSettings.DefaultMaximumPendingSessions;
            _inactivityTimeout = SecuritySessionServerSettings.s_defaultInactivityTimeout;
            _sessionKeyRenewalInterval = SecuritySessionServerSettings.s_defaultKeyRenewalInterval;
            _sessionKeyRolloverInterval = SecuritySessionServerSettings.s_defaultKeyRolloverInterval;
            ReconnectTransportOnFailure = SecuritySessionServerSettings.DefaultTolerateTransportFailures;
            TimestampValidityDuration = SecurityProtocolFactory.defaultTimestampValidityDuration;
            _maxCachedCookies = 1000; // NegotiationTokenAuthenticator<NegotiationTokenAuthenticatorState>.defaultServerMaxCachedTokens;
            NonceCache = null;
        }

        public LocalServiceSecuritySettings Clone()
        {
            return new LocalServiceSecuritySettings(this);
        }
    }
}
