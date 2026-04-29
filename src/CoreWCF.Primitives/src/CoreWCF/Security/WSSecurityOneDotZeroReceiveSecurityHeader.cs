// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Threading.Tasks;
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
            return reader.IsStartElement(XD.XmlEncryptionDictionary.EncryptedKey, XD.XmlEncryptionDictionary.Namespace);
        }

        protected override bool IsReaderAtEncryptedData(XmlDictionaryReader reader)
        {
            bool encrypted = reader.IsStartElement(XD.XmlEncryptionDictionary.EncryptedData, XD.XmlEncryptionDictionary.Namespace);

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
            XmlDocument doc = BuildSignedXmlDocument(Message.Headers, HeaderIndex);
            SignedXMLInternal signedXml = new SignedXMLInternal(doc);
            XmlElement signatureElement = FindSecurityHeaderSignatureElement(doc, HeaderIndex);
            signedXml.LoadXml(signatureElement);
            if (signedXml.SignedInfo.CanonicalizationMethodObject is XmlDsigExcC14NTransform xmlDsigExcC14NTransform)
            {
                string[] inclusivePrefixes = XmlHelper.TokenizeInclusiveNamespacesPrefixList(xmlDsigExcC14NTransform.InclusiveNamespacesPrefixList);
                if (inclusivePrefixes != null)
                {
                    for (int i = 0; i < inclusivePrefixes.Length; i++)
                    {
                        string ns = signatureReader.LookupNamespace(inclusivePrefixes[i]);
                        if (ns != null)
                        {
                            XmlAttribute nsAttribute = doc.CreateAttribute("xmlns", inclusivePrefixes[i], "http://www.w3.org/2000/xmlns/");
                            nsAttribute.Value = ns;
                            doc.DocumentElement.SetAttributeNode(nsAttribute);
                        }
                    }
                }
            }
            using (XmlReader tempReader = signatureReader.ReadSubtree())
            {
                tempReader.Read();//move the reader to next
            }
            return signedXml;
        }

        // Builds the XmlDocument used to back signature verification. The
        // document is constructed from the SOAP message headers under a
        // synthetic root element. headerIndex is the position of the
        // wsse:Security header within the headers collection.
        internal static XmlDocument BuildSignedXmlDocument(MessageHeaders headers, int headerIndex)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }
            if (headerIndex < 0 || headerIndex >= headers.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(headerIndex));
            }

            // The verification document must be backed by the wsse:Security
            // header and only the wsse:Security header. Confirm that the
            // header at the supplied index is actually a Security header
            // belonging to a recognised WS-Security namespace before
            // continuing; otherwise the document we produce would have no
            // defensible relationship to the signature being checked.
            MessageHeaderInfo headerInfo = headers[headerIndex];
            if (!string.Equals(headerInfo.Name, XD.SecurityJan2004Dictionary.Security.Value, StringComparison.Ordinal)
                || !(string.Equals(headerInfo.Namespace, WSSecurity10Constants.Namespace, StringComparison.Ordinal)
                    || string.Equals(headerInfo.Namespace, WSSecurity11Constants.Namespace, StringComparison.Ordinal)))
            {
                throw new ArgumentException(SR.Format(SR.SignatureVerificationFailed), nameof(headerIndex));
            }

            XmlDocument doc = new XmlDocument();
            using (XmlWriter writer = doc.CreateNavigator().AppendChild())
            {
                writer.WriteStartDocument();
                writer.WriteStartElement(SIGNED_XML_HEADER);
                // All headers are written into the synthetic verification
                // document so that signature Reference URIs targeting
                // addressing headers (wsa:To, wsa:Action, wsa:MessageID,
                // ...) and the Body continue to resolve. The Security
                // header position (headerIndex) is used by callers (see
                // ReadSignatureCore) to restrict the *Signature element
                // lookup* to inside the Security header subtree,
                // preventing attacker-planted ds:Signature elements in
                // sibling headers from being verified instead of the
                // legitimate one.
                for (int i = 0; i < headers.Count; i++)
                {
                    headers.WriteHeader(i, writer);
                }
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
            return doc;
        }

        // Locates the ds:Signature element that ReadSignatureCore should
        // load. The lookup is constrained to the descendants of the
        // wsse:Security header at headerIndex. Sibling SOAP headers may not
        // contribute a Signature element to the verification, even if one
        // appears lexically before the Security header in the envelope.
        internal static XmlElement FindSecurityHeaderSignatureElement(XmlDocument doc, int headerIndex)
        {
            if (doc == null)
            {
                throw new ArgumentNullException(nameof(doc));
            }
            if (doc.DocumentElement == null || headerIndex < 0 || headerIndex >= doc.DocumentElement.ChildNodes.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(headerIndex));
            }

            XmlElement securityHeaderElement = doc.DocumentElement.ChildNodes[headerIndex] as XmlElement;
            if (securityHeaderElement == null)
            {
                throw new MessageSecurityException(SR.Format(SR.SignatureVerificationFailed));
            }

            // Defence in depth: confirm locally that the element at
            // headerIndex really is a wsse:Security header before
            // descending into it to pick a ds:Signature. The whole
            // mitigation rests on the indexing operation above, so we
            // do not want to rely solely on the upstream check in
            // BuildSignedXmlDocument staying in lock-step with this
            // method.
            if (!string.Equals(securityHeaderElement.LocalName, XD.SecurityJan2004Dictionary.Security.Value, StringComparison.Ordinal)
                || !(string.Equals(securityHeaderElement.NamespaceURI, WSSecurity10Constants.Namespace, StringComparison.Ordinal)
                    || string.Equals(securityHeaderElement.NamespaceURI, WSSecurity11Constants.Namespace, StringComparison.Ordinal)))
            {
                throw new MessageSecurityException(SR.Format(SR.SignatureVerificationFailed));
            }

            XmlNodeList nodeList = securityHeaderElement.GetElementsByTagName(XD.XmlSignatureDictionary.Signature.Value, XD.XmlSignatureDictionary.Namespace.Value);
            if (nodeList.Count == 0)
            {
                throw new MessageSecurityException(SR.Format(SR.SignatureVerificationFailed));
            }

            return (XmlElement)nodeList[0];
        }

        // Endorsing/supporting signatures must explicitly cover the element
        // identified by 'id' (the wsu:Timestamp on the transport-only path,
        // or the primary signature id on the message-protected path). If the
        // signature carries no Reference whose URI fragment matches the
        // expected id, the signature is rejected even when the cryptographic
        // CheckSignature succeeded against some other resolved element.
        private static void EnsureSignatureCoversExpectedTarget(SignedXml signedXml, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new MessageSecurityException(SR.Format(SR.SignatureVerificationFailed));
            }

            string expected = "#" + id;
            foreach (Reference reference in signedXml.SignedInfo.References)
            {
                if (string.Equals(reference.Uri, expected, StringComparison.Ordinal))
                {
                    return;
                }
            }

            throw new MessageSecurityException(SR.Format(SR.SignatureVerificationFailed));
        }

        protected override async ValueTask<SecurityToken> VerifySignatureAsync(SignedXml signedXml, bool isPrimarySignature, SecurityHeaderTokenResolver resolver, object signatureTarget, string id)
        {
            SecurityKeyIdentifier securityKeyIdentifier = null;
            string keyInfoString = signedXml.Signature.KeyInfo.GetXml().OuterXml;
            using (var strReader = new StringReader(keyInfoString))
            {
                XmlReader xmlReader = XmlReader.Create(strReader);
                securityKeyIdentifier = StandardsManager.SecurityTokenSerializer.ReadKeyIdentifier(xmlReader);
            }
            if (securityKeyIdentifier == null)
            {
                throw new Exception("SecurityKeyIdentifier is missing");
            }

            SecurityToken token = await ResolveSignatureTokenAsync(securityKeyIdentifier, resolver, isPrimarySignature);
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
                AlgorithmSuite.EnsureAcceptableSignatureKeySize(securityKey, token);
                AlgorithmSuite.EnsureAcceptableSignatureAlgorithm(securityKey, signedXml.Signature.SignedInfo.SignatureMethod);
                string canonicalizationAlgorithm = suite.DefaultCanonicalizationAlgorithm;
                suite.GetSignatureAlgorithmAndKey(token, out string signatureAlgorithm, out SecurityKey signatureKey, out XmlDictionaryString signatureAlgorithmDictionaryString);
                GetSigningAlgorithm(signatureKey, signatureAlgorithm, out _signingKey, out AsymmetricAlgorithm asymmetricAlgorithm);
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

                EnsureSignatureCoversExpectedTarget(signedXml, id);
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
            if (signatureKey is SymmetricSecurityKey symmetricKey)
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
                if (!(signatureKey is AsymmetricSecurityKey asymmetricKey))
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

        private async ValueTask<SecurityToken> ResolveSignatureTokenAsync(SecurityKeyIdentifier keyIdentifier, SecurityTokenResolver resolver, bool isPrimarySignature)
        {
            TryResolveKeyIdentifier(keyIdentifier, resolver, true, out SecurityToken token);
            if (token == null && !isPrimarySignature)
            {
                // check if there is a rsa key token authenticator
                if (keyIdentifier.Count == 1)
                {
                    if (keyIdentifier.TryFind<RsaKeyIdentifierClause>(out RsaKeyIdentifierClause rsaClause))
                    {
                        RsaSecurityTokenAuthenticator rsaAuthenticator = FindAllowedAuthenticator<RsaSecurityTokenAuthenticator>(false);
                        if (rsaAuthenticator != null)
                        {
                            token = new RsaSecurityToken(rsaClause.Rsa);
                            ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies = await rsaAuthenticator.ValidateTokenAsync(token);
                            TokenTracker rsaTracker = GetSupportingTokenTracker(rsaAuthenticator, out SupportingTokenAuthenticatorSpecification spec);
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
