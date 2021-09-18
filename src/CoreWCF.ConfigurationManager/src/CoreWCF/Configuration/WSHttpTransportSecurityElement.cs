// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;

namespace CoreWCF.Configuration
{
    public class WSHttpTransportSecurityElement : ServiceModelConfigurationElement
    {
        [ConfigurationProperty(ConfigurationStrings.ClientCredentialType, DefaultValue = HttpClientCredentialType.Windows)]
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
                if (string.IsNullOrEmpty(value))
                {
                    value = string.Empty;
                }
                base[ConfigurationStrings.Realm] = value;
            }
        }

        internal void ApplyConfiguration(HttpTransportSecurity security)
        {
            if (security == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("security");
            }

            security.ClientCredentialType = ClientCredentialType;
           // security.ProxyCredentialType = this.ProxyCredentialType;
            security.Realm = Realm;
           // security.ExtendedProtectionPolicy = ChannelBindingUtility.BuildPolicy(this.ExtendedProtectionPolicy);
        }
    }
}
