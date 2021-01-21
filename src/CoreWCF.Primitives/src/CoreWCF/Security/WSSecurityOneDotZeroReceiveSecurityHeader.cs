// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class WSSecurityOneDotZeroReceiveSecurityHeader : ReceiveSecurityHeader
    {
        private KeyedHashAlgorithm _signingKey;
        private const string SIGNED_XML_HEADER = "signed_xml_header";
        public WSSecurityOneDotZeroReceiveSecurityHeader(Message message, string actor, bool mustUnderstand, bool relay,
            SecurityStandardsManager standardsManager,
            SecurityAlgorithmSuite algorithmSuite,
            int headerIndex,
            MessageDirection transferDirection)
            : base(message, actor, mustUnderstand, relay, standardsManager, algorithmSuite, headerIndex, transferDirection)
        {
        }

        protected override bool IsReaderAtReferenceList(XmlDictionaryReader reader)
        {
            return reader.IsStartElement(ReferenceList.ElementName, ReferenceList.NamespaceUri);
        }

        protected override bool IsReaderAtSignature(XmlDictionaryReader reader)
        {
            return reader.IsStartElement(XD.XmlSignatureDictionary.Signature, XD.XmlSignatureDictionary.Namespace);
        }

        protected override void EnsureDecryptionComplete()
        {
            // noop
        }

        protected override bool IsReaderAtEncryptedKey(XmlDictionaryReader reader)
        {
            return reader.IsStartElement(CoreWCF.XD.XmlEncryptionDictionary.EncryptedKey, CoreWCF.XD.XmlEncryptionDictionary.Namespace);
        }

        protected override bool IsReaderAtEncryptedData(XmlDictionaryReader reader)
        {
            bool encrypted = reader.IsStartElement(CoreWCF.XD.XmlEncryptionDictionary.EncryptedData, CoreWCF.XD.XmlEncryptionDictionary.Namespace);

            if (encrypted == true)
            {
                throw new PlatformNotSupportedException();
            }

            return encrypted;
        }

        protected override bool IsReaderAtSecurityTokenReference(XmlDictionaryReader reader)
        {
            return reader.IsStartElement(XD.SecurityJan2004Dictionary.SecurityTokenReference, XD.SecurityJan2004Dictionary.Namespace);
        }

        protected override EncryptedData ReadSecurityHeaderEncryptedItem(XmlDictionaryReader reader, bool readXmlreferenceKeyInfoClause)
        {
            throw new PlatformNotSupportedException();
        }

        protected override byte[] DecryptSecurityHeaderElement(EncryptedData encryptedData, WrappedKeySecurityToken wrappedKeyToken, out SecurityToken encryptionToken)
        {
            throw new PlatformNotSupportedException();
        }

        protected override WrappedKeySecurityToken DecryptWrappedKey(XmlDictionaryReader reader)
        {
            throw new PlatformNotSupportedException();
        }

        protected override void OnDecryptionOfSecurityHeaderItemRequiringReferenceListEntry(string id)
        {
            throw new PlatformNotSupportedException();
        }

        protected override void ExecuteMessageProtectionPass(bool hasAtLeastOneSupportingTokenExpectedToBeSigned)
        {
            throw new PlatformNotSupportedException();
        }

        protected override ReferenceList ReadReferenceListCore(XmlDictionaryReader reader)
        {
            throw new PlatformNotSupportedException();
        }

        protected override void ProcessReferenceListCore(ReferenceList referenceList, WrappedKeySecurityToken wrappedKeyToken)
        {
            throw new PlatformNotSupportedException();
        }

        protected override void ReadSecurityTokenReference(XmlDictionaryReader reader)
        {
            throw new PlatformNotSupportedException();
        }

        protected override SignedXml ReadSignatureCore(XmlDictionaryReader signatureReader)
        {
            XmlDocument doc = new XmlDocument();
            using (XmlWriter writer = doc.CreateNavigator().AppendChild())
            {
                writer.WriteStartDocument();
                writer.WriteStartElement(SIGNED_XML_HEADER);
                MessageHeaders headers = this.Message.Headers;
                for (int i = 0; i < headers.Count; i++)
                    headers.WriteHeader(i, writer);
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
            SignedXMLInternal signedXml = new SignedXMLInternal(doc);
            XmlNodeList nodeList = doc.GetElementsByTagName("Signature");
            signedXml.LoadXml((XmlElement)nodeList[0]);
            using (XmlReader tempReader = signatureReader.ReadSubtree())
            {
                tempReader.Read();//move the reader to next
            }
            return signedXml;
        }

        protected override SecurityToken VerifySignature(SignedXml signedXml, bool isPrimarySignature, SecurityHeaderTokenResolver resolver, object signatureTarget, string id)
        {
            SecurityKeyIdentifier securityKeyIdentifier = null;
            String keyInfoString = signedXml.Signature.KeyInfo.GetXml().OuterXml;
            using (var strReader = new StringReader(keyInfoString))
            {
                XmlReader xmlReader = XmlReader.Create(strReader);
                securityKeyIdentifier = this.StandardsManager.SecurityTokenSerializer.ReadKeyIdentifier(xmlReader);

            }
            if (securityKeyIdentifier == null)
                throw new Exception("SecurityKeyIdentifier is missing");
            SecurityToken token = ResolveSignatureToken(securityKeyIdentifier, resolver, isPrimarySignature);
            if (isPrimarySignature)
            {
                RecordSignatureToken(token);
            }
            ReadOnlyCollection<SecurityKey> keys = token.SecurityKeys;
            SecurityKey securityKey = (keys != null && keys.Count > 0) ? keys[0] : null;
            if (securityKey == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                    SR.Format(SR.UnableToCreateICryptoFromTokenForSignatureVerification, token)));
            }
            // signedXml.SigningKey = securityKey;

            // signedXml.StartSignatureVerification(securityKey);
            // StandardSignedInfo signedInfo = (StandardSignedInfo)signedXml.Signature.SignedInfo;

            // ValidateDigestsOfTargetsInSecurityHeader(signedInfo, this.Timestamp, isPrimarySignature, signatureTarget, id);

            if (!isPrimarySignature)
            {
                //TODO securityKey is AsymmetricSecurityKey
                //if ((!this.RequireMessageProtection) && (securityKey is AsymmetricSecurityKey) && (this.Version.Addressing != AddressingVersion.None))
                //{
                //    // For Transport Security using Asymmetric Keys verify that 
                //    // the 'To' header is signed.
                //    int headerIndex = this.Message.Headers.FindHeader(XD.AddressingDictionary.To.Value, this.Message.Version.Addressing.Namespace);
                //    if (headerIndex == -1)
                //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.GetString(SR.TransportSecuredMessageMissingToHeader)));
                //    XmlDictionaryReader toHeaderReader = this.Message.Headers.GetReaderAtHeader(headerIndex);
                //    id = toHeaderReader.GetAttribute(XD.UtilityDictionary.IdAttribute, XD.UtilityDictionary.Namespace);

                //    // DevDiv:938534 - We added a flag that allow unsigned headers. If this is set, we do not throw an Exception but move on to CompleteSignatureVerification()
                //    if (LocalAppContextSwitches.AllowUnsignedToHeader)
                //    {
                //        // The lack of an id indicates that the sender did not wish to sign the header. We can safely assume that null indicates this header is not signed.
                //        // If id is not null, then we need to validate the Digest and ensure signature is valid. The exception is thrown deeper in the System.IdentityModel stack.
                //        if (id != null)
                //        {
                //            signedXml.EnsureDigestValidityIfIdMatches(id, toHeaderReader);
                //        }
                //    }
                //    else
                //    {
                //        // default behavior for all platforms
                //        if (id == null)
                //        {
                //            // 
                //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.GetString(SR.UnsignedToHeaderInTransportSecuredMessage)));
                //        }
                //        signedXml.EnsureDigestValidity(id, toHeaderReader);
                //    }
                //}
                // signedXml.CompleteSignatureVerification();

                SecurityAlgorithmSuite suite = AlgorithmSuite;
                this.AlgorithmSuite.EnsureAcceptableSignatureKeySize(securityKey, token);
                this.AlgorithmSuite.EnsureAcceptableSignatureAlgorithm(securityKey, signedXml.Signature.SignedInfo.SignatureMethod);
                string canonicalizationAlgorithm = suite.DefaultCanonicalizationAlgorithm;
                string signatureAlgorithm;
                XmlDictionaryString signatureAlgorithmDictionaryString;
                SecurityKey signatureKey;
                suite.GetSignatureAlgorithmAndKey(token, out signatureAlgorithm, out signatureKey, out signatureAlgorithmDictionaryString);
                AsymmetricAlgorithm asymmetricAlgorithm;
                GetSigningAlgorithm(signatureKey, signatureAlgorithm, out _signingKey, out asymmetricAlgorithm);
                if (_signingKey != null)
                {
                    if (!signedXml.CheckSignature(_signingKey))
                    {
                        throw new Exception("Signature not valid.");
                    }
                }
                else
                {
                    if (!signedXml.CheckSignature(asymmetricAlgorithm))
                    {
                        throw new Exception("Signature not valid.");
                    }
                }


            }
            // this.pendingSignature = signedXml;

            //if (TD.SignatureVerificationSuccessIsEnabled())
            //{
            //    TD.SignatureVerificationSuccess(this.EventTraceActivity);
            //}

            return token;
        }

        private void GetSigningAlgorithm(SecurityKey signatureKey, string algorithmName, out KeyedHashAlgorithm symmetricAlgorithm, out AsymmetricAlgorithm asymmetricAlgorithm)
        {
            symmetricAlgorithm = null;
            asymmetricAlgorithm = null;
            SymmetricSecurityKey symmetricKey = signatureKey as SymmetricSecurityKey;
            if (symmetricKey != null)
            {
                _signingKey = symmetricKey.GetKeyedHashAlgorithm(algorithmName);
                if (_signingKey == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                        SR.Format(SR.UnableToCreateKeyedHashAlgorithm, symmetricKey, algorithmName)));
                }
            }
            else
            {
                AsymmetricSecurityKey asymmetricKey = signatureKey as AsymmetricSecurityKey;
                if (asymmetricKey == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                        SR.Format(SR.UnknownICryptoType, _signingKey)));
                }

                //On server we validate using Public Key.... (check with Matt)
                asymmetricAlgorithm = asymmetricKey.GetAsymmetricAlgorithm(algorithmName, false);
                if (asymmetricAlgorithm == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                        SR.Format(SR.UnableToCreateKeyedHashAlgorithm, algorithmName,
                            asymmetricKey)));
                }
            }
        }

        private SecurityToken ResolveSignatureToken(SecurityKeyIdentifier keyIdentifier, SecurityTokenResolver resolver, bool isPrimarySignature)
        {
            SecurityToken token = null;
            TryResolveKeyIdentifier(keyIdentifier, resolver, true, out token);
            if (token == null && !isPrimarySignature)
            {
                // check if there is a rsa key token authenticator
                if (keyIdentifier.Count == 1)
                {
                    RsaKeyIdentifierClause rsaClause;
                    if (keyIdentifier.TryFind<RsaKeyIdentifierClause>(out rsaClause))
                    {
                        RsaSecurityTokenAuthenticator rsaAuthenticator = FindAllowedAuthenticator<RsaSecurityTokenAuthenticator>(false);
                        if (rsaAuthenticator != null)
                        {
                            token = new RsaSecurityToken(rsaClause.Rsa);
                            ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies = rsaAuthenticator.ValidateToken(token);
                            SupportingTokenAuthenticatorSpecification spec;
                            TokenTracker rsaTracker = GetSupportingTokenTracker(rsaAuthenticator, out spec);
                            if (rsaTracker == null)
                            {
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.UnknownTokenAuthenticatorUsedInTokenProcessing, rsaAuthenticator)));
                            }
                            rsaTracker.RecordToken(token);
                            SecurityTokenAuthorizationPoliciesMapping.Add(token, authorizationPolicies);
                        }
                    }
                }
            }
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                        SR.Format(SR.UnableToResolveKeyInfoForVerifyingSignature, keyIdentifier, resolver)));
            }
            return token;
        }

        protected static bool TryResolveKeyIdentifier(
         SecurityKeyIdentifier keyIdentifier, SecurityTokenResolver resolver, bool isFromSignature, out SecurityToken token)
        {
            if (keyIdentifier == null)
            {
                if (isFromSignature)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.NoKeyInfoInSignatureToFindVerificationToken)));
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.NoKeyInfoInEncryptedItemToFindDecryptingToken)));
                }
            }
            return resolver.TryResolveToken(keyIdentifier, out token);
        }
        protected override bool TryDeleteReferenceListEntry(string id)
        {
            throw new NotImplementedException();
        }
    }

}
