// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Net.Security;
using System.Security.Authentication;
using CoreWCF.Security;

namespace CoreWCF.Channels
{
    public class SslStreamSecurityBindingElement : StreamUpgradeBindingElement
    {
        private IdentityVerifier identityVerifier;
        private SslProtocols sslProtocols;

        public SslStreamSecurityBindingElement()
        {
            RequireClientCertificate = TransportDefaults.RequireClientCertificate;
            sslProtocols = TransportDefaults.SslProtocols;
        }

        protected SslStreamSecurityBindingElement(SslStreamSecurityBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            identityVerifier = elementToBeCloned.identityVerifier;
            RequireClientCertificate = elementToBeCloned.RequireClientCertificate;
            sslProtocols = elementToBeCloned.sslProtocols;
        }

        internal IdentityVerifier IdentityVerifier
        {
            get
            {
                if (identityVerifier == null)
                {
                    identityVerifier = IdentityVerifier.CreateDefault();
                }

                return identityVerifier;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                identityVerifier = value;
            }
        }

        [DefaultValue(TransportDefaults.RequireClientCertificate)]
        public bool RequireClientCertificate { get; set; }

        [DefaultValue(TransportDefaults.SslProtocols)]
        public SslProtocols SslProtocols
        {
            get
            {
                return sslProtocols;
            }
            set
            {
                SslProtocolsHelper.Validate(value);
                sslProtocols = value;
            }
        }

        public override BindingElement Clone()
        {
            return new SslStreamSecurityBindingElement(this);
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }
            if (typeof(T) == typeof(ISecurityCapabilities))
            {
                return (T)(object)new SecurityCapabilities(RequireClientCertificate, true, RequireClientCertificate,
                    ProtectionLevel.EncryptAndSign, ProtectionLevel.EncryptAndSign);
            }
            else if (typeof(T) == typeof(IdentityVerifier))
            {
                return (T)(object)IdentityVerifier;
            }
            else
            {
                return context.GetInnerProperty<T>();
            }
        }

        public override StreamUpgradeProvider BuildServerStreamUpgradeProvider(BindingContext context)
        {
            return SslStreamSecurityUpgradeProvider.CreateServerProvider(this, context);
        }

        protected override bool IsMatch(BindingElement b)
        {
            if (b == null)
            {
                return false;
            }
            SslStreamSecurityBindingElement ssl = b as SslStreamSecurityBindingElement;
            if (ssl == null)
            {
                return false;
            }

            return RequireClientCertificate == ssl.RequireClientCertificate && sslProtocols == ssl.sslProtocols;
        }
    }
}
