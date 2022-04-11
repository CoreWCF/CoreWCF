// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Configuration;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public sealed class LocalServiceSecuritySettingsElement : ServiceModelConfigurationElement
    {
        [ConfigurationProperty(ConfigurationStrings.DetectReplays, DefaultValue = SecurityBindingDefaults.DefaultDetectReplays)]
        public bool DetectReplays
        {
            get { return (bool)base[ConfigurationStrings.DetectReplays]; }
            set { base[ConfigurationStrings.DetectReplays] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.IssuedCookieLifetime, DefaultValue = SecurityBindingDefaults.DefaultServerIssuedTokenLifetimeString)]
        [TypeConverter(typeof(TimeSpanOrInfiniteConverter))]
        public TimeSpan IssuedCookieLifetime
        {
            get { return (TimeSpan)base[ConfigurationStrings.IssuedCookieLifetime]; }
            set { base[ConfigurationStrings.IssuedCookieLifetime] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxStatefulNegotiations, DefaultValue = SecurityBindingDefaults.DefaultServerMaxActiveNegotiations)]
        [IntegerValidator(MinValue = 0)]
        public int MaxStatefulNegotiations
        {
            get { return (int)base[ConfigurationStrings.MaxStatefulNegotiations]; }
            set { base[ConfigurationStrings.MaxStatefulNegotiations] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.ReplayCacheSize, DefaultValue = SecurityBindingDefaults.DefaultMaxCachedNonces)]
        [IntegerValidator(MinValue = 1)]
        public int ReplayCacheSize
        {
            get { return (int)base[ConfigurationStrings.ReplayCacheSize]; }
            set { base[ConfigurationStrings.ReplayCacheSize] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxClockSkew, DefaultValue = SecurityBindingDefaults.DefaultMaxClockSkewString)]
        [TypeConverter(typeof(TimeSpanOrInfiniteConverter))]
        public TimeSpan MaxClockSkew
        {
            get { return (TimeSpan)base[ConfigurationStrings.MaxClockSkew]; }
            set { base[ConfigurationStrings.MaxClockSkew] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.NegotiationTimeout, DefaultValue = SecurityBindingDefaults.DefaultServerMaxNegotiationLifetimeString)]
        [TypeConverter(typeof(TimeSpanOrInfiniteConverter))]
        public TimeSpan NegotiationTimeout
        {
            get { return (TimeSpan)base[ConfigurationStrings.NegotiationTimeout]; }
            set { base[ConfigurationStrings.NegotiationTimeout] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.ReplayWindow, DefaultValue = SecurityBindingDefaults.DefaultReplayWindowString)]
        [TypeConverter(typeof(TimeSpanOrInfiniteConverter))]
        public TimeSpan ReplayWindow
        {
            get { return (TimeSpan)base[ConfigurationStrings.ReplayWindow]; }
            set { base[ConfigurationStrings.ReplayWindow] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.InactivityTimeout, DefaultValue = SecurityBindingDefaults.DefaultInactivityTimeoutString)]
        [TypeConverter(typeof(TimeSpanOrInfiniteConverter))]
        public TimeSpan InactivityTimeout
        {
            get { return (TimeSpan)base[ConfigurationStrings.InactivityTimeout]; }
            set { base[ConfigurationStrings.InactivityTimeout] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.SessionKeyRenewalInterval, DefaultValue = SecurityBindingDefaults.DefaultKeyRenewalIntervalString)]
        [TypeConverter(typeof(TimeSpanOrInfiniteConverter))]
        public TimeSpan SessionKeyRenewalInterval
        {
            get { return (TimeSpan)base[ConfigurationStrings.SessionKeyRenewalInterval]; }
            set { base[ConfigurationStrings.SessionKeyRenewalInterval] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.SessionKeyRolloverInterval, DefaultValue = SecurityBindingDefaults.DefaultKeyRolloverIntervalString)]
        [TypeConverter(typeof(TimeSpanOrInfiniteConverter))]
        public TimeSpan SessionKeyRolloverInterval
        {
            get { return (TimeSpan)base[ConfigurationStrings.SessionKeyRolloverInterval]; }
            set { base[ConfigurationStrings.SessionKeyRolloverInterval] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.ReconnectTransportOnFailure, DefaultValue = SecurityBindingDefaults.DefaultTolerateTransportFailures)]
        public bool ReconnectTransportOnFailure
        {
            get { return (bool)base[ConfigurationStrings.ReconnectTransportOnFailure]; }
            set { base[ConfigurationStrings.ReconnectTransportOnFailure] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxPendingSessions, DefaultValue = SecurityBindingDefaults.DefaultMaximumPendingSessions)]
        [IntegerValidator(MinValue = 1)]
        public int MaxPendingSessions
        {
            get { return (int)base[ConfigurationStrings.MaxPendingSessions]; }
            set { base[ConfigurationStrings.MaxPendingSessions] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxCachedCookies, DefaultValue = SecurityBindingDefaults.DefaultServerMaxCachedTokens)]
        [IntegerValidator(MinValue = 0)]
        public int MaxCachedCookies
        {
            get { return (int)base[ConfigurationStrings.MaxCachedCookies]; }
            set { base[ConfigurationStrings.MaxCachedCookies] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.TimestampValidityDuration, DefaultValue = SecurityBindingDefaults.DefaultTimestampValidityDurationString)]
        [TypeConverter(typeof(TimeSpanOrInfiniteConverter))]
        public TimeSpan TimestampValidityDuration
        {
            get { return (TimeSpan)base[ConfigurationStrings.TimestampValidityDuration]; }
            set { base[ConfigurationStrings.TimestampValidityDuration] = value; }
        }

        internal void ApplyConfiguration(LocalServiceSecuritySettings settings)
        {
            if (settings == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(settings));
            }
            if (PropertyValueOrigin.Default != ElementInformation.Properties[ConfigurationStrings.DetectReplays].ValueOrigin)
                settings.DetectReplays = DetectReplays;
            settings.IssuedCookieLifetime = IssuedCookieLifetime;
            settings.MaxClockSkew = MaxClockSkew;
            settings.MaxPendingSessions = MaxPendingSessions;
            settings.MaxStatefulNegotiations = MaxStatefulNegotiations;
            settings.NegotiationTimeout = NegotiationTimeout;
            settings.ReconnectTransportOnFailure = ReconnectTransportOnFailure;
            settings.ReplayCacheSize = ReplayCacheSize;
            settings.ReplayWindow = ReplayWindow;
            settings.SessionKeyRenewalInterval = SessionKeyRenewalInterval;
            settings.SessionKeyRolloverInterval = SessionKeyRolloverInterval;
            settings.InactivityTimeout = InactivityTimeout;
            settings.TimestampValidityDuration = TimestampValidityDuration;
            settings.MaxCachedCookies = MaxCachedCookies;
        }

        internal void InitializeFrom(LocalServiceSecuritySettings settings)
        {
            if (settings == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(settings));
            }
            DetectReplays = settings.DetectReplays; // can't use default value optimization here because runtime default doesn't match config default
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.IssuedCookieLifetime, settings.IssuedCookieLifetime);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxClockSkew, settings.MaxClockSkew);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxPendingSessions, settings.MaxPendingSessions);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxStatefulNegotiations, settings.MaxStatefulNegotiations);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.NegotiationTimeout, settings.NegotiationTimeout);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.ReconnectTransportOnFailure, settings.ReconnectTransportOnFailure);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.ReplayCacheSize, settings.ReplayCacheSize);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.ReplayWindow, settings.ReplayWindow);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.SessionKeyRenewalInterval, settings.SessionKeyRenewalInterval);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.SessionKeyRolloverInterval, settings.SessionKeyRolloverInterval);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.InactivityTimeout, settings.InactivityTimeout);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.TimestampValidityDuration, settings.TimestampValidityDuration);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxCachedCookies, settings.MaxCachedCookies);
        }

        internal void CopyFrom(LocalServiceSecuritySettingsElement source)
        {
            if (source == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(source));
            }
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.DetectReplays].ValueOrigin)
                DetectReplays = source.DetectReplays;
            IssuedCookieLifetime = source.IssuedCookieLifetime;
            MaxClockSkew = source.MaxClockSkew;
            MaxPendingSessions = source.MaxPendingSessions;
            MaxStatefulNegotiations = source.MaxStatefulNegotiations;
            NegotiationTimeout = source.NegotiationTimeout;
            ReconnectTransportOnFailure = source.ReconnectTransportOnFailure;
            ReplayCacheSize = source.ReplayCacheSize;
            ReplayWindow = source.ReplayWindow;
            SessionKeyRenewalInterval = source.SessionKeyRenewalInterval;
            SessionKeyRolloverInterval = source.SessionKeyRolloverInterval;
            InactivityTimeout = source.InactivityTimeout;
            TimestampValidityDuration = source.TimestampValidityDuration;
            MaxCachedCookies = source.MaxCachedCookies;
        }
    }
}
