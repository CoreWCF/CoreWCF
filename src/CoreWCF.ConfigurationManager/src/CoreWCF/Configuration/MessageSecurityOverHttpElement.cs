// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Configuration;
using CoreWCF.Security;

namespace CoreWCF.Configuration
{
    public class MessageSecurityOverHttpElement : ServiceModelConfigurationElement
    {
        [ConfigurationProperty(ConfigurationStrings.ClientCredentialType, DefaultValue = MessageCredentialType.Windows)]
        public MessageCredentialType ClientCredentialType
        {
            get { return (MessageCredentialType)base[ConfigurationStrings.ClientCredentialType]; }
            set { base[ConfigurationStrings.ClientCredentialType] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.NegotiateServiceCredential, DefaultValue = true)]
        public bool NegotiateServiceCredential
        {
            get { return (bool)base[ConfigurationStrings.NegotiateServiceCredential]; }
            set { base[ConfigurationStrings.NegotiateServiceCredential] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.AlgorithmSuite, DefaultValue = ConfigurationStrings.Default)]
        [TypeConverter(typeof(SecurityAlgorithmSuiteConverter))]
        public SecurityAlgorithmSuite AlgorithmSuite
        {
            get { return (SecurityAlgorithmSuite)base[ConfigurationStrings.AlgorithmSuite]; }
            set { base[ConfigurationStrings.AlgorithmSuite] = value; }
        }

        internal void ApplyConfiguration(MessageSecurityOverHttp security)
        {
            if (security == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(security));
            }

            security.ClientCredentialType = ClientCredentialType;
            security.NegotiateServiceCredential = NegotiateServiceCredential;

            if (PropertyValueOrigin.Default != ElementInformation.Properties[ConfigurationStrings.AlgorithmSuite].ValueOrigin)
            {
                security.AlgorithmSuite = AlgorithmSuite;
            }
        }
    }
}
