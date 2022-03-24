// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Configuration;

namespace CoreWCF.Configuration
{
    internal sealed class LocalClientSecuritySettingsElement : ServiceModelConfigurationElement
    {
        [ConfigurationProperty(ConfigurationStrings.CacheCookies, DefaultValue = SecurityBindingDefaults.DefaultClientCacheTokens)]
        public bool CacheCookies
        {
            get { return (bool)base[ConfigurationStrings.CacheCookies]; }
            set { base[ConfigurationStrings.CacheCookies] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.DetectReplays, DefaultValue = SecurityBindingDefaults.DefaultDetectReplays)]
        public bool DetectReplays
        {
            get { return (bool)base[ConfigurationStrings.DetectReplays]; }
            set { base[ConfigurationStrings.DetectReplays] = value; }
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

        [ConfigurationProperty(ConfigurationStrings.MaxCookieCachingTime, DefaultValue = SecurityBindingDefaults.DefaultClientMaxTokenCachingTimeString)]
        [TypeConverter(typeof(TimeSpanOrInfiniteConverter))]
        public TimeSpan MaxCookieCachingTime
        {
            get { return (TimeSpan)base[ConfigurationStrings.MaxCookieCachingTime]; }
            set { base[ConfigurationStrings.MaxCookieCachingTime] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.ReplayWindow, DefaultValue = SecurityBindingDefaults.DefaultReplayWindowString)]
        [TypeConverter(typeof(TimeSpanOrInfiniteConverter))]
        public TimeSpan ReplayWindow
        {
            get { return (TimeSpan)base[ConfigurationStrings.ReplayWindow]; }
            set { base[ConfigurationStrings.ReplayWindow] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.SessionKeyRenewalInterval, DefaultValue = SecurityBindingDefaults.DefaultClientKeyRenewalIntervalString)]
        [TypeConverter(typeof(TimeSpanOrInfiniteConverter))]
        public TimeSpan SessionKeyRenewalInterval
        {
            get { return (TimeSpan)base[ConfigurationStrings.SessionKeyRenewalInterval]; }
            set { base[ConfigurationStrings.SessionKeyRenewalInterval] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.SessionKeyRolloverInterval, DefaultValue = SecurityBindingDefaults.DefaultClientKeyRolloverIntervalString)]
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

        [ConfigurationProperty(ConfigurationStrings.TimestampValidityDuration, DefaultValue = SecurityBindingDefaults.DefaultTimestampValidityDurationString)]
        [TypeConverter(typeof(TimeSpanOrInfiniteConverter))]
        public TimeSpan TimestampValidityDuration
        {
            get { return (TimeSpan)base[ConfigurationStrings.TimestampValidityDuration]; }
            set { base[ConfigurationStrings.TimestampValidityDuration] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.CookieRenewalThresholdPercentage, DefaultValue = SecurityBindingDefaults.DefaultServiceTokenValidityThresholdPercentage)]
        [IntegerValidator(MinValue = 0, MaxValue = 100)]
        public int CookieRenewalThresholdPercentage
        {
            get { return (int)base[ConfigurationStrings.CookieRenewalThresholdPercentage]; }
            set { base[ConfigurationStrings.CookieRenewalThresholdPercentage] = value; }
        }

        //TODO If Local Client Settings are Supported
        //internal void ApplyConfiguration(LocalClientSecuritySettings settings)
        //{
        //    if (settings == null)
        //    {
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("settings");
        //    }
        //    settings.CacheCookies = this.CacheCookies;
        //    if (PropertyValueOrigin.Default != this.ElementInformation.Properties[ConfigurationStrings.DetectReplays].ValueOrigin)
        //        settings.DetectReplays = this.DetectReplays;
        //    settings.MaxClockSkew = this.MaxClockSkew;
        //    settings.MaxCookieCachingTime = this.MaxCookieCachingTime;
        //    settings.ReconnectTransportOnFailure = this.ReconnectTransportOnFailure;
        //    settings.ReplayCacheSize = this.ReplayCacheSize;
        //    settings.ReplayWindow = this.ReplayWindow;
        //    settings.SessionKeyRenewalInterval = this.SessionKeyRenewalInterval;
        //    settings.SessionKeyRolloverInterval = this.SessionKeyRolloverInterval;
        //    settings.TimestampValidityDuration = this.TimestampValidityDuration;
        //    settings.CookieRenewalThresholdPercentage = this.CookieRenewalThresholdPercentage;
        //}

        //TODO If Local Client Settings are Supported
        //internal void InitializeFrom(LocalClientSecuritySettings settings)
        //{
        //    if (settings == null)
        //    {
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("settings");
        //    }
        //    SetPropertyValueIfNotDefaultValue(ConfigurationStrings.CacheCookies, settings.CacheCookies);
        //    this.DetectReplays = settings.DetectReplays; // can't use default value optimization here because ApplyConfiguration looks at ValueOrigin
        //    SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxClockSkew, settings.MaxClockSkew);
        //    SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxCookieCachingTime, settings.MaxCookieCachingTime);
        //    SetPropertyValueIfNotDefaultValue(ConfigurationStrings.ReconnectTransportOnFailure, settings.ReconnectTransportOnFailure);
        //    SetPropertyValueIfNotDefaultValue(ConfigurationStrings.ReplayCacheSize, settings.ReplayCacheSize);
        //    SetPropertyValueIfNotDefaultValue(ConfigurationStrings.ReplayWindow, settings.ReplayWindow);
        //    SetPropertyValueIfNotDefaultValue(ConfigurationStrings.SessionKeyRenewalInterval, settings.SessionKeyRenewalInterval);
        //    SetPropertyValueIfNotDefaultValue(ConfigurationStrings.SessionKeyRolloverInterval, settings.SessionKeyRolloverInterval);
        //    SetPropertyValueIfNotDefaultValue(ConfigurationStrings.TimestampValidityDuration, settings.TimestampValidityDuration);
        //    SetPropertyValueIfNotDefaultValue(ConfigurationStrings.CookieRenewalThresholdPercentage, settings.CookieRenewalThresholdPercentage);
        //}

        internal void CopyFrom(LocalClientSecuritySettingsElement source)
        {
            if (source == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("source");
            }
            this.CacheCookies = source.CacheCookies;
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.DetectReplays].ValueOrigin)
                this.DetectReplays = source.DetectReplays;
            this.MaxClockSkew = source.MaxClockSkew;
            this.MaxCookieCachingTime = source.MaxCookieCachingTime;
            this.ReconnectTransportOnFailure = source.ReconnectTransportOnFailure;
            this.ReplayCacheSize = source.ReplayCacheSize;
            this.ReplayWindow = source.ReplayWindow;
            this.SessionKeyRenewalInterval = source.SessionKeyRenewalInterval;
            this.SessionKeyRolloverInterval = source.SessionKeyRolloverInterval;
            this.TimestampValidityDuration = source.TimestampValidityDuration;
            this.CookieRenewalThresholdPercentage = source.CookieRenewalThresholdPercentage;
        }
    }
}
