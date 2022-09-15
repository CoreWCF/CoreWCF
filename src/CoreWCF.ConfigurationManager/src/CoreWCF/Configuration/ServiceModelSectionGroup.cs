// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;

namespace CoreWCF.Configuration
{
    public sealed class ServiceModelSectionGroup : ConfigurationSectionGroup
    {
        public ServiceModelSectionGroup()
        {
        }

        public BindingsSection Bindings
        {
            get { return (BindingsSection)Sections[ConfigurationStrings.BindingsSectionGroupName]; }
        }

        public ServicesSection Services
        {
            get { return (ServicesSection)Sections[ConfigurationStrings.ServicesSectionName]; }
        }


        public static ServiceModelSectionGroup GetSectionGroup(System.Configuration.Configuration config)
        {
            if (config == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(config));
            }

            return (ServiceModelSectionGroup)config.SectionGroups[ConfigurationStrings.SectionGroupName];
        }
    }
}
