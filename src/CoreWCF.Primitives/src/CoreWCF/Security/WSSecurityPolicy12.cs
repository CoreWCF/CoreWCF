// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Net;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class WSSecurityPolicy12 : WSSecurityPolicy
    {
        public const string WsspNamespace = @"http://docs.oasis-open.org/ws-sx/ws-securitypolicy/200702";
        public const string SignedEncryptedSupportingTokensName = "SignedEncryptedSupportingTokens";
        public const string RequireImpliedDerivedKeysName = "RequireImpliedDerivedKeys";
        public const string RequireExplicitDerivedKeysName = "RequireExplicitDerivedKeys";

        public override string WsspNamespaceUri
        {
            get { return WsspNamespace; }
        }

        public override bool IsSecurityVersionSupported(MessageSecurityVersion version)
        {
            return version == MessageSecurityVersion.WSSecurity10WSTrust13WSSecureConversation13WSSecurityPolicy12BasicSecurityProfile10 ||
                version == MessageSecurityVersion.WSSecurity11WSTrust13WSSecureConversation13WSSecurityPolicy12 ||
                version == MessageSecurityVersion.WSSecurity11WSTrust13WSSecureConversation13WSSecurityPolicy12BasicSecurityProfile10;
        }

        public override TrustDriver TrustDriver
        {
            get
            {
                return new WSTrustDec2005.DriverDec2005(new SecurityStandardsManager(MessageSecurityVersion.WSSecurity11WSTrust13WSSecureConversation13WSSecurityPolicy12, WSSecurityTokenSerializer.DefaultInstance));
            }
        }

        public override Collection<XmlElement> CreateWsspSupportingTokensAssertion(MetadataExporter exporter, Collection<SecurityTokenParameters> signed, Collection<SecurityTokenParameters> signedEncrypted, Collection<SecurityTokenParameters> endorsing, Collection<SecurityTokenParameters> signedEndorsing, Collection<SecurityTokenParameters> optionalSigned, Collection<SecurityTokenParameters> optionalSignedEncrypted, Collection<SecurityTokenParameters> optionalEndorsing, Collection<SecurityTokenParameters> optionalSignedEndorsing, AddressingVersion addressingVersion)
        {
            Collection<XmlElement> supportingTokenAssertions = new Collection<XmlElement>();

            // Signed Supporting Tokens
            XmlElement supportingTokenAssertion = CreateWsspSignedSupportingTokensAssertion(exporter, signed, optionalSigned);
            if (supportingTokenAssertion != null)
                supportingTokenAssertions.Add(supportingTokenAssertion);

            // Signed Encrypted Supporting Tokens
            supportingTokenAssertion = CreateWsspSignedEncryptedSupportingTokensAssertion(exporter, signedEncrypted, optionalSignedEncrypted);
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

        public override XmlElement CreateWsspSpnegoContextTokenAssertion(MetadataExporter exporter, SspiSecurityTokenParameters parameters)
        {
            XmlElement result = CreateWsspAssertion(SpnegoContextTokenName);
            SetIncludeTokenValue(result, parameters.InclusionMode);
            result.AppendChild(
                CreateWspPolicyWrapper(
                    exporter,
                    CreateWsspRequireDerivedKeysAssertion(parameters.RequireDerivedKeys),
                    // Always emit <sp:MustNotSendCancel/> for spnego and sslnego
                    CreateWsspMustNotSendCancelAssertion(false),
                    CreateWsspMustNotSendAmendAssertion(),
                    CreateWsspMustNotSendRenewAssertion()
            ));
            return result;
        }

        public override XmlElement CreateMsspSslContextTokenAssertion(MetadataExporter exporter, SslSecurityTokenParameters parameters)
        {
            XmlElement result = CreateMsspAssertion(SslContextTokenName);
            SetIncludeTokenValue(result, parameters.InclusionMode);
            result.AppendChild(
                CreateWspPolicyWrapper(
                    exporter,
                    CreateWsspRequireDerivedKeysAssertion(parameters.RequireDerivedKeys),
                    // Always emit <sp:MustNotSendCancel/> for spnego and sslnego
                    CreateWsspMustNotSendCancelAssertion(false),
                    CreateMsspRequireClientCertificateAssertion(parameters.RequireClientCertificate),
                    CreateWsspMustNotSendAmendAssertion(),
                    CreateWsspMustNotSendRenewAssertion()
            ));
            return result;
        }

        public override XmlElement CreateWsspSecureConversationTokenAssertion(MetadataExporter exporter, SecureConversationSecurityTokenParameters parameters)
        {
            XmlElement result = CreateWsspAssertion(SecureConversationTokenName);
            SetIncludeTokenValue(result, parameters.InclusionMode);
            result.AppendChild(
                CreateWspPolicyWrapper(
                    exporter,
                    CreateWsspRequireDerivedKeysAssertion(parameters.RequireDerivedKeys),
                    CreateWsspMustNotSendCancelAssertion(parameters.RequireCancellation),
                    CreateWsspBootstrapPolicyAssertion(exporter, parameters.BootstrapSecurityBindingElement),
                    CreateWsspMustNotSendAmendAssertion(),
                    (!parameters.RequireCancellation || !parameters.CanRenewSession) ? CreateWsspMustNotSendRenewAssertion() : null
            ));
            return result;
        }

        XmlElement CreateWsspMustNotSendAmendAssertion()
        {
            XmlElement result = CreateWsspAssertion(MustNotSendAmendName);
            return result;
        }

        XmlElement CreateWsspMustNotSendRenewAssertion()
        {
            XmlElement result = CreateWsspAssertion(MustNotSendRenewName);
            return result;
        }

        XmlElement CreateWsspSignedSupportingTokensAssertion(MetadataExporter exporter, Collection<SecurityTokenParameters> signed, Collection<SecurityTokenParameters> optionalSigned)
        {
            XmlElement result;

            if ((signed == null || signed.Count == 0)
                && (optionalSigned == null || optionalSigned.Count == 0))
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
                if (optionalSigned != null)
                {
                    foreach (SecurityTokenParameters p in optionalSigned)
                    {
                        policy.AppendChild(CreateTokenAssertion(exporter, p, true));
                    }
                }

                result = CreateWsspAssertion(SignedSupportingTokensName);
                result.AppendChild(policy);
            }

            return result;
        }

        XmlElement CreateWsspSignedEncryptedSupportingTokensAssertion(MetadataExporter exporter, Collection<SecurityTokenParameters> signedEncrypted, Collection<SecurityTokenParameters> optionalSignedEncrypted)
        {
            XmlElement result;

            if ((signedEncrypted == null || signedEncrypted.Count == 0)
                && (optionalSignedEncrypted == null || optionalSignedEncrypted.Count == 0))
            {
                result = null;
            }
            else
            {
                XmlElement policy = CreateWspPolicyWrapper(exporter);

                if (signedEncrypted != null)
                {
                    foreach (SecurityTokenParameters p in signedEncrypted)
                    {
                        policy.AppendChild(CreateTokenAssertion(exporter, p));
                    }
                }
                if (optionalSignedEncrypted != null)
                {
                    foreach (SecurityTokenParameters p in optionalSignedEncrypted)
                    {
                        policy.AppendChild(CreateTokenAssertion(exporter, p, true));
                    }
                }

                result = CreateWsspAssertion(SignedEncryptedSupportingTokensName);
                result.AppendChild(policy);
            }

            return result;
        }

        public override XmlElement CreateWsspTrustAssertion(MetadataExporter exporter, SecurityKeyEntropyMode keyEntropyMode)
        {
            return CreateWsspTrustAssertion(Trust13Name, exporter, keyEntropyMode);
        }
    }
}
