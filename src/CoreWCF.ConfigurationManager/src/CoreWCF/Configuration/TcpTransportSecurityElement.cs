﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;
using System.Security.Authentication;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public class TcpTransportSecurityElement : ServiceModelConfigurationElement
    {
        [ConfigurationProperty(ConfigurationStrings.ClientCredentialType, DefaultValue = TcpClientCredentialType.Windows)]
        public TcpClientCredentialType ClientCredentialType
        {
            get { return (TcpClientCredentialType)base[ConfigurationStrings.ClientCredentialType]; }
            set { base[ConfigurationStrings.ClientCredentialType] = value; }
        }

        //[ConfigurationProperty(ConfigurationStrings.ProtectionLevel, DefaultValue = TcpTransportSecurity.DefaultProtectionLevel)]
        //[ServiceModelEnumValidator(typeof(ProtectionLevelHelper))]
        //public ProtectionLevel ProtectionLevel
        //{
        //    get { return (ProtectionLevel)base[ConfigurationStrings.ProtectionLevel]; }
        //    set { base[ConfigurationStrings.ProtectionLevel] = value; }
        //}

        //[ConfigurationProperty(ConfigurationStrings.ExtendedProtectionPolicy)]
        //public ExtendedProtectionPolicyElement ExtendedProtectionPolicy
        //{
        //    get { return (ExtendedProtectionPolicyElement)base[ConfigurationStrings.ExtendedProtectionPolicy]; }
        //    private set { base[ConfigurationStrings.ExtendedProtectionPolicy] = value; }
        //}

        [ConfigurationProperty(ConfigurationStrings.SslProtocols, DefaultValue = SslProtocols.None)]
        public SslProtocols SslProtocols
        {
            get { return (SslProtocols)base[ConfigurationStrings.SslProtocols]; }
            private set { base[ConfigurationStrings.SslProtocols] = value; }
        }

        internal void ApplyConfiguration(TcpTransportSecurity security)
        {
            if (security == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(security));
            }

            security.ClientCredentialType = ClientCredentialType;
            //security.ProtectionLevel = this.ProtectionLevel;
            //security.ExtendedProtectionPolicy = ChannelBindingUtility.BuildPolicy(this.ExtendedProtectionPolicy);
            security.SslProtocols = SslProtocols;
        }
    }
}
