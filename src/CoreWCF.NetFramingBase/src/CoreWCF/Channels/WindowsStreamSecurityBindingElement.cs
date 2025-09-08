// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Security;
using System.Security.Principal;
using System.Xml;
using CoreWCF.Description;
using CoreWCF.Security;

namespace CoreWCF.Channels
{
    public class WindowsStreamSecurityBindingElement : StreamUpgradeBindingElement, ITransportTokenAssertionProvider, IPolicyExportExtension
    {
        private ProtectionLevel _protectionLevel;

        public WindowsStreamSecurityBindingElement()
            : base()
        {
            _protectionLevel = ConnectionOrientedTransportDefaults.ProtectionLevel;
        }

        protected WindowsStreamSecurityBindingElement(WindowsStreamSecurityBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            _protectionLevel = elementToBeCloned._protectionLevel;
        }

        public ProtectionLevel ProtectionLevel
        {
            get
            {
                return _protectionLevel;
            }
            set
            {
                ProtectionLevelHelper.Validate(value);
                _protectionLevel = value;
            }
        }

        public override BindingElement Clone()
        {
            return new WindowsStreamSecurityBindingElement(this);
        }

        public override StreamUpgradeProvider BuildServerStreamUpgradeProvider(BindingContext context)
        {
            return new WindowsStreamSecurityUpgradeProvider(this, context);
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }
            if (typeof(T) == typeof(ISecurityCapabilities))
            {
                return (T)(object)new SecurityCapabilities(true, true, true, _protectionLevel, _protectionLevel);
            }
            else if (typeof(T) == typeof(IdentityVerifier))
            {
                return (T)(object)IdentityVerifier.CreateDefault();
            }
            else
            {
                return context.GetInnerProperty<T>();
            }
        }

        #region ITransportTokenAssertionProvider Members

        public XmlElement GetTransportTokenAssertion()
        {
            XmlDocument document = new XmlDocument();
            XmlElement assertion =
                document.CreateElement(TransportPolicyConstants.DotNetFramingPrefix,
                TransportPolicyConstants.WindowsTransportSecurityName,
                TransportPolicyConstants.DotNetFramingNamespace);
            XmlElement protectionLevelElement = document.CreateElement(TransportPolicyConstants.DotNetFramingPrefix,
                TransportPolicyConstants.ProtectionLevelName, TransportPolicyConstants.DotNetFramingNamespace);
            protectionLevelElement.AppendChild(document.CreateTextNode(this.ProtectionLevel.ToString()));
            assertion.AppendChild(protectionLevelElement);
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
    }
}
