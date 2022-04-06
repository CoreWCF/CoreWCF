﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public sealed class TcpConnectionPoolSettingsElement : ServiceModelConfigurationElement
    {
        //[ConfigurationProperty(ConfigurationStrings.GroupName, DefaultValue = ConnectionOrientedTransportDefaults.ConnectionPoolGroupName)]
        //[StringValidator(MinLength = 0)]
        //public string GroupName
        //{
        //    get { return (string)base[ConfigurationStrings.GroupName]; }
        //    set
        //    {
        //        if (String.IsNullOrEmpty(value))
        //        {
        //            value = String.Empty;
        //        }
        //        base[ConfigurationStrings.GroupName] = value;
        //    }
        //}

        //[ConfigurationProperty(ConfigurationStrings.LeaseTimeout, DefaultValue = TcpTransportDefaults.ConnectionLeaseTimeoutString)]
        //public TimeSpan LeaseTimeout
        //{
        //    get { return (TimeSpan)base[ConfigurationStrings.LeaseTimeout]; }
        //    set { base[ConfigurationStrings.LeaseTimeout] = value; }
        //}

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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("settings");
            }

            //settings.GroupName = this.GroupName;
            settings.IdleTimeout = this.IdleTimeout;
            //settings.LeaseTimeout = this.LeaseTimeout;
            settings.MaxOutboundConnectionsPerEndpoint = this.MaxOutboundConnectionsPerEndpoint;
        }

        internal void InitializeFrom(TcpConnectionPoolSettings settings)
        {
            if (null == settings)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("settings");
            }

            //SetPropertyValueIfNotDefaultValue(ConfigurationStrings.GroupName, settings.GroupName);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.IdleTimeout, settings.IdleTimeout);
            //SetPropertyValueIfNotDefaultValue(ConfigurationStrings.LeaseTimeout, settings.LeaseTimeout);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxOutboundConnectionsPerEndpoint, settings.MaxOutboundConnectionsPerEndpoint);
        }

        internal void CopyFrom(TcpConnectionPoolSettingsElement source)
        {
            if (source == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("source");
            }

            //this.GroupName = source.GroupName;
            this.IdleTimeout = source.IdleTimeout;
            //this.LeaseTimeout = source.LeaseTimeout;
            this.MaxOutboundConnectionsPerEndpoint = source.MaxOutboundConnectionsPerEndpoint;
        }
    }
}