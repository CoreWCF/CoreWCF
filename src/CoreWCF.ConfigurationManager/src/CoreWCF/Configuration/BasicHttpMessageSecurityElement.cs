// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Configuration;
using CoreWCF.Security;

namespace CoreWCF.Configuration
{
    public class BasicHttpMessageSecurityElement : ServiceModelConfigurationElement
    {
        internal const BasicHttpMessageCredentialType DefaultClientCredentialType = BasicHttpMessageCredentialType.UserName;

        [ConfigurationProperty(ConfigurationStrings.ClientCredentialType, DefaultValue = DefaultClientCredentialType)]
        public BasicHttpMessageCredentialType ClientCredentialType
        {
            get { return (BasicHttpMessageCredentialType)base[ConfigurationStrings.ClientCredentialType]; }
            set { base[ConfigurationStrings.ClientCredentialType] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.AlgorithmSuite, DefaultValue = ConfigurationStrings.Default)]
        [TypeConverter(typeof(SecurityAlgorithmSuiteConverter))]
        public SecurityAlgorithmSuite AlgorithmSuite
        {
            get { return (SecurityAlgorithmSuite)base[ConfigurationStrings.AlgorithmSuite]; }
            set { base[ConfigurationStrings.AlgorithmSuite] = value; }
        }

        internal void ApplyConfiguration(BasicHttpMessageSecurity security)
        {
            if (security == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(security));
            }

            security.ClientCredentialType = ClientCredentialType;

            if (PropertyValueOrigin.Default != ElementInformation.Properties[ConfigurationStrings.AlgorithmSuite].ValueOrigin)
            {
                security.AlgorithmSuite = AlgorithmSuite;
            }
        }
    }
}
