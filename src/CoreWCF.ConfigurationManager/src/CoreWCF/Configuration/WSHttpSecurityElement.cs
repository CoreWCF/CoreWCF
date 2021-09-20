// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;

namespace CoreWCF.Configuration
{
    public class WSHttpSecurityElement : ServiceModelConfigurationElement
    {
        [ConfigurationProperty(ConfigurationStrings.Mode, DefaultValue = SecurityMode.Message)]
        public SecurityMode Mode
        {
            get { return (SecurityMode)base[ConfigurationStrings.Mode]; }
            set { base[ConfigurationStrings.Mode] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.Transport)]
        public WSHttpTransportSecurityElement Transport
        {
            get { return (WSHttpTransportSecurityElement)base[ConfigurationStrings.Transport]; }
        }

        [ConfigurationProperty(ConfigurationStrings.Message)]
        public NonDualMessageSecurityOverHttpElement Message
        {
            get { return (NonDualMessageSecurityOverHttpElement)base[ConfigurationStrings.Message]; }
        }

        internal void ApplyConfiguration(WSHTTPSecurity security)
        {
            if (security == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(security));
            }

            security.Mode = Mode;
            Transport.ApplyConfiguration(security.Transport);
            Message.ApplyConfiguration(security.Message);
        }
    }
}
