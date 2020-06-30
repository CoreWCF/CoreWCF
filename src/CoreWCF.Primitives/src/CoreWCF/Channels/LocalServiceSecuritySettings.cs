using System;
using System.Runtime;
using CoreWCF.Runtime;
using CoreWCF;
using CoreWCF.Security;
using System.Runtime.CompilerServices;
using System.Globalization;

namespace CoreWCF.Channels
{    public sealed class LocalServiceSecuritySettings
    {
        //Move these to NegotiationTokenAuthenticator
        internal const string defaultServerMaxNegotiationLifetimeString = "00:01:00";
        internal const string defaultServerIssuedTokenLifetimeString = "10:00:00";
        internal const string defaultServerIssuedTransitionTokenLifetimeString = "00:15:00";
        internal const int defaultServerMaxActiveNegotiations = 128;

        bool detectReplays;
        int replayCacheSize;
        TimeSpan replayWindow;
        TimeSpan maxClockSkew;
        TimeSpan issuedCookieLifetime;
        int maxStatefulNegotiations;
        TimeSpan negotiationTimeout;
        int maxCachedCookies;
        int maxPendingSessions;
        TimeSpan inactivityTimeout;
        TimeSpan sessionKeyRenewalInterval;
        TimeSpan sessionKeyRolloverInterval;
        bool reconnectTransportOnFailure;
        TimeSpan timestampValidityDuration;
        NonceCache nonceCache = null;

        LocalServiceSecuritySettings(LocalServiceSecuritySettings other)
        {
            this.detectReplays = other.detectReplays;
            this.replayCacheSize = other.replayCacheSize;
            this.replayWindow = other.replayWindow;
            this.maxClockSkew = other.maxClockSkew;
            this.issuedCookieLifetime = other.issuedCookieLifetime;
            this.maxStatefulNegotiations = other.maxStatefulNegotiations;
            this.negotiationTimeout = other.negotiationTimeout;
            this.maxPendingSessions = other.maxPendingSessions;
            this.inactivityTimeout = other.inactivityTimeout;
            this.sessionKeyRenewalInterval = other.sessionKeyRenewalInterval;
            this.sessionKeyRolloverInterval = other.sessionKeyRolloverInterval;
            this.reconnectTransportOnFailure = other.reconnectTransportOnFailure;
            this.timestampValidityDuration = other.timestampValidityDuration;
            this.maxCachedCookies = other.maxCachedCookies;
            this.nonceCache = other.nonceCache;
        }

        public bool DetectReplays
        {
            get
            {
                return this.detectReplays;
            }
            set
            {
                this.detectReplays = value;
            }
        }

        public int ReplayCacheSize
        {
            get
            {
                return this.replayCacheSize;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                                                    SR.Format(SR.ValueMustBeNonNegative)));
                }
                this.replayCacheSize = value;
            }
        }

        public TimeSpan ReplayWindow
        {
            get
            {
                return this.replayWindow;
            }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.Format(SR.SFxTimeoutOutOfRange0)));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.Format(SR.SFxTimeoutOutOfRangeTooBig)));
                }

                this.replayWindow = value;
            }
        }

        public TimeSpan MaxClockSkew
        {
            get
            {
                return this.maxClockSkew;
            }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.Format(SR.SFxTimeoutOutOfRange0)));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.Format(SR.SFxTimeoutOutOfRangeTooBig)));
                }

                this.maxClockSkew = value;
            }
        }


        public NonceCache NonceCache
        {
            get
            {
                return this.nonceCache;
            }
            set
            {
                this.nonceCache = value;
            }
        }

        public TimeSpan IssuedCookieLifetime
        {
            get
            {
                return this.issuedCookieLifetime;
            }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.Format(SR.SFxTimeoutOutOfRange0)));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.Format(SR.SFxTimeoutOutOfRangeTooBig)));
                }

                this.issuedCookieLifetime = value;
            }
        }

        public int MaxStatefulNegotiations
        {
            get
            {
                return this.maxStatefulNegotiations;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                                                    SR.Format(SR.ValueMustBeNonNegative)));
                }
                this.maxStatefulNegotiations = value;
            }
        }

        public TimeSpan NegotiationTimeout
        {
            get
            {
                return this.negotiationTimeout;
            }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.Format(SR.SFxTimeoutOutOfRange0)));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.Format(SR.SFxTimeoutOutOfRangeTooBig)));
                }

                this.negotiationTimeout = value;
            }
        }

        public int MaxPendingSessions
        {
            get
            {
                return this.maxPendingSessions;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                                                    SR.Format(SR.ValueMustBeNonNegative)));
                }
                this.maxPendingSessions = value;
            }
        }

        public TimeSpan InactivityTimeout
        {
            get
            {
                return this.inactivityTimeout;
            }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.Format(SR.SFxTimeoutOutOfRange0)));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.Format(SR.SFxTimeoutOutOfRangeTooBig)));
                }

                this.inactivityTimeout = value;
            }
        }

        public TimeSpan SessionKeyRenewalInterval
        {
            get
            {
                return this.sessionKeyRenewalInterval;
            }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.Format(SR.SFxTimeoutOutOfRange0)));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.Format(SR.SFxTimeoutOutOfRangeTooBig)));
                }

                this.sessionKeyRenewalInterval = value;
            }
        }

        public TimeSpan SessionKeyRolloverInterval
        {
            get
            {
                return this.sessionKeyRolloverInterval;
            }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.Format(SR.SFxTimeoutOutOfRange0)));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.Format(SR.SFxTimeoutOutOfRangeTooBig)));
                }

                this.sessionKeyRolloverInterval = value;
            }
        }

        public bool ReconnectTransportOnFailure
        {
            get
            {
                return this.reconnectTransportOnFailure;
            }
            set
            {
                this.reconnectTransportOnFailure = value;
            }
        }

        public TimeSpan TimestampValidityDuration
        {
            get
            {
                return this.timestampValidityDuration;
            }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.Format(SR.SFxTimeoutOutOfRange0)));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.Format(SR.SFxTimeoutOutOfRangeTooBig)));
                }

                this.timestampValidityDuration = value;
            }
        }

        public int MaxCachedCookies
        {
            get
            {
                return this.maxCachedCookies;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                                                    SR.Format(SR.ValueMustBeNonNegative)));
                }
                this.maxCachedCookies = value;
            }
        }

        public LocalServiceSecuritySettings()
        {
            this.DetectReplays = SecurityProtocolFactory.defaultDetectReplays;
            this.ReplayCacheSize = SecurityProtocolFactory.defaultMaxCachedNonces;
            this.ReplayWindow = SecurityProtocolFactory.defaultReplayWindow;
            this.MaxClockSkew = SecurityProtocolFactory.defaultMaxClockSkew;
            this.NegotiationTimeout = TimeSpan.Parse(defaultServerMaxNegotiationLifetimeString, CultureInfo.InvariantCulture);
           this.IssuedCookieLifetime  = TimeSpan.Parse(defaultServerIssuedTokenLifetimeString, CultureInfo.InvariantCulture);
             
            //this.IssuedCookieLifetime = NegotiationTokenAuthenticator<NegotiationTokenAuthenticatorState>.defaultServerIssuedTokenLifetime;
            //this.MaxStatefulNegotiations = NegotiationTokenAuthenticator<NegotiationTokenAuthenticatorState>.defaultServerMaxActiveNegotiations;
            //this.NegotiationTimeout = NegotiationTokenAuthenticator<NegotiationTokenAuthenticatorState>.defaultServerMaxNegotiationLifetime;
            this.maxPendingSessions = SecuritySessionServerSettings.defaultMaximumPendingSessions;
            this.inactivityTimeout = SecuritySessionServerSettings.defaultInactivityTimeout;
            this.sessionKeyRenewalInterval = SecuritySessionServerSettings.defaultKeyRenewalInterval;
            this.sessionKeyRolloverInterval = SecuritySessionServerSettings.defaultKeyRolloverInterval;
            this.reconnectTransportOnFailure = SecuritySessionServerSettings.defaultTolerateTransportFailures;
            this.TimestampValidityDuration = SecurityProtocolFactory.defaultTimestampValidityDuration;
       //     this.maxCachedCookies = NegotiationTokenAuthenticator<NegotiationTokenAuthenticatorState>.defaultServerMaxCachedTokens;
            this.nonceCache = null;
        }

        public LocalServiceSecuritySettings Clone()
        {
            return new LocalServiceSecuritySettings(this);
        }
    }
}
