// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using CoreWCF.Description;
namespace CoreWCF.Security
{
    internal abstract class WSSecurityPolicy
    {
        public static ContractDescription NullContract = new ContractDescription("null");
        public static ServiceEndpoint NullServiceEndpoint = new ServiceEndpoint(NullContract);
        public static XmlDocument doc = new XmlDocument();
        public const string WsspPrefix = "sp";
        public const string WspNamespace = MetadataStrings.WSPolicy.NamespaceUri; //@"http://schemas.xmlsoap.org/ws/2004/09/policy";
        public const string Wsp15Namespace = MetadataStrings.WSPolicy.NamespaceUri15;
        public const string WspPrefix = MetadataStrings.WSPolicy.Prefix; //"wsp";
        public const string MsspNamespace = @"http://schemas.microsoft.com/ws/2005/07/securitypolicy";
        public const string MsspPrefix = "mssp";
        public const string PolicyName = MetadataStrings.WSPolicy.Elements.Policy; //"Policy";
        public const string OptionalName = "Optional";
        public const string TrueName = "true";
        public const string FalseName = "false";
        public const string SymmetricBindingName = "SymmetricBinding";
        public const string AsymmetricBindingName = "AsymmetricBinding";
        public const string TransportBindingName = "TransportBinding";
        public const string OnlySignEntireHeadersAndBodyName = "OnlySignEntireHeadersAndBody";
        public const string ProtectionTokenName = "ProtectionToken";
        public const string InitiatorTokenName = "InitiatorToken";
        public const string RecipientTokenName = "RecipientToken";
        public const string TransportTokenName = "TransportToken";
        public const string AlgorithmSuiteName = "AlgorithmSuite";
        public const string LaxName = "Lax";
        public const string LaxTsLastName = "LaxTsLast";
        public const string LaxTsFirstName = "LaxTsFirst";
        public const string StrictName = "Strict";
        public const string IncludeTimestampName = "IncludeTimestamp";
        public const string EncryptBeforeSigningName = "EncryptBeforeSigning";
        public const string ProtectTokens = "ProtectTokens";
        public const string EncryptSignatureName = "EncryptSignature";
        public const string SignedSupportingTokensName = "SignedSupportingTokens";
        public const string EndorsingSupportingTokensName = "EndorsingSupportingTokens";
        public const string SignedEndorsingSupportingTokensName = "SignedEndorsingSupportingTokens";
        public const string Wss10Name = "Wss10";
        public const string MustSupportRefKeyIdentifierName = "MustSupportRefKeyIdentifier";
        public const string MustSupportRefIssuerSerialName = "MustSupportRefIssuerSerial";
        public const string MustSupportRefThumbprintName = "MustSupportRefThumbprint";
        public const string MustSupportRefEncryptedKeyName = "MustSupportRefEncryptedKey";
        public const string RequireSignatureConfirmationName = "RequireSignatureConfirmation";
        public const string MustSupportIssuedTokensName = "MustSupportIssuedTokens";
        public const string RequireClientEntropyName = "RequireClientEntropy";
        public const string RequireServerEntropyName = "RequireServerEntropy";
        public const string Wss11Name = "Wss11";
        public const string Trust10Name = "Trust10";
        public const string Trust13Name = "Trust13";
        public const string RequireAppliesTo = "RequireAppliesTo";
        public const string SignedPartsName = "SignedParts";
        public const string EncryptedPartsName = "EncryptedParts";
        public const string BodyName = "Body";
        public const string HeaderName = "Header";
        public const string NameName = "Name";
        public const string NamespaceName = "Namespace";
        public const string Basic128Name = "Basic128";
        public const string Basic192Name = "Basic192";
        public const string Basic256Name = "Basic256";
        public const string TripleDesName = "TripleDes";
        public const string Basic128Rsa15Name = "Basic128Rsa15";
        public const string Basic192Rsa15Name = "Basic192Rsa15";
        public const string Basic256Rsa15Name = "Basic256Rsa15";
        public const string TripleDesRsa15Name = "TripleDesRsa15";
        public const string Basic128Sha256Name = "Basic128Sha256";
        public const string Basic192Sha256Name = "Basic192Sha256";
        public const string Basic256Sha256Name = "Basic256Sha256";
        public const string TripleDesSha256Name = "TripleDesSha256";
        public const string Basic128Sha256Rsa15Name = "Basic128Sha256Rsa15";
        public const string Basic192Sha256Rsa15Name = "Basic192Sha256Rsa15";
        public const string Basic256Sha256Rsa15Name = "Basic256Sha256Rsa15";
        public const string TripleDesSha256Rsa15Name = "TripleDesSha256Rsa15";
        public const string IncludeTokenName = "IncludeToken";
        public const string KerberosTokenName = "KerberosToken";
        public const string X509TokenName = "X509Token";
        public const string IssuedTokenName = "IssuedToken";
        public const string UsernameTokenName = "UsernameToken";
        public const string RsaTokenName = "RsaToken";
        public const string KeyValueTokenName = "KeyValueToken";
        public const string SpnegoContextTokenName = "SpnegoContextToken";
        public const string SslContextTokenName = "SslContextToken";
        public const string SecureConversationTokenName = "SecureConversationToken";
        public const string WssGssKerberosV5ApReqToken11Name = "WssGssKerberosV5ApReqToken11";
        public const string RequireDerivedKeysName = "RequireDerivedKeys";
        public const string RequireIssuerSerialReferenceName = "RequireIssuerSerialReference";
        public const string RequireKeyIdentifierReferenceName = "RequireKeyIdentifierReference";
        public const string RequireThumbprintReferenceName = "RequireThumbprintReference";
        public const string WssX509V3Token10Name = "WssX509V3Token10";
        public const string WssUsernameToken10Name = "WssUsernameToken10";
        public const string RequestSecurityTokenTemplateName = "RequestSecurityTokenTemplate";
        public const string RequireExternalReferenceName = "RequireExternalReference";
        public const string RequireInternalReferenceName = "RequireInternalReference";
        public const string IssuerName = "Issuer";
        public const string RequireClientCertificateName = "RequireClientCertificate";
        public const string MustNotSendCancelName = "MustNotSendCancel";
        public const string MustNotSendAmendName = "MustNotSendAmend";
        public const string MustNotSendRenewName = "MustNotSendRenew";
        public const string LayoutName = "Layout";
        public const string BootstrapPolicyName = "BootstrapPolicy";
        public const string HttpsTokenName = "HttpsToken";
        public const string HttpBasicAuthenticationName = "HttpBasicAuthentication";
        public const string HttpDigestAuthenticationName = "HttpDigestAuthentication";
        private readonly bool _mustSupportRefKeyIdentifierName = false;
        private readonly bool _mustSupportRefIssuerSerialName = false;
        private readonly bool _mustSupportRefThumbprintName = false;
        private readonly bool _protectionTokenHasAsymmetricKey = false;
    }

    internal static class SecurityPolicyStrings
    {
        public const string SecureConversationBootstrapBindingElementsBelowSecurityKey = "SecureConversationBootstrapBindingElementsBelowSecurityKey";
    }

}
