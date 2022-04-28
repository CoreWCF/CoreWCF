﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Configuration;

namespace CoreWCF.Configuration
{
    public abstract class ServiceModelConfigurationElementCollection<TConfigurationElementType> : ConfigurationElementCollection
        where TConfigurationElementType : ConfigurationElement, new()
    {
        internal ServiceModelConfigurationElementCollection()
            : this(ConfigurationElementCollectionType.AddRemoveClearMap, null)
        { }

        internal ServiceModelConfigurationElementCollection(ConfigurationElementCollectionType collectionType,
            string elementName)
        {
            if (!String.IsNullOrEmpty(elementName))
            {
                AddElementName = elementName;
            }
        }

        internal ServiceModelConfigurationElementCollection(ConfigurationElementCollectionType collectionType,
            string elementName, IComparer comparer) : base(comparer)
        {
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new TConfigurationElementType();
        }
    }
}
