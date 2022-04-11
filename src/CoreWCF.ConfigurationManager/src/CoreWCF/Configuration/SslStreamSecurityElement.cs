// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;
using System.Security.Authentication;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public sealed class SslStreamSecurityElement : BindingElementExtensionElement
    {
        public SslStreamSecurityElement()
        {
        }

        [ConfigurationProperty(
            ConfigurationStrings.RequireClientCertificate, DefaultValue = TransportDefaults.RequireClientCertificate)]
        public bool RequireClientCertificate
        {
            get { return (bool)base[ConfigurationStrings.RequireClientCertificate]; }
            set { base[ConfigurationStrings.RequireClientCertificate] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.SslProtocols, DefaultValue = TransportDefaults.OldDefaultSslProtocols)]
        public SslProtocols SslProtocols
        {
            get { return (SslProtocols)base[ConfigurationStrings.SslProtocols]; }
            private set { base[ConfigurationStrings.SslProtocols] = value; }
        }


        public override void ApplyConfiguration(BindingElement bindingElement)
        {
            base.ApplyConfiguration(bindingElement);
            SslStreamSecurityBindingElement sslBindingElement =
                (SslStreamSecurityBindingElement)bindingElement;
            sslBindingElement.RequireClientCertificate = RequireClientCertificate;
            sslBindingElement.SslProtocols = SslProtocols;
        }

        protected internal override BindingElement CreateBindingElement()
        {
            SslStreamSecurityBindingElement sslBindingElement
                = new SslStreamSecurityBindingElement();

            ApplyConfiguration(sslBindingElement);
            return sslBindingElement;
        }

        public override Type BindingElementType
        {
            get { return typeof(SslStreamSecurityBindingElement); }
        }

        public override void CopyFrom(ServiceModelExtensionElement from)
        {
            base.CopyFrom(from);

            SslStreamSecurityElement source = (SslStreamSecurityElement)from;
            RequireClientCertificate = source.RequireClientCertificate;
            SslProtocols = source.SslProtocols;
        }

        protected internal override void InitializeFrom(BindingElement bindingElement)
        {
            base.InitializeFrom(bindingElement);
            SslStreamSecurityBindingElement sslBindingElement
                = (SslStreamSecurityBindingElement)bindingElement;
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.RequireClientCertificate, sslBindingElement.RequireClientCertificate);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.SslProtocols, sslBindingElement.SslProtocols);
        }
    }
}
