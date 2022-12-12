// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Net.Security;
using System.Security.Authentication;
using System.Xml;
using CoreWCF.Description;
using CoreWCF.Security;

namespace CoreWCF.Channels
{
    public class SslStreamSecurityBindingElement : StreamUpgradeBindingElement, ITransportTokenAssertionProvider, IPolicyExportExtension
    {
        private IdentityVerifier _identityVerifier;
        private SslProtocols _sslProtocols;

        public SslStreamSecurityBindingElement()
        {
            RequireClientCertificate = TransportDefaults.RequireClientCertificate;
            _sslProtocols = TransportDefaults.SslProtocols;
        }

        protected SslStreamSecurityBindingElement(SslStreamSecurityBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            _identityVerifier = elementToBeCloned._identityVerifier;
            RequireClientCertificate = elementToBeCloned.RequireClientCertificate;
            _sslProtocols = elementToBeCloned._sslProtocols;
        }

        internal IdentityVerifier IdentityVerifier
        {
            get
            {
                if (_identityVerifier == null)
                {
                    _identityVerifier = IdentityVerifier.CreateDefault();
                }

                return _identityVerifier;
            }
            set
            {
                _identityVerifier = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        [DefaultValue(TransportDefaults.RequireClientCertificate)]
        public bool RequireClientCertificate { get; set; }

        [DefaultValue(TransportDefaults.SslProtocols)]
        public SslProtocols SslProtocols
        {
            get
            {
                return _sslProtocols;
            }
            set
            {
                SslProtocolsHelper.Validate(value);
                _sslProtocols = value;
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

        #region ITransportTokenAssertionProvider Members

        public XmlElement GetTransportTokenAssertion()
        {
            XmlDocument document = new XmlDocument();
            XmlElement assertion =
                document.CreateElement(TransportPolicyConstants.DotNetFramingPrefix,
                TransportPolicyConstants.SslTransportSecurityName,
                TransportPolicyConstants.DotNetFramingNamespace);
            if (RequireClientCertificate)
            {
                assertion.AppendChild(document.CreateElement(TransportPolicyConstants.DotNetFramingPrefix,
                    TransportPolicyConstants.RequireClientCertificateName,
                    TransportPolicyConstants.DotNetFramingNamespace));
            }
            return assertion;
        }

        #endregion

        void IPolicyExportExtension.ExportPolicy(MetadataExporter exporter, PolicyConversionContext context)
        {
            if (exporter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(exporter));
            }
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

            SecurityBindingElement.ExportPolicyForTransportTokenAssertionProviders(exporter, context);
        }

        protected override bool IsMatch(BindingElement b)
        {
            if (b == null)
            {
                return false;
            }
            if (!(b is SslStreamSecurityBindingElement ssl))
            {
                return false;
            }

            return RequireClientCertificate == ssl.RequireClientCertificate && _sslProtocols == ssl._sslProtocols;
        }

        private static class TransportPolicyConstants
        {
            public const string DotNetFramingNamespace = Framing.FramingEncodingString.NamespaceUri + "/policy";
            public const string DotNetFramingPrefix = "msf";
            public const string RequireClientCertificateName = "RequireClientCertificate";
            public const string SslTransportSecurityName = "SslTransportSecurity";
        }
    }
}
