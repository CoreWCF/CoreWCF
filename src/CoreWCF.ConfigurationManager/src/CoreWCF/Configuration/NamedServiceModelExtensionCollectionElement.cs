﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;

namespace CoreWCF.Configuration
{
    public abstract class NamedServiceModelExtensionCollectionElement<TServiceModelExtensionElement> : ServiceModelExtensionCollectionElement<TServiceModelExtensionElement>
        where TServiceModelExtensionElement : ServiceModelExtensionElement
    {
        private ConfigurationPropertyCollection _properties = null;

        protected NamedServiceModelExtensionCollectionElement(string extensionCollectionName, string name)
            : base(extensionCollectionName)
        {
            if (!string.IsNullOrEmpty(name))
            {
                Name = name;
            }
            else
            {
                Name = string.Empty;
            }
        }

        [ConfigurationProperty(ConfigurationStrings.Name, Options = ConfigurationPropertyOptions.IsKey)]
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

        protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                if (_properties == null)
                {
                    _properties = base.Properties;
                    _properties.Add(new ConfigurationProperty(ConfigurationStrings.Name, typeof(string), null, null, new StringValidator(0, 2147483647, null), System.Configuration.ConfigurationPropertyOptions.IsKey));
                }
                return _properties;
            }
        }
    }
}
