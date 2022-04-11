// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Configuration
{
    public sealed class ClaimTypeElement : ConfigurationElement
    {
        internal const bool DefaultIsOptional = false;

        public ClaimTypeElement() { }

        public ClaimTypeElement(string claimType, bool isOptional)
        {
            ClaimType = claimType;
            IsOptional = isOptional;
        }

        [ConfigurationProperty(ConfigurationStrings.ClaimType, DefaultValue = "", Options = ConfigurationPropertyOptions.IsRequired | ConfigurationPropertyOptions.IsKey)]
        [StringValidator(MinLength = 0)]
        public string ClaimType
        {
            get { return (string)base[ConfigurationStrings.ClaimType]; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    value = string.Empty;
                }
                base[ConfigurationStrings.ClaimType] = value;
            }
        }

        [ConfigurationProperty(ConfigurationStrings.IsOptional, DefaultValue = DefaultIsOptional)]
        public bool IsOptional
        {
            get { return (bool)base[ConfigurationStrings.IsOptional]; }
            set { base[ConfigurationStrings.IsOptional] = value; }
        }
    }
}
