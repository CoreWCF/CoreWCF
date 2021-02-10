// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text;
using CoreWCF.Channels;
using CoreWCF.IdentityModel.Selectors;

namespace CoreWCF.Security.Tokens
{
    public abstract class ServiceModelSecurityTokenRequirement : SecurityTokenRequirement
    {
        protected const string Namespace = "http://schemas.microsoft.com/ws/2006/05/servicemodel/securitytokenrequirement";
        private const string securityAlgorithmSuiteProperty = Namespace + "/SecurityAlgorithmSuite";
        private const string securityBindingElementProperty = Namespace + "/SecurityBindingElement";
        private const string issuerAddressProperty = Namespace + "/IssuerAddress";
        private const string issuerBindingProperty = Namespace + "/IssuerBinding";
        private const string secureConversationSecurityBindingElementProperty = Namespace + "/SecureConversationSecurityBindingElement";
        private const string supportSecurityContextCancellationProperty = Namespace + "/SupportSecurityContextCancellation";
        private const string messageSecurityVersionProperty = Namespace + "/MessageSecurityVersion";
        private const string defaultMessageSecurityVersionProperty = Namespace + "/DefaultMessageSecurityVersion";
        private const string issuerBindingContextProperty = Namespace + "/IssuerBindingContext";
        private const string transportSchemeProperty = Namespace + "/TransportScheme";
        private const string isInitiatorProperty = Namespace + "/IsInitiator";
        private const string targetAddressProperty = Namespace + "/TargetAddress";
        private const string viaProperty = Namespace + "/Via";
        private const string listenUriProperty = Namespace + "/ListenUri";
        private const string auditLogLocationProperty = Namespace + "/AuditLogLocation";
        private const string suppressAuditFailureProperty = Namespace + "/SuppressAuditFailure";
        private const string messageAuthenticationAuditLevelProperty = Namespace + "/MessageAuthenticationAuditLevel";
        private const string isOutOfBandTokenProperty = Namespace + "/IsOutOfBandToken";
        private const string preferSslCertificateAuthenticatorProperty = Namespace + "/PreferSslCertificateAuthenticator";

        // the following properties dont have top level OM properties but are part of the property bag
        private const string supportingTokenAttachmentModeProperty = Namespace + "/SupportingTokenAttachmentMode";
        private const string messageDirectionProperty = Namespace + "/MessageDirection";
        private const string httpAuthenticationSchemeProperty = Namespace + "/HttpAuthenticationScheme";
        private const string issuedSecurityTokenParametersProperty = Namespace + "/IssuedSecurityTokenParameters";
        private const string privacyNoticeUriProperty = Namespace + "/PrivacyNoticeUri";
        private const string privacyNoticeVersionProperty = Namespace + "/PrivacyNoticeVersion";
        private const string duplexClientLocalAddressProperty = Namespace + "/DuplexClientLocalAddress";
        private const string endpointFilterTableProperty = Namespace + "/EndpointFilterTable";
        private const string channelParametersCollectionProperty = Namespace + "/ChannelParametersCollection";
        private const string extendedProtectionPolicy = Namespace + "/ExtendedProtectionPolicy";
        private const bool defaultSupportSecurityContextCancellation = false;

        protected ServiceModelSecurityTokenRequirement()
            : base()
        {
            Properties[SupportSecurityContextCancellationProperty] = defaultSupportSecurityContextCancellation;
        }

        public static string SecurityAlgorithmSuiteProperty { get { return securityAlgorithmSuiteProperty; } }
        public static string SecurityBindingElementProperty { get { return securityBindingElementProperty; } }
        public static string IssuerAddressProperty { get { return issuerAddressProperty; } }
        public static string IssuerBindingProperty { get { return issuerBindingProperty; } }
        public static string SecureConversationSecurityBindingElementProperty { get { return secureConversationSecurityBindingElementProperty; } }
        public static string SupportSecurityContextCancellationProperty { get { return supportSecurityContextCancellationProperty; } }
        public static string MessageSecurityVersionProperty { get { return messageSecurityVersionProperty; } }
        internal static string DefaultMessageSecurityVersionProperty { get { return defaultMessageSecurityVersionProperty; } }
        public static string IssuerBindingContextProperty { get { return issuerBindingContextProperty; } }
        public static string TransportSchemeProperty { get { return transportSchemeProperty; } }
        public static string IsInitiatorProperty { get { return isInitiatorProperty; } }
        public static string TargetAddressProperty { get { return targetAddressProperty; } }
        public static string ViaProperty { get { return viaProperty; } }
        public static string ListenUriProperty { get { return listenUriProperty; } }
        public static string AuditLogLocationProperty { get { return auditLogLocationProperty; } }
        public static string SuppressAuditFailureProperty { get { return suppressAuditFailureProperty; } }
        public static string MessageAuthenticationAuditLevelProperty { get { return messageAuthenticationAuditLevelProperty; } }
        public static string IsOutOfBandTokenProperty { get { return isOutOfBandTokenProperty; } }
        public static string PreferSslCertificateAuthenticatorProperty { get { return preferSslCertificateAuthenticatorProperty; } }

