// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Security;
using System.Xml;
using CoreWCF.Description;

namespace CoreWCF.Channels
{
    public class HttpsTransportBindingElement : HttpTransportBindingElement, ITransportTokenAssertionProvider
    {
        MessageSecurityVersion _messageSecurityVersion = null;

        public HttpsTransportBindingElement() : base()
        {
            RequireClientCertificate = TransportDefaults.RequireClientCertificate;
        }

        protected HttpsTransportBindingElement(HttpsTransportBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            RequireClientCertificate = elementToBeCloned.RequireClientCertificate;
        }

        private HttpsTransportBindingElement(HttpTransportBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
        }

        public bool RequireClientCertificate { get; set; }

        public override string Scheme
        {
            get { return "https"; }
        }

        public override BindingElement Clone()
        {
            return new HttpsTransportBindingElement(this);
        }

        internal override bool GetSupportsClientAuthenticationImpl(AuthenticationSchemes effectiveAuthenticationSchemes)
        {
            return RequireClientCertificate || base.GetSupportsClientAuthenticationImpl(effectiveAuthenticationSchemes);
        }

        internal override bool GetSupportsClientWindowsIdentityImpl(AuthenticationSchemes effectiveAuthenticationSchemes)
        {
            return RequireClientCertificate || base.GetSupportsClientWindowsIdentityImpl(effectiveAuthenticationSchemes);
        }

        internal static HttpsTransportBindingElement CreateFromHttpBindingElement(HttpTransportBindingElement elementToBeCloned)
        {
            return new HttpsTransportBindingElement(elementToBeCloned);
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }
            if (typeof(T) == typeof(ISecurityCapabilities))
            {
                AuthenticationSchemes effectiveAuthenticationSchemes = GetEffectiveAuthenticationSchemes(AuthenticationScheme,
                    context.BindingParameters);

                return (T)(object)new SecurityCapabilities(GetSupportsClientAuthenticationImpl(effectiveAuthenticationSchemes),
                    true,
                    GetSupportsClientWindowsIdentityImpl(effectiveAuthenticationSchemes),
                    ProtectionLevel.EncryptAndSign,
                    ProtectionLevel.EncryptAndSign);
            }
            else
            {
                return base.GetProperty<T>(context);
            }
        }

        internal override void OnExportPolicy(MetadataExporter exporter, PolicyConversionContext context)
        {
            base.OnExportPolicy(exporter, context);
            SecurityBindingElement.ExportPolicyForTransportTokenAssertionProviders(exporter, context);
            // The below code used to be in ExportPolicyForTransportTokenAssertionProviders but as it can't access this class,
            // it's now been moved inline.
            if (context.BindingElements.Find<TransportSecurityBindingElement>() == null)
            {
                TransportSecurityBindingElement dummyTransportBindingElement = new TransportSecurityBindingElement();
                if (context.BindingElements.Find<SecurityBindingElement>() == null)
                {
                    dummyTransportBindingElement.IncludeTimestamp = false;
                }

                // In order to generate the right sp assertion without SBE.
                // scenario: WSxHttpBinding with SecurityMode.Transport.
                if (_messageSecurityVersion != null)
                {
                    dummyTransportBindingElement.MessageSecurityVersion = _messageSecurityVersion;
                }

                SecurityBindingElement.ExportTransportSecurityBindingElement(dummyTransportBindingElement, this, exporter, context);
            }
        }

        #region ITransportTokenAssertionProvider Members

        public XmlElement GetTransportTokenAssertion()
        {
            return null;
        }

        #endregion
    }
}
