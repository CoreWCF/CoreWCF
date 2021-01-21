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
        private int replayCacheSize;
        private TimeSpan replayWindow;
        private TimeSpan maxClockSkew;
        private TimeSpan issuedCookieLifetime;
        private int maxStatefulNegotiations;
        private TimeSpan negotiationTimeout;
        private int maxCachedCookies;
        private int maxPendingSessions;
        private TimeSpan inactivityTimeout;
        private TimeSpan sessionKeyRenewalInterval;
        private TimeSpan sessionKeyRolloverInterval;
        private TimeSpan timestampValidityDuration;

        private LocalServiceSecuritySettings(LocalServiceSecuritySettings other)
        {
            DetectReplays = other.DetectReplays;
            replayCacheSize = other.replayCacheSize;
            replayWindow = other.replayWindow;
            maxClockSkew = other.maxClockSkew;
            issuedCookieLifetime = other.issuedCookieLifetime;
            maxStatefulNegotiations = other.maxStatefulNegotiations;
            negotiationTimeout = other.negotiationTimeout;
            maxPendingSessions = other.maxPendingSessions;
            inactivityTimeout = other.inactivityTimeout;
            sessionKeyRenewalInterval = other.sessionKeyRenewalInterval;
            sessionKeyRolloverInterval = other.sessionKeyRolloverInterval;
            ReconnectTransportOnFailure = other.ReconnectTransportOnFailure;
            timestampValidityDuration = other.timestampValidityDuration;
            maxCachedCookies = other.maxCachedCookies;
            NonceCache = other.NonceCache;
        }

        public bool DetectReplays { get; set; }

        public int ReplayCacheSize
        {
            get
            {
                return replayCacheSize;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                                                    SR.ValueMustBeNonNegative));
                }
                replayCacheSize = value;
            }
        }

        public TimeSpan ReplayWindow
        {
            get
            {
                return replayWindow;
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

                replayWindow = value;
            }
        }

        public TimeSpan MaxClockSkew
        {
            get
            {
                return maxClockSkew;
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

                maxClockSkew = value;
            }
        }

        public NonceCache NonceCache { get; set; } = null;

        public TimeSpan IssuedCookieLifetime
        {
            get
            {
                return issuedCookieLifetime;
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

                issuedCookieLifetime = value;
            }
        }

        public int MaxStatefulNegotiations
        {
            get
            {
                return maxStatefulNegotiations;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                                                    SR.ValueMustBeNonNegative));
                }
                maxStatefulNegotiations = value;
            }
        }

        public TimeSpan NegotiationTimeout
        {
            get
            {
                return negotiationTimeout;
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

                negotiationTimeout = value;
            }
        }

        public int MaxPendingSessions
        {
            get
            {
                return maxPendingSessions;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                                                    SR.ValueMustBeNonNegative));
                }
                maxPendingSessions = value;
            }
        }

        public TimeSpan InactivityTimeout
        {
            get
            {
                return inactivityTimeout;
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

                inactivityTimeout = value;
            }
        }

        public TimeSpan SessionKeyRenewalInterval
        {
            get
            {
                return sessionKeyRenewalInterval;
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

                sessionKeyRenewalInterval = value;
            }
        }

        public TimeSpan SessionKeyRolloverInterval
        {
            get
            {
                return sessionKeyRolloverInterval;
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

                sessionKeyRolloverInterval = value;
            }
        }

        public bool ReconnectTransportOnFailure { get; set; }

        public TimeSpan TimestampValidityDuration
        {
            get
            {
                return timestampValidityDuration;
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

                timestampValidityDuration = value;
            }
        }

        public int MaxCachedCookies
        {
            get
            {
                return maxCachedCookies;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                                                    SR.ValueMustBeNonNegative));
                }
                maxCachedCookies = value;
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
            maxPendingSessions = SecuritySessionServerSettings.defaultMaximumPendingSessions;
            inactivityTimeout = SecuritySessionServerSettings.defaultInactivityTimeout;
            sessionKeyRenewalInterval = SecuritySessionServerSettings.defaultKeyRenewalInterval;
            sessionKeyRolloverInterval = SecuritySessionServerSettings.defaultKeyRolloverInterval;
            ReconnectTransportOnFailure = SecuritySessionServerSettings.defaultTolerateTransportFailures;
            TimestampValidityDuration = SecurityProtocolFactory.defaultTimestampValidityDuration;
            maxCachedCookies = 1000; // NegotiationTokenAuthenticator<NegotiationTokenAuthenticatorState>.defaultServerMaxCachedTokens;
            NonceCache = null;
        }

        public LocalServiceSecuritySettings Clone()
        {
            return new LocalServiceSecuritySettings(this);
        }
    }
}
