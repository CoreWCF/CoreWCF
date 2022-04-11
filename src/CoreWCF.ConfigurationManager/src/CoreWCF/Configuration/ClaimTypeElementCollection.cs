// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;

namespace CoreWCF.Configuration
{
    [ConfigurationCollection(typeof(ClaimTypeElement))]
    public sealed class ClaimTypeElementCollection : ServiceModelConfigurationElementCollection<ClaimTypeElement>
    {
        protected override object GetElementKey(ConfigurationElement element)
        {
            if (element == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
            }
            ClaimTypeElement claimElement = (ClaimTypeElement)element;
            return claimElement.ClaimType;
        }
    }
}
