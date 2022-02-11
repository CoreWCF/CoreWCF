// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Net.Security;
using System.Xml;
using CoreWCF.Description;

namespace CoreWCF.Channels
{
    public class HttpsTransportBindingElement : HttpTransportBindingElement, ITransportTokenAssertionProvider
    {
        private MessageSecurityVersion _messageSecurityVersion = null;
        private XmlElement _transportTokenAssertion;

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
            var tsbe = context.BindingElements.Find<TransportSecurityBindingElement>();
            if (tsbe != null)
            {
                _transportTokenAssertion = CreateTransportTokenAssertion(tsbe.MessageSecurityVersion.SecurityPolicyVersion, exporter);
                SecurityBindingElement.ExportPolicyForTransportTokenAssertionProviders(exporter, context);
                _transportTokenAssertion = null;
            }
            else
            {
                // The below code used to be in ExportPolicyForTransportTokenAssertionProviders but as it can't access this class,
                // it's now been moved inline. This is the code that used to be executed before calling ExportTransportSecurityBindingElement
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

                _transportTokenAssertion = CreateTransportTokenAssertion(dummyTransportBindingElement.MessageSecurityVersion.SecurityPolicyVersion, exporter);
                SecurityBindingElement.ExportTransportSecurityBindingElement(dummyTransportBindingElement, this, exporter, context);
                _transportTokenAssertion = null;
            }
        }

        private XmlElement CreateTransportTokenAssertion(Security.SecurityPolicyVersion securityPolicyVersion, MetadataExporter exporter)
        {
            XmlElement assertion = null;
            if (securityPolicyVersion == Security.SecurityPolicyVersion.WSSecurityPolicy11)
            {
                assertion = CreateWsspAssertion(securityPolicyVersion, "HttpsToken"); // WSSecurityPolicy.HttpsTokenName
                assertion.SetAttribute("RequireClientCertificate", // WSSecurityPolicy.RequireClientCertificateName
                                    RequireClientCertificate ? "true" : "false");
            }
            else if (securityPolicyVersion == Security.SecurityPolicyVersion.WSSecurityPolicy12)
            {
                assertion = CreateWsspAssertion(securityPolicyVersion, "HttpsToken"); // WSSecurityPolicy.HttpsTokenName
                if (RequireClientCertificate ||
                    AuthenticationScheme == AuthenticationSchemes.Basic ||
                    AuthenticationScheme == AuthenticationSchemes.Digest)
                {
                    var doc = new XmlDocument();
                    XmlElement policy = doc.CreateElement("wsp", // WspPrefix
                                                          "Policy", // PolicyName
                                                          exporter.PolicyVersion.Namespace);
                    if (RequireClientCertificate)
                    {
                        policy.AppendChild(CreateWsspAssertion(securityPolicyVersion, "RequireClientCertificate"));
                    }
                    if (AuthenticationScheme == AuthenticationSchemes.Basic)
                    {
                        policy.AppendChild(CreateWsspAssertion(securityPolicyVersion, "HttpBasicAuthentication"));
                    }
                    else if (AuthenticationScheme == AuthenticationSchemes.Digest)
                    {
                        policy.AppendChild(CreateWsspAssertion(securityPolicyVersion, "HttpDigestAuthentication"));
                    }
                    assertion.AppendChild(policy);
                }
            }

            return assertion;
        }

        private XmlElement CreateWsspAssertion(Security.SecurityPolicyVersion policyVersion, string name)
        {
            string policyNamespace = string.Empty;
            if (policyVersion == Security.SecurityPolicyVersion.WSSecurityPolicy11)
            {
                policyNamespace = @"http://schemas.xmlsoap.org/ws/2005/07/securitypolicy"; //WSSecurityPolicy11.WsspNamespaceUri
            }
            else if(policyVersion == Security.SecurityPolicyVersion.WSSecurityPolicy12)
            {
                policyNamespace = @"http://docs.oasis-open.org/ws-sx/ws-securitypolicy/200702"; //WSSecurityPolicy12.WsspNamespaceUri
            }

            var doc = new XmlDocument();
            XmlElement result = doc.CreateElement("sp", // WSSecurityPolicy.WsspPrefix
                                                  name,
                                                  policyNamespace);
            return result;
        }

        #region ITransportTokenAssertionProvider Members

        public XmlElement GetTransportTokenAssertion()
        {
            return _transportTokenAssertion;
        }

        #endregion
    }
}
