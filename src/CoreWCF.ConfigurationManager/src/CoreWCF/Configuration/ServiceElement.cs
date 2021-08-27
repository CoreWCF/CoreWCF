// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;

namespace CoreWCF.Configuration
{
    public class ServiceElement : ConfigurationElement
    {
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

        [ConfigurationProperty(ConfigurationStrings.DefaultCollectionName, Options = ConfigurationPropertyOptions.IsDefaultCollection)]
        public ServiceEndpointElementCollection Endpoints
        {
            get { return (ServiceEndpointElementCollection)base[ConfigurationStrings.DefaultCollectionName]; }
        }

        [ConfigurationProperty(ConfigurationStrings.Name, Options = ConfigurationPropertyOptions.IsKey | ConfigurationPropertyOptions.IsRequired)]
        // [StringValidator(MinLength = 1)]
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
    }
}
