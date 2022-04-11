// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public sealed class TcpConnectionPoolSettingsElement : ServiceModelConfigurationElement
    {
        [ConfigurationProperty(ConfigurationStrings.IdleTimeout, DefaultValue = ConnectionOrientedTransportDefaults.IdleTimeoutString)]
        public TimeSpan IdleTimeout
        {
            get { return (TimeSpan)base[ConfigurationStrings.IdleTimeout]; }
            set { base[ConfigurationStrings.IdleTimeout] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxOutboundConnectionsPerEndpoint, DefaultValue = ConnectionOrientedTransportDefaults.MaxOutboundConnectionsPerEndpoint)]
        [IntegerValidator(MinValue = 0)]
        public int MaxOutboundConnectionsPerEndpoint
        {
            get { return (int)base[ConfigurationStrings.MaxOutboundConnectionsPerEndpoint]; }
            set { base[ConfigurationStrings.MaxOutboundConnectionsPerEndpoint] = value; }
        }

        internal void ApplyConfiguration(TcpConnectionPoolSettings settings)
        {
            if (null == settings)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(settings));
            }

            settings.IdleTimeout = IdleTimeout;
            settings.MaxOutboundConnectionsPerEndpoint = MaxOutboundConnectionsPerEndpoint;
        }

        internal void InitializeFrom(TcpConnectionPoolSettings settings)
        {
            if (null == settings)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(settings));
            }

            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.IdleTimeout, settings.IdleTimeout);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxOutboundConnectionsPerEndpoint, settings.MaxOutboundConnectionsPerEndpoint);
        }

        internal void CopyFrom(TcpConnectionPoolSettingsElement source)
        {
            if (source == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(source));
            }

            //this.GroupName = source.GroupName;
            IdleTimeout = source.IdleTimeout;
            //this.LeaseTimeout = source.LeaseTimeout;
            MaxOutboundConnectionsPerEndpoint = source.MaxOutboundConnectionsPerEndpoint;
        }
    }
}
