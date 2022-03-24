// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;

namespace CoreWCF.Configuration
{
    public sealed class ServiceNameElement : ConfigurationElement
    {
        [ConfigurationProperty(ExtendedProtectionConfigurationStrings.Name, IsKey = true, IsRequired = true)]
        public string Name
        {
            get { return (string)this[ExtendedProtectionConfigurationStrings.Name]; }
            set { this[ExtendedProtectionConfigurationStrings.Name] = value; }
        }

        internal string Key
        {
            get { return this.Name; }
        }

    }
}
