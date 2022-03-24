// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Configuration;
using System.Security.Authentication.ExtendedProtection;

namespace CoreWCF.Configuration
{
    public sealed class ExtendedProtectionPolicyElement : ConfigurationElement
    {
        public ExtendedProtectionPolicyElement()
        {
            this.properties.Add(this.policyEnforcement);
            this.properties.Add(this.protectionScenario);
            this.properties.Add(this.customServiceNames);
        }
            
        protected override ConfigurationPropertyCollection Properties
        {
            get { return this.properties; }
        }

        [ConfigurationProperty(ExtendedProtectionConfigurationStrings.PolicyEnforcement)]
        public PolicyEnforcement PolicyEnforcement
        {
            get
            {
                return (PolicyEnforcement)this[this.policyEnforcement];
            }
            set
            {
                this[this.policyEnforcement] = value;
            }
        }

        [ConfigurationProperty(ExtendedProtectionConfigurationStrings.ProtectionScenario, DefaultValue = ProtectionScenario.TransportSelected)]
        public ProtectionScenario ProtectionScenario
        {
            get
            {
                return (ProtectionScenario)this[this.protectionScenario];
            }
            set
            {
                this[this.protectionScenario] = value;
            }
        }

        [ConfigurationProperty(ExtendedProtectionConfigurationStrings.CustomServiceNames)]
        public ServiceNameElementCollection CustomServiceNames
        {
            get
            {
                return (ServiceNameElementCollection)this[this.customServiceNames];
            }
        }

        public ExtendedProtectionPolicy BuildPolicy()
        {
            if (PolicyEnforcement == PolicyEnforcement.Never)
            {
                return new ExtendedProtectionPolicy(PolicyEnforcement.Never);
            }

            ServiceNameCollection spns = null;

            ServiceNameElementCollection spnCollection = CustomServiceNames;
            if (spnCollection != null && spnCollection.Count > 0)
            {
                List<string> spnList = new List<string>(spnCollection.Count);
                foreach (ServiceNameElement element in spnCollection)
                {
                    spnList.Add(element.Name);
                }
                spns = new ServiceNameCollection(spnList);
            }

            return new ExtendedProtectionPolicy(PolicyEnforcement, ProtectionScenario, spns);
        }

        ConfigurationPropertyCollection properties = new ConfigurationPropertyCollection();

        private static PolicyEnforcement DefaultPolicyEnforcement
        {
            get
            {
                return PolicyEnforcement.Never;
            }
        }

        readonly ConfigurationProperty policyEnforcement =
            new ConfigurationProperty(ExtendedProtectionConfigurationStrings.PolicyEnforcement,
                typeof(PolicyEnforcement), DefaultPolicyEnforcement,
                ConfigurationPropertyOptions.None);

        readonly ConfigurationProperty protectionScenario =
            new ConfigurationProperty(ExtendedProtectionConfigurationStrings.ProtectionScenario,
                typeof(ProtectionScenario), ProtectionScenario.TransportSelected,
                ConfigurationPropertyOptions.None);

        readonly ConfigurationProperty customServiceNames =
            new ConfigurationProperty(ExtendedProtectionConfigurationStrings.CustomServiceNames,
                typeof(ServiceNameElementCollection), null,
                ConfigurationPropertyOptions.None);
    }

    internal static class ExtendedProtectionConfigurationStrings
    {
        internal const string ExtendedProtectionPolicy = "extendedProtectionPolicy";
        internal const string PolicyEnforcement = "policyEnforcement";
        internal const string ProtectionScenario = "protectionScenario";
        internal const string CustomServiceNames = "customServiceNames";
        internal const string Name = "name";
    }
}
