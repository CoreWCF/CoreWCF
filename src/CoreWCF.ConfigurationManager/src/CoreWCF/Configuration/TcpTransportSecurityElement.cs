// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;

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

        //[ConfigurationProperty(ConfigurationStrings.SslProtocols, DefaultValue = TransportDefaults.OldDefaultSslProtocols)]
        //[ServiceModelEnumValidator(typeof(SslProtocolsHelper))]
        //public SslProtocols SslProtocols
        //{
        //    get { return (SslProtocols)base[ConfigurationStrings.SslProtocols]; }
        //    private set { base[ConfigurationStrings.SslProtocols] = value; }
        //}
    }
}