        public static string SupportingTokenAttachmentModeProperty { get { return supportingTokenAttachmentModeProperty; } }
        public static string MessageDirectionProperty { get { return messageDirectionProperty; } }
        public static string HttpAuthenticationSchemeProperty { get { return httpAuthenticationSchemeProperty; } }
        public static string IssuedSecurityTokenParametersProperty { get { return issuedSecurityTokenParametersProperty; } }
        public static string PrivacyNoticeUriProperty { get { return privacyNoticeUriProperty; } }
        public static string PrivacyNoticeVersionProperty { get { return privacyNoticeVersionProperty; } }
        public static string DuplexClientLocalAddressProperty { get { return duplexClientLocalAddressProperty; } }
        public static string EndpointFilterTableProperty { get { return endpointFilterTableProperty; } }
        public static string ChannelParametersCollectionProperty { get { return channelParametersCollectionProperty; } }
        public static string ExtendedProtectionPolicy { get { return extendedProtectionPolicy; } }

        public bool IsInitiator
        {
            get
            {
                return GetPropertyOrDefault<bool>(IsInitiatorProperty, false);
            }
        }

        public SecurityAlgorithmSuite SecurityAlgorithmSuite
        {
            get
            {
                return GetPropertyOrDefault<SecurityAlgorithmSuite>(SecurityAlgorithmSuiteProperty, null);
            }
            set
            {
                Properties[SecurityAlgorithmSuiteProperty] = value;
            }
        }

        public SecurityBindingElement SecurityBindingElement
        {
            get
            {
                return GetPropertyOrDefault<SecurityBindingElement>(SecurityBindingElementProperty, null);
            }
            set
            {
                Properties[SecurityBindingElementProperty] = value;
            }
        }

        public EndpointAddress IssuerAddress
        {
            get
            {
                return GetPropertyOrDefault<EndpointAddress>(IssuerAddressProperty, null);
            }
            set
            {
                Properties[IssuerAddressProperty] = value;
            }
        }

        public Binding IssuerBinding
        {
            get
            {
                return GetPropertyOrDefault<Binding>(IssuerBindingProperty, null);
            }
            set
            {
                Properties[IssuerBindingProperty] = value;
            }
        }

        public SecurityBindingElement SecureConversationSecurityBindingElement
        {
            get
            {
                return GetPropertyOrDefault<SecurityBindingElement>(SecureConversationSecurityBindingElementProperty, null);
            }
            set
            {
                Properties[SecureConversationSecurityBindingElementProperty] = value;
            }
        }

        public SecurityTokenVersion MessageSecurityVersion
        {
            get
            {
                return GetPropertyOrDefault<SecurityTokenVersion>(MessageSecurityVersionProperty, null);
            }
            set
            {
                Properties[MessageSecurityVersionProperty] = value;
            }
        }

        internal MessageSecurityVersion DefaultMessageSecurityVersion
        {
            get
            {
                return (TryGetProperty<MessageSecurityVersion>(DefaultMessageSecurityVersionProperty, out MessageSecurityVersion messageSecurityVersion)) ? messageSecurityVersion : null;
            }
            set
            {
                Properties[DefaultMessageSecurityVersionProperty] = (object)value;
            }
        }

        public string TransportScheme
        {
            get
            {
                return GetPropertyOrDefault<string>(TransportSchemeProperty, null);
            }
            set
            {
                Properties[TransportSchemeProperty] = value;
            }
        }

        internal bool SupportSecurityContextCancellation
        {
            get
            {
                return GetPropertyOrDefault<bool>(SupportSecurityContextCancellationProperty, defaultSupportSecurityContextCancellation);
            }
            set
            {
                Properties[SupportSecurityContextCancellationProperty] = value;
            }
        }

        internal EndpointAddress DuplexClientLocalAddress
        {
            get
            {
                return GetPropertyOrDefault<EndpointAddress>(duplexClientLocalAddressProperty, null);
            }
            set
            {
                Properties[duplexClientLocalAddressProperty] = value;
            }
        }

        internal TValue GetPropertyOrDefault<TValue>(string propertyName, TValue defaultValue)
        {
            if (!TryGetProperty<TValue>(propertyName, out TValue result))
            {
                result = defaultValue;
            }
            return result;
        }

        internal string InternalToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}:", GetType().ToString()));
            foreach (string propertyName in Properties.Keys)
            {
                object propertyValue = Properties[propertyName];
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "PropertyName: {0}", propertyName));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "PropertyValue: {0}", propertyValue));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "---"));
            }
            return sb.ToString().Trim();
        }
    }
}
