// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;

namespace CoreWCF.Configuration
{
    public class ServicesSection : ConfigurationSection
    {
        [ConfigurationProperty(ConfigurationStrings.DefaultCollectionName, Options = ConfigurationPropertyOptions.IsDefaultCollection)]
        public ServiceElementCollection Services
        {
            get { return (ServiceElementCollection)this[ConfigurationStrings.DefaultCollectionName]; }
        }
    }
}
