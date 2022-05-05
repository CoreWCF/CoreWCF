// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public class WebSocketTransportSettingsElement : ServiceModelConfigurationElement
    {
        [ConfigurationProperty(ConfigurationStrings.TransportUsage, DefaultValue = WebSocketDefaults.TransportUsage)]
        public virtual WebSocketTransportUsage TransportUsage
        {
            get { return (WebSocketTransportUsage)base[ConfigurationStrings.TransportUsage]; }
            set { base[ConfigurationStrings.TransportUsage] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.CreateNotificationOnConnection, DefaultValue = WebSocketDefaults.CreateNotificationOnConnection)]
        public bool CreateNotificationOnConnection
        {
            get { return (bool)base[ConfigurationStrings.CreateNotificationOnConnection]; }
            set { base[ConfigurationStrings.CreateNotificationOnConnection] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.KeepAliveInterval, DefaultValue = WebSocketDefaults.DefaultKeepAliveIntervalString)]
        public TimeSpan KeepAliveInterval
        {
            get { return (TimeSpan)base[ConfigurationStrings.KeepAliveInterval]; }
            set { base[ConfigurationStrings.KeepAliveInterval] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.SubProtocol, DefaultValue = null)]
        [StringValidator(MinLength = 0)]
        public virtual string SubProtocol
        {
            get { return (string)base[ConfigurationStrings.SubProtocol]; }
            set { base[ConfigurationStrings.SubProtocol] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.DisablePayloadMasking, DefaultValue = WebSocketDefaults.DisablePayloadMasking)]
        public bool DisablePayloadMasking
        {
            get { return (bool)base[ConfigurationStrings.DisablePayloadMasking]; }
            set { base[ConfigurationStrings.DisablePayloadMasking] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxPendingConnections, DefaultValue = WebSocketDefaults.DefaultMaxPendingConnections)]
        [IntegerValidator(MinValue = 0)]
        public int MaxPendingConnections
        {
            get { return (int)base[ConfigurationStrings.MaxPendingConnections]; }
            set { base[ConfigurationStrings.MaxPendingConnections] = value; }
        }

        public void InitializeFrom(WebSocketTransportSettings settings)
        {
            if (settings == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(settings));
            }

            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.TransportUsage, settings.TransportUsage);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.CreateNotificationOnConnection, settings.CreateNotificationOnConnection);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.KeepAliveInterval, settings.KeepAliveInterval);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.SubProtocol, settings.SubProtocol);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.DisablePayloadMasking, settings.DisablePayloadMasking);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxPendingConnections, settings.MaxPendingConnections);
        }

        public void ApplyConfiguration(WebSocketTransportSettings settings)
        {
            if (settings == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(settings));
            }

            settings.TransportUsage = TransportUsage;
            settings.CreateNotificationOnConnection = CreateNotificationOnConnection;
            settings.KeepAliveInterval = KeepAliveInterval;
            settings.SubProtocol = string.IsNullOrEmpty(SubProtocol) ? null : SubProtocol;
            settings.DisablePayloadMasking = DisablePayloadMasking;
            settings.MaxPendingConnections = MaxPendingConnections;
        }
    }
}
