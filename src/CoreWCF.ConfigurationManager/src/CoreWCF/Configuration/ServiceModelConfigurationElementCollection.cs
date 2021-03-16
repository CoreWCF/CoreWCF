// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Text;

namespace CoreWCF.Configuration
{
    public abstract class ServiceModelConfigurationElementCollection<ConfigurationElementType> : ConfigurationElementCollection
        where ConfigurationElementType : ConfigurationElement, new()
    {
        internal ServiceModelConfigurationElementCollection()
            : this(ConfigurationElementCollectionType.AddRemoveClearMap, null)
        { }

        internal ServiceModelConfigurationElementCollection(ConfigurationElementCollectionType collectionType,
            string elementName)
        {
            if (!String.IsNullOrEmpty(elementName))
            {
                this.AddElementName = elementName;
            }
        }

        internal ServiceModelConfigurationElementCollection(ConfigurationElementCollectionType collectionType,
            string elementName, IComparer comparer) : base(comparer)
        {
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new ConfigurationElementType();
        }
    }
}
