using CoreWCF.Security;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;

namespace CoreWCF.Channels
{
    public class SslStreamSecurityBindingElement : StreamUpgradeBindingElement
    {
        IdentityVerifier identityVerifier;
        bool requireClientCertificate;
        SslProtocols sslProtocols;

        public SslStreamSecurityBindingElement()
        {
            requireClientCertificate = TransportDefaults.RequireClientCertificate;
            sslProtocols = TransportDefaults.SslProtocols;
        }

        protected SslStreamSecurityBindingElement(SslStreamSecurityBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            identityVerifier = elementToBeCloned.identityVerifier;
            requireClientCertificate = elementToBeCloned.requireClientCertificate;
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
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("value");
                }

                identityVerifier = value;
            }
        }

        [DefaultValue(TransportDefaults.RequireClientCertificate)]
        public bool RequireClientCertificate
        {
            get
            {
                return requireClientCertificate;
            }
            set
            {
                requireClientCertificate = value;
            }
        }

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

        public override IChannelListener<TChannel> BuildChannelListener<TChannel>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("context");
            }

            context.BindingParameters.Add(this);
            return context.BuildInnerChannelListener<TChannel>();
        }

        public override bool CanBuildChannelListener<TChannel>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("context");
            }

            context.BindingParameters.Add(this);
            return context.CanBuildInnerChannelListener<TChannel>();
        }

        public override BindingElement Clone()
        {
            return new SslStreamSecurityBindingElement(this);
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("context");
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

            return requireClientCertificate == ssl.requireClientCertificate && sslProtocols == ssl.sslProtocols;
        }
    }
}
