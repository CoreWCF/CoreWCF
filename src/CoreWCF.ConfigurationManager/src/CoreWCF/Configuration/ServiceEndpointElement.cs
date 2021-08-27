// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;

namespace CoreWCF.Configuration
{
    public class ServiceEndpointElement : ConfigurationElement
    {
        [ConfigurationProperty(ConfigurationStrings.Address, DefaultValue = "", Options = ConfigurationPropertyOptions.IsKey)]
        public Uri Address
        {
            get { return (Uri)base[ConfigurationStrings.Address]; }
            set { base[ConfigurationStrings.Address] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.BehaviorConfiguration, DefaultValue = "")]
        [StringValidator(MinLength = 0)]
        public string BehaviorConfiguration
        {
            get { return (string)base[ConfigurationStrings.BehaviorConfiguration]; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    value = string.Empty;
                }
                base[ConfigurationStrings.BehaviorConfiguration] = value;
            }
        }

        [ConfigurationProperty(ConfigurationStrings.Binding, Options = ConfigurationPropertyOptions.IsKey)]
        [StringValidator(MinLength = 0)]
        public string Binding
        {
            get { return (string)base[ConfigurationStrings.Binding]; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    value = string.Empty;
                }
                base[ConfigurationStrings.Binding] = value;
            }
        }

        [ConfigurationProperty(ConfigurationStrings.BindingConfiguration, DefaultValue = "", Options = ConfigurationPropertyOptions.IsKey)]
        [StringValidator(MinLength = 0)]
        public string BindingConfiguration
        {
            get { return (string)base[ConfigurationStrings.BindingConfiguration]; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    value = string.Empty;
                }
                base[ConfigurationStrings.BindingConfiguration] = value;
            }
        }

        [ConfigurationProperty(ConfigurationStrings.Name, DefaultValue = "")]
        [StringValidator(MinLength = 0)]
        public string Name
        {
            get { return (string)base[ConfigurationStrings.Name]; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    value = string.Empty;
                }
                base[ConfigurationStrings.Name] = value;
            }
        }

        [ConfigurationProperty(ConfigurationStrings.BindingName, DefaultValue = "", Options = ConfigurationPropertyOptions.IsKey)]
        [StringValidator(MinLength = 0)]
        public string BindingName
        {
            get { return (string)base[ConfigurationStrings.BindingName]; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    value = string.Empty;
                }
                base[ConfigurationStrings.BindingName] = value;
            }
        }

        [ConfigurationProperty(ConfigurationStrings.BindingNamespace, DefaultValue = "", Options = ConfigurationPropertyOptions.IsKey)]
        [StringValidator(MinLength = 0)]
        public string BindingNamespace
        {
            get { return (string)base[ConfigurationStrings.BindingNamespace]; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    value = string.Empty;
                }
                base[ConfigurationStrings.BindingNamespace] = value;
            }
        }

        [ConfigurationProperty(ConfigurationStrings.Contract, DefaultValue = "", Options = ConfigurationPropertyOptions.IsKey)]
        [StringValidator(MinLength = 0)]
        public string Contract
        {
            get { return (string)base[ConfigurationStrings.Contract]; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    value = string.Empty;
                }
                base[ConfigurationStrings.Contract] = value;
            }
        }

        [ConfigurationProperty(ConfigurationStrings.ListenUri, DefaultValue = null)]
        public Uri ListenUri
        {
            get { return (Uri)base[ConfigurationStrings.ListenUri]; }
            set { base[ConfigurationStrings.ListenUri] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.IsSystemEndpoint, DefaultValue = false)]
        public bool IsSystemEndpoint
        {
            get { return (bool)base[ConfigurationStrings.IsSystemEndpoint]; }
            set { base[ConfigurationStrings.IsSystemEndpoint] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.Kind, DefaultValue = "", Options = ConfigurationPropertyOptions.IsKey)]
        [StringValidator(MinLength = 0)]
        public string Kind
        {
            get { return (string)base[ConfigurationStrings.Kind]; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    value = string.Empty;
                }
                base[ConfigurationStrings.Kind] = value;
            }
        }

        [ConfigurationProperty(ConfigurationStrings.EndpointConfiguration, DefaultValue = "", Options = ConfigurationPropertyOptions.IsKey)]
        [StringValidator(MinLength = 0)]
        public string EndpointConfiguration
        {
            get { return (string)base[ConfigurationStrings.EndpointConfiguration]; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    value = string.Empty;
                }
                base[ConfigurationStrings.EndpointConfiguration] = value;
            }
        }

        internal ServiceEndpoint CreateServiceEndpoint()
        {          
            var endpoint = new ServiceEndpoint()
            {
                Name = Name,
                Address = Address,
                Binding = Binding,
                BindingConfiguration = BindingConfiguration,
                Contract = Contract
            };

            return endpoint;
        }
    }
}
