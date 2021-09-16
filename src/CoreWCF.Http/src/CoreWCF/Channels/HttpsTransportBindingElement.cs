// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Net.Security;

namespace CoreWCF.Channels
{
    public class HttpsTransportBindingElement : HttpTransportBindingElement
    {
        private MessageSecurityVersion _messageSecurityVersion;

        public HttpsTransportBindingElement() : base()
        {
            RequireClientCertificate = TransportDefaults.RequireClientCertificate;
        }

        protected HttpsTransportBindingElement(HttpsTransportBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            RequireClientCertificate = elementToBeCloned.RequireClientCertificate;
            _messageSecurityVersion = elementToBeCloned.MessageSecurityVersion;
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

        internal MessageSecurityVersion MessageSecurityVersion
        {
            get
            {
                return _messageSecurityVersion;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
                }
                _messageSecurityVersion = value;
            }
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
    }
}
