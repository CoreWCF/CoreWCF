// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Security.Tokens;

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

        bool _mustSupportRefKeyIdentifierName = false;
        bool _mustSupportRefIssuerSerialName = false;
        bool _mustSupportRefThumbprintName = false;
        bool _protectionTokenHasAsymmetricKey = false;

        public virtual XmlElement CreateWsspAssertion(string name)
        {
            return doc.CreateElement(WsspPrefix, name, WsspNamespaceUri);
        }

        public virtual bool IsWsspAssertion(XmlElement assertion)
        {
            return assertion.NamespaceURI == WsspNamespaceUri;
        }

        public virtual XmlElement CreateMsspAssertion(string name)
        {
            return doc.CreateElement(MsspPrefix, name, MsspNamespace);
        }

        public abstract bool IsSecurityVersionSupported(MessageSecurityVersion version);

        public abstract string WsspNamespaceUri { get; }
        public abstract TrustDriver TrustDriver { get; }
        public virtual string AlwaysToRecipientUri => WsspNamespaceUri + @"/IncludeToken/AlwaysToRecipient";
        public virtual string NeverUri => WsspNamespaceUri + @"/IncludeToken/Never";
        public virtual string OnceUri => WsspNamespaceUri + @"/IncludeToken/Once";
        public virtual string AlwaysToInitiatorUri => WsspNamespaceUri + @"/IncludeToken/AlwaysToInitiator";

        public virtual XmlElement CreateWspPolicyWrapper(MetadataExporter exporter, params XmlElement[] nestedAssertions)
        {
            XmlElement result = doc.CreateElement(WspPrefix, PolicyName, exporter.PolicyVersion.Namespace);

            if (nestedAssertions != null)
            {
                foreach (XmlElement e in nestedAssertions)
                {
                    if (e != null)
                    {
                        result.AppendChild(e);
                    }
                }
            }

            return result;
        }

        public virtual XmlElement CreateWsspSignedPartsAssertion(MessagePartSpecification parts)
        {
            if (parts == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parts));
            }

            XmlElement result;

            if (parts.IsEmpty())
            {
                result = null;
            }
            else
            {
                result = CreateWsspAssertion(SignedPartsName);
                if (parts.IsBodyIncluded)
                {
                    result.AppendChild(CreateWsspAssertion(BodyName));
                }
                foreach (XmlQualifiedName header in parts.HeaderTypes)
                {
                    result.AppendChild(CreateWsspHeaderAssertion(header));
                }
            }
            return result;
        }

        public virtual XmlElement CreateWsspEncryptedPartsAssertion(MessagePartSpecification parts)
        {
            if (parts == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parts));
            }

            XmlElement result;

            if (parts.IsEmpty())
            {
                result = null;
            }
            else
            {
                result = CreateWsspAssertion(EncryptedPartsName);
                if (parts.IsBodyIncluded)
                {
                    result.AppendChild(CreateWsspAssertion(BodyName));
                }
                foreach (XmlQualifiedName header in parts.HeaderTypes)
                {
                    result.AppendChild(CreateWsspHeaderAssertion(header));
                }
            }
            return result;
        }

        public virtual XmlElement CreateWsspHeaderAssertion(XmlQualifiedName header)
        {
            XmlElement result = CreateWsspAssertion(HeaderName);
            result.SetAttribute(NameName, header.Name);
            result.SetAttribute(NamespaceName, header.Namespace);

            return result;
        }

        public virtual XmlElement CreateWsspTransportBindingAssertion(MetadataExporter exporter, TransportSecurityBindingElement binding, XmlElement transportTokenAssertion)
        {
            XmlElement result = CreateWsspAssertion(TransportBindingName);
            result.AppendChild(
                CreateWspPolicyWrapper(
                    exporter,
                    CreateWsspTransportTokenAssertion(exporter, transportTokenAssertion),
                    CreateWsspAlgorithmSuiteAssertion(exporter, binding.DefaultAlgorithmSuite),
                    CreateWsspLayoutAssertion(exporter, binding.SecurityHeaderLayout),
                    CreateWsspIncludeTimestampAssertion(binding.IncludeTimestamp)
            ));

            return result;
        }

        public virtual XmlElement CreateWsspWssAssertion(MetadataExporter exporter, SecurityBindingElement binding)
        {
            if (binding == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(binding));
            }

            if (binding.MessageSecurityVersion.SecurityVersion == SecurityVersion.WSSecurity10)
            {
                return CreateWsspWss10Assertion(exporter);
            }
            else if (binding.MessageSecurityVersion.SecurityVersion == SecurityVersion.WSSecurity11)
            {
                if (binding is SymmetricSecurityBindingElement)
                {
                    return CreateWsspWss11Assertion(exporter, ((SymmetricSecurityBindingElement)binding).RequireSignatureConfirmation);
                }
                /*else if (binding is AsymmetricSecurityBindingElement)
                {
                    return CreateWsspWss11Assertion(exporter, ((AsymmetricSecurityBindingElement)binding).RequireSignatureConfirmation);
                }*/
                else
                {
                    return CreateWsspWss11Assertion(exporter, false);
                }
            }
            else
            {
                return null;
            }
        }

        public virtual XmlElement CreateWsspWss10Assertion(MetadataExporter exporter)
        {
            XmlElement result = CreateWsspAssertion(Wss10Name);
            result.AppendChild(
                CreateWspPolicyWrapper(
                    exporter,
                    CreateWsspAssertionMustSupportRefKeyIdentifierName(),
                    CreateWsspAssertionMustSupportRefIssuerSerialName()
            ));

            return result;
        }

        public virtual XmlElement CreateWsspWss11Assertion(MetadataExporter exporter, bool requireSignatureConfirmation)
        {
            XmlElement result = CreateWsspAssertion(Wss11Name);
            result.AppendChild(
                CreateWspPolicyWrapper(
                    exporter,
                    CreateWsspAssertionMustSupportRefKeyIdentifierName(),
                    CreateWsspAssertionMustSupportRefIssuerSerialName(),
                    CreateWsspAssertionMustSupportRefThumbprintName(),
                    CreateWsspAssertionMustSupportRefEncryptedKeyName(),
                    CreateWsspRequireSignatureConformationAssertion(requireSignatureConfirmation)
            ));

            return result;
        }

        public virtual XmlElement CreateWsspAssertionMustSupportRefKeyIdentifierName()
        {
            if (_mustSupportRefKeyIdentifierName)
            {
                return CreateWsspAssertion(MustSupportRefKeyIdentifierName);
            }
            else
            {
                return null;
            }
        }

        public virtual XmlElement CreateWsspAssertionMustSupportRefIssuerSerialName()
        {
            if (_mustSupportRefIssuerSerialName)
            {
                return CreateWsspAssertion(MustSupportRefIssuerSerialName);
            }
            else
            {
                return null;
            }
        }

        public virtual XmlElement CreateWsspAssertionMustSupportRefThumbprintName()
        {
            if (_mustSupportRefThumbprintName)
            {
                return CreateWsspAssertion(MustSupportRefThumbprintName);
            }
            else
            {
                return null;
            }
        }

        public virtual XmlElement CreateWsspAssertionMustSupportRefEncryptedKeyName()
        {
            // protectionTokenHasAsymmetricKey is only set to true for a SymmetricBindingElement having an asymmetric key
            if (_protectionTokenHasAsymmetricKey)
            {
                return CreateWsspAssertion(MustSupportRefEncryptedKeyName);
            }
            else
            {
                return null;
            }
        }

        public virtual XmlElement CreateWsspRequireSignatureConformationAssertion(bool requireSignatureConfirmation)
        {
            if (requireSignatureConfirmation)
            {
                return CreateWsspAssertion(RequireSignatureConfirmationName);
            }
            else
            {
                return null;
            }
        }

        public abstract XmlElement CreateWsspTrustAssertion(MetadataExporter exporter, SecurityKeyEntropyMode keyEntropyMode);

        protected XmlElement CreateWsspTrustAssertion(string trustName, MetadataExporter exporter, SecurityKeyEntropyMode keyEntropyMode)
        {
            XmlElement result = CreateWsspAssertion(trustName);
            result.AppendChild(
                CreateWspPolicyWrapper(
                    exporter,
                    CreateWsspAssertion(MustSupportIssuedTokensName),
                    CreateWsspRequireClientEntropyAssertion(keyEntropyMode),
                    CreateWsspRequireServerEntropyAssertion(keyEntropyMode)
            ));

            return result;
        }

        public virtual XmlElement CreateWsspRequireClientEntropyAssertion(SecurityKeyEntropyMode keyEntropyMode)
        {
            if (keyEntropyMode == SecurityKeyEntropyMode.ClientEntropy || keyEntropyMode == SecurityKeyEntropyMode.CombinedEntropy)
            {
                return CreateWsspAssertion(RequireClientEntropyName);
            }
            else
            {
                return null;
            }
        }

        public virtual XmlElement CreateWsspRequireServerEntropyAssertion(SecurityKeyEntropyMode keyEntropyMode)
        {
            if (keyEntropyMode == SecurityKeyEntropyMode.ServerEntropy || keyEntropyMode == SecurityKeyEntropyMode.CombinedEntropy)
            {
                return CreateWsspAssertion(RequireServerEntropyName);
            }
            else
            {
                return null;
            }
        }

        public virtual Collection<XmlElement> CreateWsspSupportingTokensAssertion(MetadataExporter exporter, Collection<SecurityTokenParameters> signed, Collection<SecurityTokenParameters> signedEncrypted, Collection<SecurityTokenParameters> endorsing, Collection<SecurityTokenParameters> signedEndorsing, Collection<SecurityTokenParameters> optionalSigned, Collection<SecurityTokenParameters> optionalSignedEncrypted, Collection<SecurityTokenParameters> optionalEndorsing, Collection<SecurityTokenParameters> optionalSignedEndorsing)
        {
            return CreateWsspSupportingTokensAssertion(exporter, signed, signedEncrypted, endorsing, signedEndorsing, optionalSigned, optionalSignedEncrypted, optionalEndorsing, optionalSignedEndorsing, null);
        }

        public virtual Collection<XmlElement> CreateWsspSupportingTokensAssertion(MetadataExporter exporter, Collection<SecurityTokenParameters> signed, Collection<SecurityTokenParameters> signedEncrypted, Collection<SecurityTokenParameters> endorsing, Collection<SecurityTokenParameters> signedEndorsing, Collection<SecurityTokenParameters> optionalSigned, Collection<SecurityTokenParameters> optionalSignedEncrypted, Collection<SecurityTokenParameters> optionalEndorsing, Collection<SecurityTokenParameters> optionalSignedEndorsing, AddressingVersion addressingVersion)
        {
            Collection<XmlElement> supportingTokenAssertions = new Collection<XmlElement>();

            // Signed Supporting Tokens
            XmlElement supportingTokenAssertion = CreateWsspSignedSupportingTokensAssertion(exporter, signed, signedEncrypted, optionalSigned, optionalSignedEncrypted);
            if (supportingTokenAssertion != null)
                supportingTokenAssertions.Add(supportingTokenAssertion);

            // Endorsing Supporting Tokens.
            supportingTokenAssertion = CreateWsspEndorsingSupportingTokensAssertion(exporter, endorsing, optionalEndorsing, addressingVersion);
            if (supportingTokenAssertion != null)
                supportingTokenAssertions.Add(supportingTokenAssertion);

            // Signed Endorsing Supporting Tokens.
            supportingTokenAssertion = CreateWsspSignedEndorsingSupportingTokensAssertion(exporter, signedEndorsing, optionalSignedEndorsing, addressingVersion);
            if (supportingTokenAssertion != null)
                supportingTokenAssertions.Add(supportingTokenAssertion);

            return supportingTokenAssertions;
        }

        protected XmlElement CreateWsspSignedSupportingTokensAssertion(MetadataExporter exporter, Collection<SecurityTokenParameters> signed, Collection<SecurityTokenParameters> signedEncrypted, Collection<SecurityTokenParameters> optionalSigned, Collection<SecurityTokenParameters> optionalSignedEncrypted)
        {
            XmlElement result;

            if ((signed == null || signed.Count == 0)
                && (signedEncrypted == null || signedEncrypted.Count == 0)
                && (optionalSigned == null || optionalSigned.Count == 0)
                && (optionalSignedEncrypted == null || optionalSignedEncrypted.Count == 0))
            {
                result = null;
            }
            else
            {
                XmlElement policy = CreateWspPolicyWrapper(exporter);

                if (signed != null)
                {
                    foreach (SecurityTokenParameters p in signed)
                    {
                        policy.AppendChild(CreateTokenAssertion(exporter, p));
                    }
                }
                if (signedEncrypted != null)
                {
                    foreach (SecurityTokenParameters p in signedEncrypted)
                    {
                        policy.AppendChild(CreateTokenAssertion(exporter, p));
                    }
                }
                if (optionalSigned != null)
                {
                    foreach (SecurityTokenParameters p in optionalSigned)
                    {
                        policy.AppendChild(CreateTokenAssertion(exporter, p, true));
                    }
                }
                if (optionalSignedEncrypted != null)
                {
                    foreach (SecurityTokenParameters p in optionalSignedEncrypted)
                    {
                        policy.AppendChild(CreateTokenAssertion(exporter, p, true));
                    }
                }

                result = CreateWsspAssertion(SignedSupportingTokensName);
                result.AppendChild(policy);
            }

            return result;
        }

        protected XmlElement CreateWsspEndorsingSupportingTokensAssertion(MetadataExporter exporter, Collection<SecurityTokenParameters> endorsing, Collection<SecurityTokenParameters> optionalEndorsing, AddressingVersion addressingVersion)
        {
            return CreateWsspiSupportingTokensAssertion(exporter, endorsing, optionalEndorsing, addressingVersion, EndorsingSupportingTokensName);
        }

        protected XmlElement CreateWsspSignedEndorsingSupportingTokensAssertion(MetadataExporter exporter, Collection<SecurityTokenParameters> signedEndorsing, Collection<SecurityTokenParameters> optionalSignedEndorsing, AddressingVersion addressingVersion)
        {
            return CreateWsspiSupportingTokensAssertion(exporter, signedEndorsing, optionalSignedEndorsing, addressingVersion, SignedEndorsingSupportingTokensName);
        }

        protected XmlElement CreateWsspiSupportingTokensAssertion(MetadataExporter exporter, Collection<SecurityTokenParameters> endorsing, Collection<SecurityTokenParameters> optionalEndorsing, AddressingVersion addressingVersion, string assertionName)
        {
            XmlElement result;
            bool hasAssymetricKey = false;

            if ((endorsing == null || endorsing.Count == 0)
                && (optionalEndorsing == null || optionalEndorsing.Count == 0))
            {
                result = null;
            }
            else
            {
                XmlElement policy = CreateWspPolicyWrapper(exporter);

                if (endorsing != null)
                {
                    foreach (SecurityTokenParameters p in endorsing)
                    {
                        if (p.HasAsymmetricKey)
                            hasAssymetricKey = true;

                        policy.AppendChild(CreateTokenAssertion(exporter, p));
                    }
                }
                if (optionalEndorsing != null)
                {
                    foreach (SecurityTokenParameters p in optionalEndorsing)
                    {
                        if (p.HasAsymmetricKey)
                            hasAssymetricKey = true;

                        policy.AppendChild(CreateTokenAssertion(exporter, p, true));
                    }
                }
                if (addressingVersion != null && AddressingVersion.None != addressingVersion)
                {
                    // only add assertion to sign the 'To' only if an assymetric key is found
                    if (hasAssymetricKey)
                    {
                        policy.AppendChild(
                            CreateWsspSignedPartsAssertion(
                                new MessagePartSpecification(new XmlQualifiedName(AddressingStrings.To, addressingVersion.Namespace))));
                    }
                }

                result = CreateWsspAssertion(assertionName);
                result.AppendChild(policy);
            }

            return result;
        }

        public virtual XmlElement CreateWsspIncludeTimestampAssertion(bool includeTimestamp)
        {
            if (includeTimestamp)
            {
                return CreateWsspAssertion(IncludeTimestampName);
            }
            else
            {
                return null;
            }
        }

        public virtual XmlElement CreateWsspLayoutAssertion(MetadataExporter exporter, SecurityHeaderLayout layout)
        {
            XmlElement result = CreateWsspAssertion(LayoutName);
            result.AppendChild(
                CreateWspPolicyWrapper(
                    exporter,
                    CreateLayoutAssertion(layout)
            ));

            return result;
        }

        public virtual XmlElement CreateLayoutAssertion(SecurityHeaderLayout layout)
        {
            switch (layout)
            {
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(layout)));
                case SecurityHeaderLayout.Lax:
                    return CreateWsspAssertion(LaxName);
                case SecurityHeaderLayout.LaxTimestampFirst:
                    return CreateWsspAssertion(LaxTsFirstName);
                case SecurityHeaderLayout.LaxTimestampLast:
                    return CreateWsspAssertion(LaxTsLastName);
                case SecurityHeaderLayout.Strict:
                    return CreateWsspAssertion(StrictName);
            }
        }

        public virtual XmlElement CreateWsspAlgorithmSuiteAssertion(MetadataExporter exporter, SecurityAlgorithmSuite suite)
        {
            XmlElement result = CreateWsspAssertion(AlgorithmSuiteName);
            result.AppendChild(
                CreateWspPolicyWrapper(
                    exporter,
                    CreateAlgorithmSuiteAssertion(suite)
            ));

            return result;
        }

        public virtual XmlElement CreateAlgorithmSuiteAssertion(SecurityAlgorithmSuite suite)
        {
            if (suite == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(suite));
            }

            XmlElement result;

            if (suite == SecurityAlgorithmSuite.Basic256)
                result = CreateWsspAssertion(Basic256Name);
            else if (suite == SecurityAlgorithmSuite.Basic192)
                result = CreateWsspAssertion(Basic192Name);
            else if (suite == SecurityAlgorithmSuite.Basic128)
                result = CreateWsspAssertion(Basic128Name);
            else if (suite == SecurityAlgorithmSuite.TripleDes)
                result = CreateWsspAssertion(TripleDesName);
            else if (suite == SecurityAlgorithmSuite.Basic256Rsa15)
                result = CreateWsspAssertion(Basic256Rsa15Name);
            else if (suite == SecurityAlgorithmSuite.Basic192Rsa15)
                result = CreateWsspAssertion(Basic192Rsa15Name);
            else if (suite == SecurityAlgorithmSuite.Basic128Rsa15)
                result = CreateWsspAssertion(Basic128Rsa15Name);
            else if (suite == SecurityAlgorithmSuite.TripleDesRsa15)
                result = CreateWsspAssertion(TripleDesRsa15Name);
            else if (suite == SecurityAlgorithmSuite.Basic256Sha256)
                result = CreateWsspAssertion(Basic256Sha256Name);
            else if (suite == SecurityAlgorithmSuite.Basic192Sha256)
                result = CreateWsspAssertion(Basic192Sha256Name);
            else if (suite == SecurityAlgorithmSuite.Basic128Sha256)
                result = CreateWsspAssertion(Basic128Sha256Name);
            else if (suite == SecurityAlgorithmSuite.TripleDesSha256)
                result = CreateWsspAssertion(TripleDesSha256Name);
            else if (suite == SecurityAlgorithmSuite.Basic256Sha256Rsa15)
                result = CreateWsspAssertion(Basic256Sha256Rsa15Name);
            else if (suite == SecurityAlgorithmSuite.Basic192Sha256Rsa15)
                result = CreateWsspAssertion(Basic192Sha256Rsa15Name);
            else if (suite == SecurityAlgorithmSuite.Basic128Sha256Rsa15)
                result = CreateWsspAssertion(Basic128Sha256Rsa15Name);
            else if (suite == SecurityAlgorithmSuite.TripleDesSha256Rsa15)
                result = CreateWsspAssertion(TripleDesSha256Rsa15Name);
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(suite)));
            }

            return result;
        }

        public virtual XmlElement CreateWsspTransportTokenAssertion(MetadataExporter exporter, XmlElement transportTokenAssertion)
        {
            if (transportTokenAssertion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(transportTokenAssertion));
            }

            XmlElement result = CreateWsspAssertion(TransportTokenName);

            result.AppendChild(
                CreateWspPolicyWrapper(
                    exporter,
                    (XmlElement)(doc.ImportNode(transportTokenAssertion, true))
            ));

            return result;
        }

        public virtual XmlElement CreateTokenAssertion(MetadataExporter exporter, SecurityTokenParameters parameters)
        {
            return CreateTokenAssertion(exporter, parameters, false);
        }

        public virtual XmlElement CreateTokenAssertion(MetadataExporter exporter, SecurityTokenParameters parameters, bool isOptional)
        {
            if (parameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameters));
            }

            XmlElement result;

            /*if (parameters is KerberosSecurityTokenParameters)
            {
                result = CreateWsspKerberosTokenAssertion(exporter, (KerberosSecurityTokenParameters)parameters);
            }
            else */

            if (parameters is X509SecurityTokenParameters)
            {
                result = CreateWsspX509TokenAssertion(exporter, (X509SecurityTokenParameters)parameters);
            }
            else if (parameters is UserNameSecurityTokenParameters)
            {
                result = CreateWsspUsernameTokenAssertion(exporter, (UserNameSecurityTokenParameters)parameters);
            }
            else if (parameters is IssuedSecurityTokenParameters)
            {
                result = CreateWsspIssuedTokenAssertion(exporter, (IssuedSecurityTokenParameters)parameters);
            }
            else if (parameters is SspiSecurityTokenParameters)
            {
                result = CreateWsspSpnegoContextTokenAssertion(exporter, (SspiSecurityTokenParameters)parameters);
            }
            else if (parameters is SslSecurityTokenParameters)
            {
                result = CreateMsspSslContextTokenAssertion(exporter, (SslSecurityTokenParameters)parameters);
            }
            else if (parameters is SecureConversationSecurityTokenParameters)
            {
                result = CreateWsspSecureConversationTokenAssertion(exporter, (SecureConversationSecurityTokenParameters)parameters);
            }
            /*else if (parameters is RsaSecurityTokenParameters) // If this is enabled, CreateWsspRsaTokenAssertion needs to be overriden in WSSecurityPolicy12
            {
                result = CreateWsspRsaTokenAssertion((RsaSecurityTokenParameters)parameters);
            }*/
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(parameters)));
            }

            if (result != null && isOptional)
            {
                result.SetAttribute(OptionalName, exporter.PolicyVersion.Namespace, TrueName);
            }

            return result;
        }

        public virtual void SetIncludeTokenValue(XmlElement tokenAssertion, SecurityTokenInclusionMode inclusionMode)
        {
            switch (inclusionMode)
            {
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("inclusionMode"));
                case SecurityTokenInclusionMode.AlwaysToInitiator:
                    tokenAssertion.SetAttribute(IncludeTokenName, WsspNamespaceUri, AlwaysToInitiatorUri);
                    break;
                case SecurityTokenInclusionMode.AlwaysToRecipient:
                    tokenAssertion.SetAttribute(IncludeTokenName, WsspNamespaceUri, AlwaysToRecipientUri);
                    break;
                case SecurityTokenInclusionMode.Never:
                    tokenAssertion.SetAttribute(IncludeTokenName, WsspNamespaceUri, NeverUri);
                    break;
                case SecurityTokenInclusionMode.Once:
                    tokenAssertion.SetAttribute(IncludeTokenName, WsspNamespaceUri, OnceUri);
                    break;
            }
        }

        public virtual XmlElement CreateWsspRequireDerivedKeysAssertion(bool requireDerivedKeys)
        {
            if (requireDerivedKeys)
            {
                return CreateWsspAssertion(RequireDerivedKeysName);
            }
            else
            {
                return null;
            }
        }

        public virtual XmlElement CreateX509ReferenceStyleAssertion(X509KeyIdentifierClauseType referenceStyle)
        {
            switch (referenceStyle)
            {
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(referenceStyle)));
                case X509KeyIdentifierClauseType.IssuerSerial:
                    _mustSupportRefIssuerSerialName = true;
                    return CreateWsspAssertion(RequireIssuerSerialReferenceName);
                case X509KeyIdentifierClauseType.SubjectKeyIdentifier:
                    _mustSupportRefKeyIdentifierName = true;
                    return CreateWsspAssertion(RequireKeyIdentifierReferenceName);
                case X509KeyIdentifierClauseType.Thumbprint:
                    _mustSupportRefThumbprintName = true;
                    return CreateWsspAssertion(RequireThumbprintReferenceName);
                case X509KeyIdentifierClauseType.Any:
                    _mustSupportRefIssuerSerialName = true;
                    _mustSupportRefKeyIdentifierName = true;
                    _mustSupportRefThumbprintName = true;
                    return null;
            }
        }

        public virtual XmlElement CreateWsspX509TokenAssertion(MetadataExporter exporter, X509SecurityTokenParameters parameters)
        {
            XmlElement result = CreateWsspAssertion(X509TokenName);
            SetIncludeTokenValue(result, parameters.InclusionMode);
            result.AppendChild(
                CreateWspPolicyWrapper(
                    exporter,
                    CreateWsspRequireDerivedKeysAssertion(parameters.RequireDerivedKeys),
                    CreateX509ReferenceStyleAssertion(parameters.X509ReferenceStyle),
                    CreateWsspAssertion(WssX509V3Token10Name)
            ));
            return result;
        }

        public virtual XmlElement CreateWsspUsernameTokenAssertion(MetadataExporter exporter, UserNameSecurityTokenParameters parameters)
        {
            XmlElement result = CreateWsspAssertion(UsernameTokenName);
            SetIncludeTokenValue(result, parameters.InclusionMode);
            result.AppendChild(
                CreateWspPolicyWrapper(
                    exporter,
                    CreateWsspAssertion(WssUsernameToken10Name)
            ));
            return result;
        }

        public virtual XmlElement CreateReferenceStyleAssertion(SecurityTokenReferenceStyle referenceStyle)
        {
            switch (referenceStyle)
            {
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(referenceStyle)));
                case SecurityTokenReferenceStyle.External:
                    return CreateWsspAssertion(RequireExternalReferenceName);
                case SecurityTokenReferenceStyle.Internal:
                    return CreateWsspAssertion(RequireInternalReferenceName);
            }
        }

        public virtual XmlElement CreateWsspIssuerElement(EndpointAddress issuerAddress, EndpointAddress issuerMetadataAddress)
        {
            XmlElement result;
            if (issuerAddress == null && issuerMetadataAddress == null)
            {
                result = null;
            }
            else
            {
                EndpointAddress addressToSerialize;
                addressToSerialize = issuerAddress == null ? EndpointAddress.AnonymousAddress : issuerAddress;

                MemoryStream stream;
                XmlWriter writer;

                if (issuerMetadataAddress != null)
                {
                    MetadataSet metadataSet = new MetadataSet();
                    metadataSet.MetadataSections.Add(new MetadataSection(null, null, new MetadataReference(issuerMetadataAddress, AddressingVersion.WSAddressing10)));

                    stream = new MemoryStream();
                    writer = new XmlTextWriter(stream, System.Text.Encoding.UTF8);
                    metadataSet.WriteTo(XmlDictionaryWriter.CreateDictionaryWriter(writer));
                    writer.Flush();
                    stream.Seek(0, SeekOrigin.Begin);

                    addressToSerialize = new EndpointAddress(
                        addressToSerialize.Uri,
                        addressToSerialize.Identity,
                        addressToSerialize.Headers,
                        XmlDictionaryReader.CreateDictionaryReader(XmlReader.Create(stream)),
                        addressToSerialize.GetReaderAtExtensions());
                }

                stream = new MemoryStream();
                writer = new XmlTextWriter(stream, System.Text.Encoding.UTF8);
                writer.WriteStartElement(IssuerName, WsspNamespaceUri);
                addressToSerialize.WriteContentsTo(AddressingVersion.WSAddressing10, writer);
                writer.WriteEndElement();
                writer.Flush();
                stream.Seek(0, SeekOrigin.Begin);
                result = (XmlElement)doc.ReadNode(new XmlTextReader(stream) { DtdProcessing = DtdProcessing.Prohibit });
            }
            return result;
        }

        public virtual XmlElement CreateWsspIssuedTokenAssertion(MetadataExporter exporter, IssuedSecurityTokenParameters parameters)
        {
            XmlElement result = CreateWsspAssertion(IssuedTokenName);
            SetIncludeTokenValue(result, parameters.InclusionMode);
            XmlElement issuerAssertion = CreateWsspIssuerElement(parameters.IssuerAddress, parameters.IssuerMetadataAddress);
            if (issuerAssertion != null)
            {
                result.AppendChild(issuerAssertion);
            }
            XmlElement tokenTemplate = CreateWsspAssertion(RequestSecurityTokenTemplateName);
            TrustDriver driver = TrustDriver;
            foreach (XmlElement p in parameters.CreateRequestParameters(driver))
            {
                tokenTemplate.AppendChild(doc.ImportNode(p, true));
            }
            result.AppendChild(tokenTemplate);
            result.AppendChild(
                CreateWspPolicyWrapper(
                    exporter,
                    CreateWsspRequireDerivedKeysAssertion(parameters.RequireDerivedKeys),
                    CreateReferenceStyleAssertion(parameters.ReferenceStyle)
            ));
            return result;
        }

        public virtual XmlElement CreateWsspMustNotSendCancelAssertion(bool requireCancel)
        {
            if (!requireCancel)
            {
                XmlElement result = CreateWsspAssertion(MustNotSendCancelName);
                return result;
            }
            else
            {
                return null;
            }
        }

        public virtual XmlElement CreateWsspSpnegoContextTokenAssertion(MetadataExporter exporter, SspiSecurityTokenParameters parameters)
        {
            XmlElement result = CreateWsspAssertion(SpnegoContextTokenName);
            SetIncludeTokenValue(result, parameters.InclusionMode);
            result.AppendChild(
                CreateWspPolicyWrapper(
                    exporter,
                    CreateWsspRequireDerivedKeysAssertion(parameters.RequireDerivedKeys),
                    CreateWsspMustNotSendCancelAssertion(parameters.RequireCancellation)
            ));
            return result;
        }

        public virtual XmlElement CreateMsspRequireClientCertificateAssertion(bool requireClientCertificate)
        {
            if (requireClientCertificate)
            {
                return CreateMsspAssertion(RequireClientCertificateName);
            }
            else
            {
                return null;
            }
        }

        public virtual XmlElement CreateMsspSslContextTokenAssertion(MetadataExporter exporter, SslSecurityTokenParameters parameters)
        {
            XmlElement result = CreateMsspAssertion(SslContextTokenName);
            SetIncludeTokenValue(result, parameters.InclusionMode);
            result.AppendChild(
                CreateWspPolicyWrapper(
                    exporter,
                    CreateWsspRequireDerivedKeysAssertion(parameters.RequireDerivedKeys),
                    CreateWsspMustNotSendCancelAssertion(parameters.RequireCancellation),
                    CreateMsspRequireClientCertificateAssertion(parameters.RequireClientCertificate)
            ));
            return result;
        }

        public virtual XmlElement CreateWsspBootstrapPolicyAssertion(MetadataExporter exporter, SecurityBindingElement bootstrapSecurity)
        {
            if (bootstrapSecurity == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(bootstrapSecurity));

            WSSecurityPolicy sp = WSSecurityPolicy.GetSecurityPolicyDriver(bootstrapSecurity.MessageSecurityVersion);

            // create complete bootstrap binding

            CustomBinding bootstrapBinding = new CustomBinding(bootstrapSecurity);
            if (exporter.State.ContainsKey(SecurityPolicyStrings.SecureConversationBootstrapBindingElementsBelowSecurityKey))
            {
                BindingElementCollection bindingElementsBelowSecurity = exporter.State[SecurityPolicyStrings.SecureConversationBootstrapBindingElementsBelowSecurityKey] as BindingElementCollection;
                if (bindingElementsBelowSecurity != null)
                {
                    foreach (BindingElement be in bindingElementsBelowSecurity)
                    {
                        bootstrapBinding.Elements.Add(be);
                    }
                }
            }

            // generate policy for the "how" of security 

            ServiceEndpoint bootstrapEndpoint = new ServiceEndpoint(NullContract);
            bootstrapEndpoint.Binding = bootstrapBinding;
            PolicyConversionContext policyContext = exporter.ExportPolicy(bootstrapEndpoint);

            // generate policy for the "what" of security (protection assertions)

            // hard-coded requirements in V1: sign and encrypt RST and RSTR body
            ChannelProtectionRequirements bootstrapProtection = new ChannelProtectionRequirements();
            bootstrapProtection.IncomingEncryptionParts.AddParts(new MessagePartSpecification(true));
            bootstrapProtection.OutgoingEncryptionParts.AddParts(new MessagePartSpecification(true));
            bootstrapProtection.IncomingSignatureParts.AddParts(new MessagePartSpecification(true));
            bootstrapProtection.OutgoingSignatureParts.AddParts(new MessagePartSpecification(true));

            // add boostrap binding protection requirements (e.g. addressing headers)
            ChannelProtectionRequirements cpr = bootstrapBinding.GetProperty<ChannelProtectionRequirements>(new BindingParameterCollection());
            if (cpr != null)
            {
                bootstrapProtection.Add(cpr);
            }

            // extract channel-scope protection requirements and union them across request and response
            MessagePartSpecification encryption = new MessagePartSpecification();
            encryption.Union(bootstrapProtection.IncomingEncryptionParts.ChannelParts);
            encryption.Union(bootstrapProtection.OutgoingEncryptionParts.ChannelParts);
            encryption.MakeReadOnly();
            MessagePartSpecification signature = new MessagePartSpecification();
            signature.Union(bootstrapProtection.IncomingSignatureParts.ChannelParts);
            signature.Union(bootstrapProtection.OutgoingSignatureParts.ChannelParts);
            signature.MakeReadOnly();

            // create final boostrap policy assertion

            XmlElement nestedPolicy = CreateWspPolicyWrapper(
                    exporter,
                    sp.CreateWsspSignedPartsAssertion(signature),
                    sp.CreateWsspEncryptedPartsAssertion(encryption));
            foreach (XmlElement e in sp.FilterWsspPolicyAssertions(policyContext.GetBindingAssertions()))
            {
                nestedPolicy.AppendChild(e);
            }
            XmlElement result = CreateWsspAssertion(BootstrapPolicyName);
            result.AppendChild(nestedPolicy);

            return result;
        }

        public virtual ICollection<XmlElement> FilterWsspPolicyAssertions(ICollection<XmlElement> policyAssertions)
        {
            Collection<XmlElement> result = new Collection<XmlElement>();

            foreach (XmlElement assertion in policyAssertions)
                if (IsWsspAssertion(assertion))
                    result.Add(assertion);

            return result;
        }

        public virtual XmlElement CreateWsspSecureConversationTokenAssertion(MetadataExporter exporter, SecureConversationSecurityTokenParameters parameters)
        {
            XmlElement result = CreateWsspAssertion(SecureConversationTokenName);
            SetIncludeTokenValue(result, parameters.InclusionMode);
            result.AppendChild(
                CreateWspPolicyWrapper(
                    exporter,
                    CreateWsspRequireDerivedKeysAssertion(parameters.RequireDerivedKeys),
                    CreateWsspMustNotSendCancelAssertion(parameters.RequireCancellation),
                    CreateWsspBootstrapPolicyAssertion(exporter, parameters.BootstrapSecurityBindingElement)
            ));
            return result;
        }

        public static WSSecurityPolicy GetSecurityPolicyDriver(MessageSecurityVersion version)
        {
            SecurityPolicyManager policyManager = new SecurityPolicyManager();
            return policyManager.GetSecurityPolicyDriver(version);
        }

        private class SecurityPolicyManager
        {
            private readonly List<WSSecurityPolicy> _drivers;

            public SecurityPolicyManager()
            {
                _drivers = new List<WSSecurityPolicy>();
                Initialize();
            }

            public void Initialize()
            {
                _drivers.Add(new WSSecurityPolicy11());
                _drivers.Add(new WSSecurityPolicy12());
            }

            public WSSecurityPolicy GetSecurityPolicyDriver(MessageSecurityVersion version)
            {
                for (int i = 0; i < _drivers.Count; ++i)
                {
                    if (_drivers[i].IsSecurityVersionSupported(version))
                    {
                        return _drivers[i];
                    }
                }

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
            }

        }
    }

    internal static class SecurityPolicyStrings
    {
        public const string SecureConversationBootstrapBindingElementsBelowSecurityKey = "SecureConversationBootstrapBindingElementsBelowSecurityKey";
    }
}
