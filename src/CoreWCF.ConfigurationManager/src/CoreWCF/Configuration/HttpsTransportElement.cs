// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public class HttpsTransportElement : HttpTransportElement
    {
        public override void ApplyConfiguration(BindingElement bindingElement)
        {
            base.ApplyConfiguration(bindingElement);
            HttpsTransportBindingElement binding = (HttpsTransportBindingElement)bindingElement;
            binding.RequireClientCertificate = RequireClientCertificate;
        }

        public override Type BindingElementType
        {
            get { return typeof(HttpsTransportBindingElement); }
        }

        public override void CopyFrom(ServiceModelExtensionElement from)
        {
            base.CopyFrom(from);

            HttpsTransportElement source = (HttpsTransportElement)from;
            RequireClientCertificate = source.RequireClientCertificate;
        }

        protected override TransportBindingElement CreateDefaultBindingElement()
        {
            return new HttpsTransportBindingElement();
        }

        protected internal override void InitializeFrom(BindingElement bindingElement)
        {
            base.InitializeFrom(bindingElement);
            HttpsTransportBindingElement binding = (HttpsTransportBindingElement)bindingElement;
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.RequireClientCertificate, binding.RequireClientCertificate);
        }

        [ConfigurationProperty(ConfigurationStrings.RequireClientCertificate, DefaultValue = TransportDefaults.RequireClientCertificate)]
        public bool RequireClientCertificate
        {
            get { return (bool)base[ConfigurationStrings.RequireClientCertificate]; }
            set { base[ConfigurationStrings.RequireClientCertificate] = value; }
        }
    }
}
