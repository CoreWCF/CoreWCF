// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;

namespace CoreWCF.Configuration
{
    public abstract class ServiceModelEnhancedConfigurationElementCollection<TConfigurationElement> : ServiceModelConfigurationElementCollection<TConfigurationElement>
        where TConfigurationElement : ConfigurationElement, new()
    {
        internal ServiceModelEnhancedConfigurationElementCollection(string elementName)
            : base(ConfigurationElementCollectionType.AddRemoveClearMap, elementName)
        {
            this.AddElementName = elementName;
        }
    }
}
