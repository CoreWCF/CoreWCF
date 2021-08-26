// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Configuration;
using CoreWCF.Security;

namespace CoreWCF.Configuration
{
    public class BasicHttpMessageSecurityElement : ServiceModelConfigurationElement
    {
        //[ConfigurationProperty(ConfigurationStrings.ClientCredentialType, DefaultValue = BasicHttpMessageSecurity.DefaultClientCredentialType)]
        //[ServiceModelEnumValidator(typeof(BasicHttpMessageCredentialTypeHelper))]
        //public BasicHttpMessageCredentialType ClientCredentialType
        //{
        //    get { return (BasicHttpMessageCredentialType)base[ConfigurationStrings.ClientCredentialType]; }
        //    set { base[ConfigurationStrings.ClientCredentialType] = value; }
        //}

        [ConfigurationProperty(ConfigurationStrings.AlgorithmSuite, DefaultValue = ConfigurationStrings.Default)]
        [TypeConverter(typeof(SecurityAlgorithmSuiteConverter))]
        public SecurityAlgorithmSuite AlgorithmSuite
        {
            get { return (SecurityAlgorithmSuite)base[ConfigurationStrings.AlgorithmSuite]; }
            set { base[ConfigurationStrings.AlgorithmSuite] = value; }
        }
    }
}
