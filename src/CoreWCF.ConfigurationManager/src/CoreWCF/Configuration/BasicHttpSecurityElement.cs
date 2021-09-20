// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public class BasicHttpSecurityElement : ServiceModelConfigurationElement
    {
        [ConfigurationProperty(ConfigurationStrings.Mode, DefaultValue = BasicHttpSecurityMode.None)]
        public BasicHttpSecurityMode Mode
        {
            get { return (BasicHttpSecurityMode)base[ConfigurationStrings.Mode]; }
            set { base[ConfigurationStrings.Mode] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.Transport)]
        public HttpTransportSecurityElement Transport
        {
            get { return (HttpTransportSecurityElement)base[ConfigurationStrings.Transport]; }
        }

        [ConfigurationProperty(ConfigurationStrings.Message)]
        public BasicHttpMessageSecurityElement Message
        {
            get { return (BasicHttpMessageSecurityElement)base[ConfigurationStrings.Message]; }
        }

        internal void ApplyConfiguration(BasicHttpSecurity security)
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
