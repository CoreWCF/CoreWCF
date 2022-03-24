// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;

namespace CoreWCF.Configuration
{
    internal sealed class IssuedTokenParametersEndpointAddressElement : EndpointAddressElementBase
    {
        public IssuedTokenParametersEndpointAddressElement()
        {
        }

        [ConfigurationProperty(ConfigurationStrings.Binding, DefaultValue = "")]
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

        [ConfigurationProperty(ConfigurationStrings.BindingConfiguration, DefaultValue = "")]
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

        internal void Copy(IssuedTokenParametersEndpointAddressElement source)
        {
            base.Copy(source);
            this.BindingConfiguration = source.BindingConfiguration;
            this.Binding = source.Binding;
        }

    }
}
