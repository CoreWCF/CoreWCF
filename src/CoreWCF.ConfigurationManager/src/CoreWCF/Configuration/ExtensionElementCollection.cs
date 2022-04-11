// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;

namespace CoreWCF.Configuration
{
    [ConfigurationCollection(typeof(ExtensionElement), CollectionType = ConfigurationElementCollectionType.BasicMap)]
    public class ExtensionElementCollection : ServiceModelConfigurationElementCollection<ExtensionElement>
    {
        public ExtensionElementCollection()
            : base(ConfigurationElementCollectionType.BasicMap, ConfigurationStrings.Add)
        {
        }

        internal ExtensionElement GetElementExtension(object key)
        {
            return (ExtensionElement)BaseGet(key);
        }
        internal void Add(ConfigurationElement element)
        {
            BaseAdd(element);
        }

        protected override void BaseAdd(ConfigurationElement element)
        {
            if (!InheritedElementExists((ExtensionElement)element))
            {
                EnforceUniqueElement((ExtensionElement)element);
                base.BaseAdd(element);
            }
        }

        protected override void BaseAdd(int index, ConfigurationElement element)
        {
            if (!InheritedElementExists((ExtensionElement)element))
            {
                EnforceUniqueElement((ExtensionElement)element);
                base.BaseAdd(index, element);
            }
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            if (null == element)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
            }

            ExtensionElement configElementKey = (ExtensionElement)element;
            return configElementKey.Name;
        }

        internal bool ContainsKey(object key)
        {
            return BaseGet(key) != null;
        }

        private bool InheritedElementExists(ExtensionElement element)
        {
            // This is logic from ServiceModelEnhancedConfigurationElementCollection
            // The idea is to allow duplicate identical extension definition in different level (i.e. app level and machine level)
            // We however do not allow them on the same level.
            // Identical extension is defined by same name and type.
            object newElementKey = GetElementKey(element);
            if (ContainsKey(newElementKey))
            {
                ExtensionElement oldElement = (ExtensionElement)BaseGet(newElementKey);
                if (oldElement != null)
                {
                    // Is oldElement present in the different level of original config
                    // and name/type matching
                    if (!oldElement.ElementInformation.IsPresent &&
                        element.Type.Equals(oldElement.Type, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void EnforceUniqueElement(ExtensionElement element)
        {
            foreach (ExtensionElement extension in this)
            {
                if (element.Name.Equals(extension.Name, StringComparison.Ordinal))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(
                        string.Format(SR.ConfigDuplicateExtensionName, element.Name)));
                }

                bool foundDuplicateType = false;
                if (element.Type.Equals(extension.Type, StringComparison.OrdinalIgnoreCase))
                {
                    foundDuplicateType = true;
                }
                else if (element.TypeName.Equals(extension.TypeName, StringComparison.Ordinal))
                {
                    Type elementType = Type.GetType(element.Type, false);
                    if (elementType != null && elementType == Type.GetType(extension.Type, false))
                    {
                        foundDuplicateType = true;
                    }
                }

                if (foundDuplicateType)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(
                        string.Format(SR.ConfigDuplicateExtensionType, element.Type)));
                }
            }
        }

        protected override bool ThrowOnDuplicate
        {
            get
            {
                return true;
            }
        }
    }
}
