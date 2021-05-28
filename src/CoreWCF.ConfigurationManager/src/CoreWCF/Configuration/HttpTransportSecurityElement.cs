// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;

namespace CoreWCF.Configuration
{
    public class HttpTransportSecurityElement : ServiceModelConfigurationElement
    {
        [ConfigurationProperty(ConfigurationStrings.ClientCredentialType, DefaultValue = HttpClientCredentialType.None)]
        public HttpClientCredentialType ClientCredentialType
        {
            get { return (HttpClientCredentialType)base[ConfigurationStrings.ClientCredentialType]; }
            set { base[ConfigurationStrings.ClientCredentialType] = value; }
        }

        //[ConfigurationProperty(ConfigurationStrings.ProxyCredentialType, DefaultValue = HttpTransportSecurity.DefaultProxyCredentialType)]
        //[ServiceModelEnumValidator(typeof(HttpProxyCredentialTypeHelper))]
        //public HttpProxyCredentialType ProxyCredentialType
        //{
        //    get { return (HttpProxyCredentialType)base[ConfigurationStrings.ProxyCredentialType]; }
        //    set { base[ConfigurationStrings.ProxyCredentialType] = value; }
        //}

        //[ConfigurationProperty(ConfigurationStrings.ExtendedProtectionPolicy)]
        //public ExtendedProtectionPolicyElement ExtendedProtectionPolicy
        //{
        //    get { return (ExtendedProtectionPolicyElement)base[ConfigurationStrings.ExtendedProtectionPolicy]; }
        //    private set { base[ConfigurationStrings.ExtendedProtectionPolicy] = value; }
        //}

        [ConfigurationProperty(ConfigurationStrings.Realm, DefaultValue = "")]
        [StringValidator(MinLength = 0)]
        public string Realm
        {
            get { return (string)base[ConfigurationStrings.Realm]; }
            set
            {
                if (String.IsNullOrEmpty(value))
                {
                    value = String.Empty;
                }
                base[ConfigurationStrings.Realm] = value;
            }
        }
    }
}
