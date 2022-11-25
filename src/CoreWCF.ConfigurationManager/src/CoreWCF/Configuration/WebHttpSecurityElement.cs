// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;

namespace CoreWCF.Configuration
{
    public class WebHttpSecurityElement : ServiceModelConfigurationElement
    {
        [ConfigurationProperty(ConfigurationStrings.Mode, DefaultValue = WebHttpSecurityMode.None)]
        public WebHttpSecurityMode Mode
        {
            get { return (WebHttpSecurityMode)base[ConfigurationStrings.Mode]; }
            set { base[ConfigurationStrings.Mode] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.Transport)]
        public HttpTransportSecurityElement Transport
        {
            get { return (HttpTransportSecurityElement)base[ConfigurationStrings.Transport]; }
        }

        internal void ApplyConfiguration(WebHttpSecurity security)
        {
            if (security == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(security));
            }

            security.Mode = Mode;
            Transport.ApplyConfiguration(security.Transport);
        }
    }
}
