// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class WSSecurityOneDotZeroReceiveSecurityHeader : ReceiveSecurityHeader
    {
        private KeyedHashAlgorithm _signingKey;
        private ReferenceList _pendingReferenceList;
        private WrappedKeySecurityToken _pendingDecryptionToken;
        private List<string> _earlyDecryptedDataReferences;
        private SignedXml _pendingSignature;
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
            if (_earlyDecryptedDataReferences != null)
            {
                for (int i = 0; i < _earlyDecryptedDataReferences.Count; i++)
                {
                    if (!TryDeleteReferenceListEntry(_earlyDecryptedDataReferences[i]))
                    {
                        throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.UnexpectedEncryptedElementInSecurityHeader)), this.Message);
                    }
                }
            }
            if (HasPendingDecryptionItem())
            {
                throw TraceUtility.ThrowHelperError(
                    new MessageSecurityException(SR.Format(SR.UnableToResolveDataReference, _pendingReferenceList.GetReferredId(0))), this.Message);
            }
        }

        protected bool HasPendingDecryptionItem()
        {
            return _pendingReferenceList != null && _pendingReferenceList.DataReferenceCount > 0;
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
                HasAtLeastOneItemInsideSecurityHeaderEncrypted = true;
            }

            return encrypted;
        }

        protected override bool IsReaderAtSecurityTokenReference(XmlDictionaryReader reader)
        {
            return reader.IsStartElement(XD.SecurityJan2004Dictionary.SecurityTokenReference, XD.SecurityJan2004Dictionary.Namespace);
        }

        protected override EncryptedData ReadSecurityHeaderEncryptedItem(XmlDictionaryReader reader, bool readXmlreferenceKeyInfoClause)
        {
            EncryptedData encryptedData = new EncryptedData();
            encryptedData.ShouldReadXmlReferenceKeyInfoClause = readXmlreferenceKeyInfoClause;
            encryptedData.SecurityTokenSerializer = StandardsManager.SecurityTokenSerializer;
            encryptedData.ReadFrom(reader);
            return encryptedData;
        }

        protected override byte[] DecryptSecurityHeaderElement(EncryptedData encryptedData, WrappedKeySecurityToken wrappedKeyToken, out SecurityToken encryptionToken)
        {
            if ((encryptedData.KeyIdentifier != null) || (wrappedKeyToken == null))
            {
                // The EncryptedData might have a KeyInfo inside it. Try resolving the SecurityKeyIdentifier. 
                encryptionToken = ResolveKeyIdentifier(encryptedData.KeyIdentifier, this.CombinedPrimaryTokenResolver, false);
                if (wrappedKeyToken != null && wrappedKeyToken.ReferenceList != null && encryptedData.HasId && wrappedKeyToken.ReferenceList.ContainsReferredId(encryptedData.Id) && (wrappedKeyToken != encryptionToken))
                {
                    // We have a EncryptedKey with a ReferenceList inside it. This would mean that 
                    // all the EncryptedData pointed by the ReferenceList should be encrypted only
                    // by this key. The individual EncryptedData elements if containing a KeyInfo
                    // clause should point back to the same EncryptedKey token.
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.EncryptedKeyWasNotEncryptedWithTheRequiredEncryptingToken, wrappedKeyToken)));
                }
            }
            else
            {
                encryptionToken = wrappedKeyToken;
            }
            using (SymmetricAlgorithm algorithm = CreateDecryptionAlgorithm(encryptionToken, encryptedData.EncryptionMethod, this.AlgorithmSuite))
            {
                encryptedData.SetUpDecryption(algorithm);
                return encryptedData.GetDecryptedBuffer();
            }
        }

        protected override WrappedKeySecurityToken DecryptWrappedKey(XmlDictionaryReader reader)
        {
            WrappedKeySecurityToken token = (WrappedKeySecurityToken)StandardsManager.SecurityTokenSerializer.ReadToken(
                reader, PrimaryTokenResolver);
            AlgorithmSuite.EnsureAcceptableKeyWrapAlgorithm(token.WrappingAlgorithm, token.WrappingSecurityKey is AsymmetricSecurityKey);
            return token;
        }

        protected static SymmetricAlgorithm CreateDecryptionAlgorithm(SecurityToken token, string encryptionMethod, SecurityAlgorithmSuite suite)
        {
            if (encryptionMethod == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                    SR.Format(SR.EncryptionMethodMissingInEncryptedData)));
            }
            suite.EnsureAcceptableEncryptionAlgorithm(encryptionMethod);
            SymmetricSecurityKey symmetricSecurityKey = SecurityUtils.GetSecurityKey<SymmetricSecurityKey>(token);
            if (symmetricSecurityKey == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                    SR.Format(SR.TokenCannotCreateSymmetricCrypto, token)));
            }
            suite.EnsureAcceptableDecryptionSymmetricKeySize(symmetricSecurityKey, token);
            SymmetricAlgorithm algorithm = symmetricSecurityKey.GetSymmetricAlgorithm(encryptionMethod);
            if (algorithm == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                    SR.Format(SR.UnableToCreateSymmetricAlgorithmFromToken, encryptionMethod)));
            }

            return algorithm;
        }

        protected override void OnDecryptionOfSecurityHeaderItemRequiringReferenceListEntry(string id)
        {
            if (!TryDeleteReferenceListEntry(id))
            {
                if (_earlyDecryptedDataReferences == null)
                {
                    _earlyDecryptedDataReferences = new List<string>(4);
                }
                _earlyDecryptedDataReferences.Add(id);
            }
        }

        protected override void ExecuteMessageProtectionPass(bool hasAtLeastOneSupportingTokenExpectedToBeSigned)
        {
            SignatureTargetIdManager idManager = this.StandardsManager.IdManager;
            MessagePartSpecification encryptionParts = this.RequiredEncryptionParts ?? MessagePartSpecification.NoParts;
            MessagePartSpecification signatureParts = this.RequiredSignatureParts ?? MessagePartSpecification.NoParts;

            bool checkForTokensAtHeaders = hasAtLeastOneSupportingTokenExpectedToBeSigned;
            bool doSoapAttributeChecks = !signatureParts.IsBodyIncluded;
            bool encryptBeforeSign = this.EncryptBeforeSignMode;
            SignedInfo signedInfo = _pendingSignature != null ? _pendingSignature.Signature.SignedInfo : null;

            SignatureConfirmations signatureConfirmations = this.GetSentSignatureConfirmations();
            if (signatureConfirmations != null && signatureConfirmations.Count > 0 && signatureConfirmations.IsMarkedForEncryption)
            {
                // If Signature Confirmations are encrypted then the signature should
                // be encrypted as well.
                this.VerifySignatureEncryption();
            }

            MessageHeaders headers = this.SecurityVerifiedMessage.Headers;
            XmlDictionaryReader reader = this.SecurityVerifiedMessage.GetReaderAtFirstHeader();

            bool atLeastOneHeaderOrBodyEncrypted = false;

            for (int i = 0; i < headers.Count; i++)
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.MoveToContent();
                }

                if (i == this.HeaderIndex)
                {
                    reader.Skip();
                    continue;
                }

                bool isHeaderEncrypted = false;

                string id = idManager.ExtractId(reader);

                if (id != null)
                {
                    isHeaderEncrypted = TryDeleteReferenceListEntry(id);
                }

                if (!isHeaderEncrypted && reader.IsStartElement(SecurityXXX2005Strings.EncryptedHeader, SecurityXXX2005Strings.Namespace))
                {
                    XmlDictionaryReader localreader = headers.GetReaderAtHeader(i);
                    localreader.ReadStartElement(SecurityXXX2005Strings.EncryptedHeader, SecurityXXX2005Strings.Namespace);

                    if (localreader.IsStartElement(EncryptedData.ElementName, XD.XmlEncryptionDictionary.Namespace))
                    {
                        string encryptedDataId = localreader.GetAttribute(XD.XmlEncryptionDictionary.Id, null);

                        if (encryptedDataId != null && TryDeleteReferenceListEntry(encryptedDataId))
                        {
                            isHeaderEncrypted = true;
                        }
                    }
                }

                this.ElementManager.VerifyUniquenessAndSetHeaderId(id, i);

                MessageHeaderInfo info = headers[i];

                if (!isHeaderEncrypted && encryptionParts.IsHeaderIncluded(info.Name, info.Namespace))
                {
                    this.SecurityVerifiedMessage.OnUnencryptedPart(info.Name, info.Namespace);
                }

                bool headerSigned;
                if ((!isHeaderEncrypted || encryptBeforeSign) && id != null)
                {
                    headerSigned = EnsureDigestValidityIfIdMatches(signedInfo, id, reader, doSoapAttributeChecks, signatureParts, info, checkForTokensAtHeaders);
                }
                else
                {
                    headerSigned = false;
                }

                if (isHeaderEncrypted)
                {
                    XmlDictionaryReader decryptionReader = headerSigned ? headers.GetReaderAtHeader(i) : reader;
                    DecryptedHeader decryptedHeader = DecryptHeader(decryptionReader, this.pendingDecryptionToken);
                    info = decryptedHeader;
                    id = decryptedHeader.Id;
                    this.ElementManager.VerifyUniquenessAndSetDecryptedHeaderId(id, i);
                    headers.ReplaceAt(i, decryptedHeader);
                    if (!ReferenceEquals(decryptionReader, reader))
                    {
                        decryptionReader.Close();
                    }

                    if (!encryptBeforeSign && id != null)
                    {
                        XmlDictionaryReader decryptedHeaderReader = decryptedHeader.GetHeaderReader();
                        headerSigned = EnsureDigestValidityIfIdMatches(signedInfo, id, decryptedHeaderReader, doSoapAttributeChecks, signatureParts, info, checkForTokensAtHeaders);
                        decryptedHeaderReader.Close();
                    }
                }

                if (!headerSigned && signatureParts.IsHeaderIncluded(info.Name, info.Namespace))
                {
                    this.SecurityVerifiedMessage.OnUnsignedPart(info.Name, info.Namespace);
                }

                if (headerSigned && isHeaderEncrypted)
                {
                    // We have a header that is signed and encrypted. So the accompanying primary signature
                    // should be encrypted as well.
                    this.VerifySignatureEncryption();
                }

                if (isHeaderEncrypted && !headerSigned)
                {
                    // We require all encrypted headers (outside the security header) to be signed.
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.GetString(SR.EncryptedHeaderNotSigned, info.Name, info.Namespace)));
                }

                if (!headerSigned && !isHeaderEncrypted)
                {
                    reader.Skip();
                }

                atLeastOneHeaderOrBodyEncrypted |= isHeaderEncrypted;
            }

            reader.ReadEndElement();

            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.MoveToContent();
            }

            string bodyId = idManager.ExtractId(reader);
            this.ElementManager.VerifyUniquenessAndSetBodyId(bodyId);
            this.SecurityVerifiedMessage.SetBodyPrefixAndAttributes(reader);

            bool expectBodyEncryption = encryptionParts.IsBodyIncluded || HasPendingDecryptionItem();

            bool bodySigned;
            if ((!expectBodyEncryption || encryptBeforeSign) && bodyId != null)
            {
                bodySigned = EnsureDigestValidityIfIdMatches(signedInfo, bodyId, reader, false, null, null, false);
            }
            else
            {
                bodySigned = false;
            }

            bool bodyEncrypted;
            if (expectBodyEncryption)
            {
                XmlDictionaryReader bodyReader = bodySigned ? this.SecurityVerifiedMessage.CreateFullBodyReader() : reader;
                bodyReader.ReadStartElement();
                string bodyContentId = idManager.ExtractId(bodyReader);
                this.ElementManager.VerifyUniquenessAndSetBodyContentId(bodyContentId);
                bodyEncrypted = bodyContentId != null && TryDeleteReferenceListEntry(bodyContentId);
                if (bodyEncrypted)
                {
                    DecryptBody(bodyReader, this.pendingDecryptionToken);
                }
                if (!ReferenceEquals(bodyReader, reader))
                {
                    bodyReader.Close();
                }
                if (!encryptBeforeSign && signedInfo != null && signedInfo.HasUnverifiedReference(bodyId))
                {
                    bodyReader = this.SecurityVerifiedMessage.CreateFullBodyReader();
                    bodySigned = EnsureDigestValidityIfIdMatches(signedInfo, bodyId, bodyReader, false, null, null, false);
                    bodyReader.Close();
                }
            }
            else
            {
                bodyEncrypted = false;
            }

            if (bodySigned && bodyEncrypted)
            {
                this.VerifySignatureEncryption();
            }

            reader.Close();

            if (_pendingSignature != null)
            {
                _pendingSignature.CompleteSignatureVerification();
                _pendingSignature = null;
            }
            this.pendingDecryptionToken = null;
            atLeastOneHeaderOrBodyEncrypted |= bodyEncrypted;

            if (!bodySigned && signatureParts.IsBodyIncluded)
            {
                this.SecurityVerifiedMessage.OnUnsignedPart(XD.MessageDictionary.Body.Value, this.Version.Envelope.Namespace);
            }

            if (!bodyEncrypted && encryptionParts.IsBodyIncluded)
            {
                this.SecurityVerifiedMessage.OnUnencryptedPart(XD.MessageDictionary.Body.Value, this.Version.Envelope.Namespace);
            }

            this.SecurityVerifiedMessage.OnMessageProtectionPassComplete(atLeastOneHeaderOrBodyEncrypted);
        }

        private bool EnsureDigestValidityIfIdMatches(SignedInfo signedInfo, string id, XmlDictionaryReader reader, bool doSoapAttributeChecks, MessagePartSpecification signatureParts, MessageHeaderInfo info, bool checkForTokensAtHeaders) => throw new NotImplementedException();

        protected override ReferenceList ReadReferenceListCore(XmlDictionaryReader reader)
        {
            ReferenceList referenceList = new ReferenceList();
            referenceList.ReadFrom(reader);
            return referenceList;
        }

        protected override void ProcessReferenceListCore(ReferenceList referenceList, WrappedKeySecurityToken wrappedKeyToken)
        {
            _pendingReferenceList = referenceList;
            _pendingDecryptionToken = wrappedKeyToken;
        }

        protected override void ReadSecurityTokenReference(XmlDictionaryReader reader)
        {
            string strId = reader.GetAttribute(XD.UtilityDictionary.IdAttribute, XD.UtilityDictionary.Namespace);
            SecurityKeyIdentifierClause strClause = this.StandardsManager.SecurityTokenSerializer.ReadKeyIdentifierClause(reader);
            if (string.IsNullOrEmpty(strClause.Id))
            {
                strClause.Id = strId;
            }

            if (!string.IsNullOrEmpty(strClause.Id))
            {
               ElementManager.AppendSecurityTokenReference(strClause, strClause.Id);
            }
        }

        protected override SignedXml ReadSignatureCore(XmlDictionaryReader signatureReader)
        {
            XmlDocument doc1 = new XmlDocument();
            XmlDocument doc = new XmlDocument();
          //  doc1.Load(signatureReader);
           // string result = doc1.OuterXml;
            using (XmlWriter writer = doc.CreateNavigator().AppendChild())
            {
                writer.WriteStartDocument();
                writer.WriteStartElement(SIGNED_XML_HEADER);
                MessageHeaders headers = Message.Headers;
                for (int i = 0; i < headers.Count; i++)
                {
                    headers.WriteHeader(i, writer);
                }
                writer.WriteNode(signatureReader.ReadSubtree(),true);
               // doc1.FirstChild
              //  writer.WriteStartElement("Signature", "http://www.w3.org/2000/09/xmldsig#");

               // writer.WriteString(doc1.InnerXml);
                //writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
            SignedXMLInternal signedXml = new SignedXMLInternal(doc);
            XmlNodeList nodeList = doc.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl);
            signedXml.LoadXml((XmlElement)nodeList[0]);
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
           /* using (XmlReader tempReader = signatureReader.ReadSubtree())
            {
               // tempReader.Read();//move the reader to next
            }*/
            return signedXml;
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
            ValidateDigestsOfTargetsInSecurityHeader(signedXml.SignedInfo, this.Timestamp, isPrimarySignature, signatureTarget, id);

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
            }
            _pendingSignature = signedXml;

            //if (TD.SignatureVerificationSuccessIsEnabled())
            //{
            //    TD.SignatureVerificationSuccess(this.EventTraceActivity);
            //}

            return token;
        }

        
        void ValidateDigestsOfTargetsInSecurityHeader(SignedInfo signedInfo, SecurityTimestamp timestamp, bool isPrimarySignature, object signatureTarget, string id)
        {
            Fx.Assert(!isPrimarySignature || (isPrimarySignature && (signatureTarget == null)), "For primary signature we try to validate all the references.");

            for (int i = 0; i < signedInfo.References.Count; i++)
            {
                InternalReferenceWrapper reference = new InternalReferenceWrapper((Reference)signedInfo.References[i], ResourcePool);
                this.AlgorithmSuite.EnsureAcceptableDigestAlgorithm(reference.DigestMethod);
                string referredId = reference.ExtractReferredId();
                if (isPrimarySignature || (id == referredId))
                {
                    if (timestamp != null && timestamp.Id == referredId && !reference.NeedsInclusiveContext() &&
                        timestamp.DigestAlgorithm == reference.DigestMethod && timestamp.GetDigest() != null)
                    {
                        reference.EnsureDigestValidity(referredId, timestamp.GetDigest());
                        this.ElementManager.SetTimestampSigned(referredId);
                    }
                    else
                    {
                        if (signatureTarget != null)
                            reference.EnsureDigestValidity(id, signatureTarget);
                        else
                        {
                            int tokenIndex = -1;
                            XmlDictionaryReader reader = null;
                            if (reference.IsStrTranform())
                            {
                                if (this.ElementManager.TryGetTokenElementIndexFromStrId(referredId, out tokenIndex))
                                {
                                    ReceiveSecurityHeaderEntry entry;
                                    this.ElementManager.GetElementEntry(tokenIndex, out entry);
                                    bool isSignedToken = (entry.bindingMode == ReceiveSecurityHeaderBindingModes.Signed)
                                                       || (entry.bindingMode == ReceiveSecurityHeaderBindingModes.SignedEndorsing);
                                    // This means it is a protected(signed)primary token.
                                    if (!this.ElementManager.IsPrimaryTokenSigned)
                                    {
                                        this.ElementManager.IsPrimaryTokenSigned = entry.bindingMode == ReceiveSecurityHeaderBindingModes.Primary &&
                                                                                   entry.elementCategory == ReceiveSecurityHeaderElementCategory.Token;
                                    }
                                    this.ElementManager.SetSigned(tokenIndex);
                                    // We pass true if it is a signed supporting token, signed primary token or a SignedEndorsing token. We pass false if it is a SignedEncrypted Token. 
                                    reader = this.ElementManager.GetReader(tokenIndex, isSignedToken);
                                }
                            }
                            else
                                reader = this.ElementManager.GetSignatureVerificationReader(referredId, this.EncryptBeforeSignMode);

                            if (reader != null)
                            {
                                reference.EnsureDigestValidity(referredId, reader);
                                reader.Close();
                            }
                        }
                    }

                    if (!isPrimarySignature)
                    {
                        // We were given an id to verify and we have verified it. So just break out
                        // of the loop.
                        break;
                    }
                }
            }

            // This check makes sure that if RequireSignedPrimaryToken is true (ProtectTokens is enabled on sbe) then the incoming message 
            // should have the primary signature over the primary(signing)token.
            if (isPrimarySignature && this.RequireSignedPrimaryToken && !this.ElementManager.IsPrimaryTokenSigned)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.SupportingTokenIsNotSigned, new IssuedSecurityTokenParameters())));
            }

            // NOTE: On both client and server side, WCF quietly consumes protected tokens even if protect token is not enabled on sbe. 
            // To change this behaviour add another check below and throw appropriate exception message.
        }



        protected static SecurityToken ResolveKeyIdentifier(SecurityKeyIdentifier keyIdentifier, SecurityTokenResolver resolver, bool isFromSignature)
        {
            SecurityToken token;
            if (!TryResolveKeyIdentifier(keyIdentifier, resolver, isFromSignature, out token))
            {
                if (isFromSignature)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                        SR.Format(SR.UnableToResolveKeyInfoForVerifyingSignature, keyIdentifier, resolver)));
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                        SR.Format(SR.UnableToResolveKeyInfoForDecryption, keyIdentifier, resolver)));
                }
            }

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
            return _pendingReferenceList != null && _pendingReferenceList.TryRemoveReferredId(id);
        }
    }
}
