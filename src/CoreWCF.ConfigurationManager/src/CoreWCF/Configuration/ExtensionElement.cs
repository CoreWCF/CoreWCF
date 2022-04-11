// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;

namespace CoreWCF.Configuration
{
    public class ExtensionElement : ConfigurationElement
    {
        private string _typeName;

        public ExtensionElement()
        {
        }

        public ExtensionElement(string name)
            : this()
        {
            if (string.IsNullOrEmpty(name))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            }

            Name = name;
        }

        public ExtensionElement(string name, string type)
            : this(name)
        {
            if (string.IsNullOrEmpty(type))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(type));
            }

            Type = type;
        }

        [ConfigurationProperty(ConfigurationStrings.Name, Options = ConfigurationPropertyOptions.IsRequired | ConfigurationPropertyOptions.IsKey)]
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

        [ConfigurationProperty(ConfigurationStrings.Type, Options = ConfigurationPropertyOptions.IsRequired)]
        public string Type
        {
            get { return (string)base[ConfigurationStrings.Type]; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    value = string.Empty;
                }

                base[ConfigurationStrings.Type] = value;
            }
        }

        internal string TypeName
        {
            get
            {
                if (string.IsNullOrEmpty(_typeName))
                {
                    _typeName = GetTypeName(Type);
                }

                return _typeName;
            }
        }

        internal static string GetTypeName(string fullyQualifiedName)
        {
            string typeName = fullyQualifiedName.Split(',')[0];
            return typeName.Trim();
        }
    }
}
