// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;
using DictionaryManager = CoreWCF.IdentityModel.DictionaryManager;
using IPrefixGenerator = CoreWCF.IdentityModel.IPrefixGenerator;
using ISecurityElement = CoreWCF.IdentityModel.ISecurityElement;
using ISignatureValueSecurityElement = CoreWCF.IdentityModel.ISignatureValueSecurityElement;

namespace CoreWCF.Security
{
    internal abstract class SendSecurityHeader : SecurityHeader, IMessageHeaderWithSharedNamespace
    {
        private bool basicTokenEncrypted;
        private bool primarySignatureDone;
        private bool encryptSignature;
        private SignatureConfirmations signatureValuesGenerated;
        private SignatureConfirmations signatureConfirmationsToSend;
        private int idCounter;
        private string idPrefix;
        private bool hasEncryptedTokens;
        private MessagePartSpecification signatureParts;
        private MessagePartSpecification encryptionParts;
        private SecurityTokenParameters encryptingTokenParameters;
        private List<SecurityToken> basicTokens = null;
        private List<SecurityTokenParameters> basicSupportingTokenParameters = null;
        private List<SecurityTokenParameters> endorsingTokenParameters = null;
        private List<SecurityTokenParameters> signedEndorsingTokenParameters = null;
        private List<SecurityTokenParameters> signedTokenParameters = null;
        private SecurityToken encryptingToken;
        private bool skipKeyInfoForEncryption;
        private bool shouldProtectTokens;
        private BufferManager bufferManager;
        private SecurityProtocolCorrelationState correlationState;
        private bool signThenEncrypt = true;
        private static readonly string[] ids = new string[] { "_0", "_1", "_2", "_3", "_4", "_5", "_6", "_7", "_8", "_9" };

        protected SendSecurityHeader(Message message, string actor, bool mustUnderstand, bool relay,
            SecurityStandardsManager standardsManager,
            SecurityAlgorithmSuite algorithmSuite,
            MessageDirection transferDirection)
            : base(message, actor, mustUnderstand, relay, standardsManager, algorithmSuite, transferDirection)
        {
            ElementContainer = new SendSecurityHeaderElementContainer();
        }

        public SendSecurityHeaderElementContainer ElementContainer { get; }

        public SecurityProtocolCorrelationState CorrelationState
        {
            get { return correlationState; }
            set
            {
                ThrowIfProcessingStarted();
                correlationState = value;
            }
        }

        public BufferManager StreamBufferManager
        {
            get
            {
                if (bufferManager == null)
                {
                    bufferManager = BufferManager.CreateBufferManager(0, int.MaxValue);
                }

                return bufferManager;
            }
            set
            {
                bufferManager = value;
            }
        }

        public MessagePartSpecification EncryptionParts
        {
            get { return encryptionParts; }
            set
            {
                ThrowIfProcessingStarted();
                if (value == null)
                {
                    throw TraceUtility.ThrowHelperError(new ArgumentNullException(nameof(value)), Message);
                }
                if (!value.IsReadOnly)
                {
                    throw TraceUtility.ThrowHelperError(new InvalidOperationException(SR.MessagePartSpecificationMustBeImmutable), Message);
                }
                encryptionParts = value;
            }
        }

        public bool EncryptPrimarySignature
        {
            get { return encryptSignature; }
            set
            {
                ThrowIfProcessingStarted();
                encryptSignature = value;
            }
        }

        internal byte[] PrimarySignatureValue { get; private set; } = null;

        protected internal SecurityTokenParameters SigningTokenParameters { get; private set; }

        protected bool ShouldSignToHeader { get; private set; } = false;

        public string IdPrefix
        {
            get { return idPrefix; }
            set
            {
                ThrowIfProcessingStarted();
                idPrefix = string.IsNullOrEmpty(value) || value == "_" ? null : value;
            }
        }

        public override string Name => StandardsManager.SecurityVersion.HeaderName.Value;

        public override string Namespace => StandardsManager.SecurityVersion.HeaderNamespace.Value;

        protected SecurityAppliedMessage SecurityAppliedMessage => (SecurityAppliedMessage)Message;

        public bool SignThenEncrypt
        {
            get { return signThenEncrypt; }
            set
            {
                ThrowIfProcessingStarted();
                signThenEncrypt = value;
            }
        }

        public bool ShouldProtectTokens
        {
            get { return shouldProtectTokens; }
            set
            {
                ThrowIfProcessingStarted();
                shouldProtectTokens = value;
            }
        }

        public MessagePartSpecification SignatureParts
        {
            get { return signatureParts; }
            set
            {
                ThrowIfProcessingStarted();
                if (value == null)
                {
                    throw TraceUtility.ThrowHelperError(new ArgumentNullException(nameof(value)), Message);
                }
                if (!value.IsReadOnly)
                {
                    throw TraceUtility.ThrowHelperError(new InvalidOperationException(
                        SR.MessagePartSpecificationMustBeImmutable), Message);
                }
                signatureParts = value;
            }
        }

        public SecurityTimestamp Timestamp => ElementContainer.Timestamp;

        public bool HasSignedTokens { get; private set; }

        public bool HasEncryptedTokens => hasEncryptedTokens;

        public void AddPrerequisiteToken(SecurityToken token)
        {
            ThrowIfProcessingStarted();
            if (token == null)
            {
                throw TraceUtility.ThrowHelperError(new Exception(nameof(token)), Message);
            }
            ElementContainer.PrerequisiteToken = token;
        }

        private void AddParameters(ref List<SecurityTokenParameters> list, SecurityTokenParameters item)
        {
            if (list == null)
            {
                list = new List<SecurityTokenParameters>();
            }
            list.Add(item);
        }

        public abstract void ApplyBodySecurity(XmlDictionaryWriter writer, IPrefixGenerator prefixGenerator);

        public abstract void ApplySecurityAndWriteHeaders(MessageHeaders headers, XmlDictionaryWriter writer, IPrefixGenerator prefixGenerator);

        protected virtual bool HasSignedEncryptedMessagePart => false;

        public void SetSigningToken(SecurityToken token, SecurityTokenParameters tokenParameters)
        {
            ThrowIfProcessingStarted();
            if ((token == null && tokenParameters != null) || (token != null && tokenParameters == null))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.TokenMustBeNullWhenTokenParametersAre)));
            }
            ElementContainer.SourceSigningToken = token;
            SigningTokenParameters = tokenParameters;
        }

        public void SetEncryptionToken(SecurityToken token, SecurityTokenParameters tokenParameters)
        {
            ThrowIfProcessingStarted();
            if ((token == null && tokenParameters != null) || (token != null && tokenParameters == null))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.TokenMustBeNullWhenTokenParametersAre)));
            }
            ElementContainer.SourceEncryptionToken = token;
            encryptingTokenParameters = tokenParameters;
        }

        public void AddBasicSupportingToken(SecurityToken token, SecurityTokenParameters parameters)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }

            if (parameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameters));
            }

            ThrowIfProcessingStarted();
            SendSecurityHeaderElement tokenElement = new SendSecurityHeaderElement(token.Id, new TokenElement(token, StandardsManager));
            tokenElement.MarkedForEncryption = true;
            ElementContainer.AddBasicSupportingToken(tokenElement);
            hasEncryptedTokens = true;
            HasSignedTokens = true;
            AddParameters(ref basicSupportingTokenParameters, parameters);
            if (basicTokens == null)
            {
                basicTokens = new List<SecurityToken>();
            }
            //  We maintain a list of the basic tokens for the SignThenEncrypt case as we will 
            //  need this token to write STR entry on OnWriteHeaderContents. 
            basicTokens.Add(token);

        }

        public void AddEndorsingSupportingToken(SecurityToken token, SecurityTokenParameters parameters)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }

            if (parameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameters));
            }

            ThrowIfProcessingStarted();
            ElementContainer.AddEndorsingSupportingToken(token);
            // The ProviderBackedSecurityToken was added for the ChannelBindingToken (CBT) effort for win7.  
            // We can assume the key is of type symmetric key.
            //
            // Asking for the key type from the token will cause the ProviderBackedSecurityToken 
            // to attempt to resolve the token and the nego will start.  
            //
            // We don't want that.  
            // We want to defer the nego until after the CBT is available in SecurityAppliedMessage.OnWriteMessage.
            //TODO 
            //if (!(token is ProviderBackedSecurityToken))
            //{
            //    this.shouldSignToHeader |= (!this.RequireMessageProtection) && (SecurityUtils.GetSecurityKey<AsymmetricSecurityKey>(token) != null);
            //}
            AddParameters(ref endorsingTokenParameters, parameters);
        }

        public void AddSignedEndorsingSupportingToken(SecurityToken token, SecurityTokenParameters parameters)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }

            if (parameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameters));
            }

            ThrowIfProcessingStarted();
            ElementContainer.AddSignedEndorsingSupportingToken(token);
            HasSignedTokens = true;
            ShouldSignToHeader |= (!RequireMessageProtection) && (SecurityUtils.GetSecurityKey<AsymmetricSecurityKey>(token) != null);
            AddParameters(ref signedEndorsingTokenParameters, parameters);
        }

        public void AddSignedSupportingToken(SecurityToken token, SecurityTokenParameters parameters)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }

            if (parameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameters));
            }

            ThrowIfProcessingStarted();
            ElementContainer.AddSignedSupportingToken(token);
            HasSignedTokens = true;
            AddParameters(ref signedTokenParameters, parameters);
        }

        public void AddSignatureConfirmations(SignatureConfirmations confirmations)
        {
            ThrowIfProcessingStarted();
            signatureConfirmationsToSend = confirmations;
        }

        public void AddTimestamp(TimeSpan timestampValidityDuration)
        {
            DateTime now = DateTime.UtcNow;
            string id = RequireMessageProtection ? SecurityUtils.GenerateId() : GenerateId();
            AddTimestamp(new SecurityTimestamp(now, now + timestampValidityDuration, id));
        }

        public void AddTimestamp(SecurityTimestamp timestamp)
        {
            ThrowIfProcessingStarted();
            if (ElementContainer.Timestamp != null)
            {
                throw TraceUtility.ThrowHelperError(new InvalidOperationException(SR.TimestampAlreadySetForSecurityHeader), Message);
            }
            if (timestamp == null)
            {
                throw TraceUtility.ThrowHelperArgumentNull(nameof(timestamp), Message);
            }

            ElementContainer.Timestamp = timestamp;
        }

        protected virtual ISignatureValueSecurityElement[] CreateSignatureConfirmationElements(SignatureConfirmations signatureConfirmations)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                SR.Format(SR.SignatureConfirmationNotSupported)));
        }

        private void StartEncryption()
        {
            if (ElementContainer.SourceEncryptionToken == null)
            {
                return;
            }
            // determine the key identifier clause to use for the source
            SecurityTokenReferenceStyle sourceEncryptingKeyReferenceStyle = GetTokenReferenceStyle(encryptingTokenParameters);
            bool encryptionTokenSerialized = sourceEncryptingKeyReferenceStyle == SecurityTokenReferenceStyle.Internal;
            SecurityKeyIdentifierClause sourceEncryptingKeyIdentifierClause = encryptingTokenParameters.CreateKeyIdentifierClause(ElementContainer.SourceEncryptionToken, sourceEncryptingKeyReferenceStyle);
            if (sourceEncryptingKeyIdentifierClause == null)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.TokenManagerCannotCreateTokenReference), Message);
            }
            SecurityToken sourceToken;
            SecurityKeyIdentifierClause sourceTokenIdentifierClause;

            // if the source token cannot do symmetric crypto, create a wrapped key
            if (!SecurityUtils.HasSymmetricSecurityKey(ElementContainer.SourceEncryptionToken))
            {
                int keyLength = Math.Max(128, AlgorithmSuite.DefaultSymmetricKeyLength);
                CryptoHelper.ValidateSymmetricKeyLength(keyLength, AlgorithmSuite);
                byte[] key = new byte[keyLength / 8];
                CryptoHelper.FillRandomBytes(key);
                AlgorithmSuite.GetKeyWrapAlgorithm(ElementContainer.SourceEncryptionToken, out string keyWrapAlgorithm, out XmlDictionaryString keyWrapAlgorithmDictionaryString);
                WrappedKeySecurityToken wrappedKey = new WrappedKeySecurityToken(GenerateId(), key, keyWrapAlgorithm, keyWrapAlgorithmDictionaryString,
                    ElementContainer.SourceEncryptionToken, new SecurityKeyIdentifier(sourceEncryptingKeyIdentifierClause));
                ElementContainer.WrappedEncryptionToken = wrappedKey;
                sourceToken = wrappedKey;
                sourceTokenIdentifierClause = new LocalIdKeyIdentifierClause(wrappedKey.Id, wrappedKey.GetType());
                encryptionTokenSerialized = true;
            }
            else
            {
                sourceToken = ElementContainer.SourceEncryptionToken;
                sourceTokenIdentifierClause = sourceEncryptingKeyIdentifierClause;
            }

            // determine if a key needs to be derived
            SecurityKeyIdentifierClause encryptingKeyIdentifierClause;
            // determine if a token needs to be derived
            if (encryptingTokenParameters.RequireDerivedKeys)
            {
                string derivationAlgorithm = AlgorithmSuite.GetEncryptionKeyDerivationAlgorithm(sourceToken, StandardsManager.MessageSecurityVersion.SecureConversationVersion);
                string expectedDerivationAlgorithm = SecurityUtils.GetKeyDerivationAlgorithm(StandardsManager.MessageSecurityVersion.SecureConversationVersion);
                if (derivationAlgorithm == expectedDerivationAlgorithm)
                {
                    DerivedKeySecurityToken derivedEncryptingToken = new DerivedKeySecurityToken(-1, 0,
                        AlgorithmSuite.GetEncryptionKeyDerivationLength(sourceToken, StandardsManager.MessageSecurityVersion.SecureConversationVersion), null, DerivedKeySecurityToken.DefaultNonceLength, sourceToken, sourceTokenIdentifierClause, derivationAlgorithm, GenerateId());
                    encryptingToken = ElementContainer.DerivedEncryptionToken = derivedEncryptingToken;
                    encryptingKeyIdentifierClause = new LocalIdKeyIdentifierClause(derivedEncryptingToken.Id, derivedEncryptingToken.GetType());
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnsupportedCryptoAlgorithm, derivationAlgorithm)));
                }
            }
            else
            {
                encryptingToken = sourceToken;
                encryptingKeyIdentifierClause = sourceTokenIdentifierClause;
            }

            skipKeyInfoForEncryption = encryptionTokenSerialized && EncryptedKeyContainsReferenceList && (encryptingToken is WrappedKeySecurityToken) && signThenEncrypt;
            SecurityKeyIdentifier identifier;
            if (skipKeyInfoForEncryption)
            {
                identifier = null;
            }
            else
            {
                identifier = new SecurityKeyIdentifier(encryptingKeyIdentifierClause);
            }

            StartEncryptionCore(encryptingToken, identifier);
        }

        private void CompleteEncryption()
        {
            ISecurityElement referenceList = CompleteEncryptionCore(
                ElementContainer.PrimarySignature,
                ElementContainer.GetBasicSupportingTokens(),
                ElementContainer.GetSignatureConfirmations(),
                ElementContainer.GetEndorsingSignatures());

            if (referenceList == null)
            {
                // null out all the encryption fields since there is no encryption needed
                ElementContainer.SourceEncryptionToken = null;
                ElementContainer.WrappedEncryptionToken = null;
                ElementContainer.DerivedEncryptionToken = null;
                return;
            }

            if (skipKeyInfoForEncryption)
            {
                WrappedKeySecurityToken wrappedKeyToken = encryptingToken as WrappedKeySecurityToken;
                wrappedKeyToken.EnsureEncryptedKeySetUp();
                wrappedKeyToken.EncryptedKey.ReferenceList = (ReferenceList)referenceList;
            }
            else
            {
                ElementContainer.ReferenceList = referenceList;
            }
            basicTokenEncrypted = true;
        }

        internal void StartSecurityApplication()
        {
            if (SignThenEncrypt)
            {
                StartSignature();
                StartEncryption();
            }
            else
            {
                StartEncryption();
                StartSignature();
            }
        }

        internal void CompleteSecurityApplication()
        {
            if (SignThenEncrypt)
            {
                CompleteSignature();
                SignWithSupportingTokens();
                CompleteEncryption();
            }
            else
            {
                CompleteEncryption();
                CompleteSignature();
                SignWithSupportingTokens();
            }

            if (correlationState != null)
            {
                correlationState.SignatureConfirmations = GetSignatureValues();
            }
        }

        public void RemoveSignatureEncryptionIfAppropriate()
        {
            if (SignThenEncrypt &&
                EncryptPrimarySignature &&
                (SecurityAppliedMessage.BodyProtectionMode != MessagePartProtectionMode.SignThenEncrypt) &&
                (basicSupportingTokenParameters == null || basicSupportingTokenParameters.Count == 0) &&
                (signatureConfirmationsToSend == null || signatureConfirmationsToSend.Count == 0 || !signatureConfirmationsToSend.IsMarkedForEncryption) &&
                !HasSignedEncryptedMessagePart)
            {
                encryptSignature = false;
            }
        }

        public string GenerateId()
        {
            int id = idCounter++;

            if (idPrefix != null)
            {
                return idPrefix + id;
            }

            if (id < ids.Length)
            {
                return ids[id];
            }
            else
            {
                return "_" + id;
            }
        }

        private SignatureConfirmations GetSignatureValues()
        {
            return signatureValuesGenerated;
        }

        protected override void OnWriteStartHeader(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            StandardsManager.SecurityVersion.WriteStartHeader(writer);
            WriteHeaderAttributes(writer, messageVersion);
        }

        internal static bool ShouldSerializeToken(SecurityTokenParameters parameters, MessageDirection transferDirection)
        {
            switch (parameters.InclusionMode)
            {
                case SecurityTokenInclusionMode.AlwaysToInitiator:
                    return (transferDirection == MessageDirection.Output);
                case SecurityTokenInclusionMode.Once:
                case SecurityTokenInclusionMode.AlwaysToRecipient:
                    return (transferDirection == MessageDirection.Input);
                case SecurityTokenInclusionMode.Never:
                    return false;
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnsupportedTokenInclusionMode, parameters.InclusionMode)));
            }
        }

        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            if (basicSupportingTokenParameters != null && basicSupportingTokenParameters.Count > 0
                && RequireMessageProtection && !basicTokenEncrypted)
            {
                throw TraceUtility.ThrowHelperError(new InvalidOperationException(SR.BasicTokenCannotBeWrittenWithoutEncryption), Message);
            }

            if (ElementContainer.Timestamp != null && Layout != SecurityHeaderLayout.LaxTimestampLast)
            {
                StandardsManager.WSUtilitySpecificationVersion.WriteTimestamp(writer, ElementContainer.Timestamp);
            }
            if (ElementContainer.PrerequisiteToken != null)
            {
                StandardsManager.SecurityTokenSerializer.WriteToken(writer, ElementContainer.PrerequisiteToken);
            }
            if (ElementContainer.SourceSigningToken != null)
            {
                if (ShouldSerializeToken(SigningTokenParameters, MessageDirection))
                {
                    StandardsManager.SecurityTokenSerializer.WriteToken(writer, ElementContainer.SourceSigningToken);

                    // Implement Protect token 
                    // NOTE: The spec says sign the primary token if it is not included in the message. But we currently are not supporting it
                    // as we do not support STR-Transform for external references. Hence we can not sign the token which is external ie not in the message.
                    // This only affects the messages from service to client where 
                    // 1. allowSerializedSigningTokenOnReply is false.
                    // 2. SymmetricSecurityBindingElement with IssuedTokens binding where the issued token has a symmetric key.

                    if (ShouldProtectTokens)
                    {
                        WriteSecurityTokenReferencyEntry(writer, ElementContainer.SourceSigningToken, SigningTokenParameters);
                    }
                }
            }
            if (ElementContainer.DerivedSigningToken != null)
            {
                StandardsManager.SecurityTokenSerializer.WriteToken(writer, ElementContainer.DerivedSigningToken);
            }
            if (ElementContainer.SourceEncryptionToken != null && ElementContainer.SourceEncryptionToken != ElementContainer.SourceSigningToken && ShouldSerializeToken(encryptingTokenParameters, MessageDirection))
            {
                StandardsManager.SecurityTokenSerializer.WriteToken(writer, ElementContainer.SourceEncryptionToken);
            }
            if (ElementContainer.WrappedEncryptionToken != null)
            {
                StandardsManager.SecurityTokenSerializer.WriteToken(writer, ElementContainer.WrappedEncryptionToken);
            }
            if (ElementContainer.DerivedEncryptionToken != null)
            {
                StandardsManager.SecurityTokenSerializer.WriteToken(writer, ElementContainer.DerivedEncryptionToken);
            }
            if (SignThenEncrypt)
            {
                if (ElementContainer.ReferenceList != null)
                {
                    ElementContainer.ReferenceList.WriteTo(writer, ServiceModelDictionaryManager.Instance);
                }
            }

            SecurityToken[] signedTokens = ElementContainer.GetSignedSupportingTokens();
            if (signedTokens != null)
            {
                for (int i = 0; i < signedTokens.Length; ++i)
                {
                    StandardsManager.SecurityTokenSerializer.WriteToken(writer, signedTokens[i]);
                    WriteSecurityTokenReferencyEntry(writer, signedTokens[i], signedTokenParameters[i]);
                }
            }
            SendSecurityHeaderElement[] basicTokensXml = ElementContainer.GetBasicSupportingTokens();
            if (basicTokensXml != null)
            {
                for (int i = 0; i < basicTokensXml.Length; ++i)
                {
                    basicTokensXml[i].Item.WriteTo(writer, ServiceModelDictionaryManager.Instance);
                    if (SignThenEncrypt)
                    {
                        WriteSecurityTokenReferencyEntry(writer, basicTokens[i], basicSupportingTokenParameters[i]);
                    }
                }
            }
            SecurityToken[] endorsingTokens = ElementContainer.GetEndorsingSupportingTokens();
            if (endorsingTokens != null)
            {
                for (int i = 0; i < endorsingTokens.Length; ++i)
                {
                    if (ShouldSerializeToken(endorsingTokenParameters[i], MessageDirection))
                    {
                        StandardsManager.SecurityTokenSerializer.WriteToken(writer, endorsingTokens[i]);
                    }
                }
            }
            SecurityToken[] endorsingDerivedTokens = ElementContainer.GetEndorsingDerivedSupportingTokens();
            if (endorsingDerivedTokens != null)
            {
                for (int i = 0; i < endorsingDerivedTokens.Length; ++i)
                {
                    StandardsManager.SecurityTokenSerializer.WriteToken(writer, endorsingDerivedTokens[i]);
                }
            }
            SecurityToken[] signedEndorsingTokens = ElementContainer.GetSignedEndorsingSupportingTokens();
            if (signedEndorsingTokens != null)
            {
                for (int i = 0; i < signedEndorsingTokens.Length; ++i)
                {
                    StandardsManager.SecurityTokenSerializer.WriteToken(writer, signedEndorsingTokens[i]);
                    WriteSecurityTokenReferencyEntry(writer, signedEndorsingTokens[i], signedEndorsingTokenParameters[i]);
                }
            }
            SecurityToken[] signedEndorsingDerivedTokens = ElementContainer.GetSignedEndorsingDerivedSupportingTokens();
            if (signedEndorsingDerivedTokens != null)
            {
                for (int i = 0; i < signedEndorsingDerivedTokens.Length; ++i)
                {
                    StandardsManager.SecurityTokenSerializer.WriteToken(writer, signedEndorsingDerivedTokens[i]);
                }
            }
            SendSecurityHeaderElement[] signatureConfirmations = ElementContainer.GetSignatureConfirmations();
            if (signatureConfirmations != null)
            {
                for (int i = 0; i < signatureConfirmations.Length; ++i)
                {
                    signatureConfirmations[i].Item.WriteTo(writer, ServiceModelDictionaryManager.Instance);
                }
            }
            if (ElementContainer.PrimarySignature != null && ElementContainer.PrimarySignature.Item != null)
            {
                ElementContainer.PrimarySignature.Item.WriteTo(writer, ServiceModelDictionaryManager.Instance);
            }
            SendSecurityHeaderElement[] endorsingSignatures = ElementContainer.GetEndorsingSignatures();
            if (endorsingSignatures != null)
            {
                for (int i = 0; i < endorsingSignatures.Length; ++i)
                {
                    endorsingSignatures[i].Item.WriteTo(writer, ServiceModelDictionaryManager.Instance);
                }
            }
            if (!SignThenEncrypt)
            {
                if (ElementContainer.ReferenceList != null)
                {
                    ElementContainer.ReferenceList.WriteTo(writer, ServiceModelDictionaryManager.Instance);
                }
            }
            if (ElementContainer.Timestamp != null && Layout == SecurityHeaderLayout.LaxTimestampLast)
            {
                StandardsManager.WSUtilitySpecificationVersion.WriteTimestamp(writer, ElementContainer.Timestamp);
            }
        }

        protected abstract void WriteSecurityTokenReferencyEntry(XmlDictionaryWriter writer, SecurityToken securityToken, SecurityTokenParameters securityTokenParameters);

        public Message SetupExecution()
        {
            ThrowIfProcessingStarted();
            SetProcessingStarted();

            bool signBody = false;
            if (ElementContainer.SourceSigningToken != null)
            {
                if (signatureParts == null)
                {
                    throw TraceUtility.ThrowHelperError(new ArgumentNullException(nameof(SignatureParts)), Message);
                }
                signBody = signatureParts.IsBodyIncluded;
            }

            bool encryptBody = false;
            if (ElementContainer.SourceEncryptionToken != null)
            {
                if (encryptionParts == null)
                {
                    throw TraceUtility.ThrowHelperError(new ArgumentNullException(nameof(EncryptionParts)), Message);
                }
                encryptBody = encryptionParts.IsBodyIncluded;
            }

            SecurityAppliedMessage message = new SecurityAppliedMessage(Message, this, signBody, encryptBody);
            Message = message;
            return message;
        }

        protected internal SecurityTokenReferenceStyle GetTokenReferenceStyle(SecurityTokenParameters parameters)
        {
            return (ShouldSerializeToken(parameters, MessageDirection)) ? SecurityTokenReferenceStyle.Internal : SecurityTokenReferenceStyle.External;
        }

        private void StartSignature()
        {
            if (ElementContainer.SourceSigningToken == null)
            {
                return;
            }

            // determine the key identifier clause to use for the source
            SecurityTokenReferenceStyle sourceSigningKeyReferenceStyle = GetTokenReferenceStyle(SigningTokenParameters);
            SecurityKeyIdentifierClause sourceSigningKeyIdentifierClause = SigningTokenParameters.CreateKeyIdentifierClause(ElementContainer.SourceSigningToken, sourceSigningKeyReferenceStyle);
            if (sourceSigningKeyIdentifierClause == null)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.TokenManagerCannotCreateTokenReference), Message);
            }

            SecurityToken signingToken;
            SecurityKeyIdentifierClause signingKeyIdentifierClause;

            // determine if a token needs to be derived
            if (SigningTokenParameters.RequireDerivedKeys && !SigningTokenParameters.HasAsymmetricKey)
            {
                string derivationAlgorithm = AlgorithmSuite.GetSignatureKeyDerivationAlgorithm(ElementContainer.SourceSigningToken, StandardsManager.MessageSecurityVersion.SecureConversationVersion);
                string expectedDerivationAlgorithm = SecurityUtils.GetKeyDerivationAlgorithm(StandardsManager.MessageSecurityVersion.SecureConversationVersion);
                if (derivationAlgorithm == expectedDerivationAlgorithm)
                {
                    DerivedKeySecurityToken derivedSigningToken = new DerivedKeySecurityToken(-1, 0, AlgorithmSuite.GetSignatureKeyDerivationLength(ElementContainer.SourceSigningToken, StandardsManager.MessageSecurityVersion.SecureConversationVersion), null, DerivedKeySecurityToken.DefaultNonceLength, ElementContainer.SourceSigningToken,
                        sourceSigningKeyIdentifierClause, derivationAlgorithm, GenerateId());
                    signingToken = ElementContainer.DerivedSigningToken = derivedSigningToken;
                    signingKeyIdentifierClause = new LocalIdKeyIdentifierClause(signingToken.Id, signingToken.GetType());
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnsupportedCryptoAlgorithm, derivationAlgorithm)));
                }
            }
            else
            {
                signingToken = ElementContainer.SourceSigningToken;
                signingKeyIdentifierClause = sourceSigningKeyIdentifierClause;
            }

            SecurityKeyIdentifier signingKeyIdentifier = new SecurityKeyIdentifier(signingKeyIdentifierClause);

            if (signatureConfirmationsToSend != null && signatureConfirmationsToSend.Count > 0)
            {
                ISecurityElement[] signatureConfirmationElements;
                signatureConfirmationElements = CreateSignatureConfirmationElements(signatureConfirmationsToSend);
                for (int i = 0; i < signatureConfirmationElements.Length; ++i)
                {
                    SendSecurityHeaderElement sigConfElement = new SendSecurityHeaderElement(signatureConfirmationElements[i].Id, signatureConfirmationElements[i]);
                    sigConfElement.MarkedForEncryption = signatureConfirmationsToSend.IsMarkedForEncryption;
                    ElementContainer.AddSignatureConfirmation(sigConfElement);
                }
            }

            bool generateTargettablePrimarySignature = ((endorsingTokenParameters != null) || (signedEndorsingTokenParameters != null));
            StartPrimarySignatureCore(signingToken, signingKeyIdentifier, signatureParts, generateTargettablePrimarySignature);
        }

        private void CompleteSignature()
        {
            ISignatureValueSecurityElement signedXml = CompletePrimarySignatureCore(
                ElementContainer.GetSignatureConfirmations(), ElementContainer.GetSignedEndorsingSupportingTokens(),
                ElementContainer.GetSignedSupportingTokens(), ElementContainer.GetBasicSupportingTokens(), true);
            if (signedXml == null)
            {
                return;
            }
            ElementContainer.PrimarySignature = new SendSecurityHeaderElement(signedXml.Id, signedXml);
            ElementContainer.PrimarySignature.MarkedForEncryption = encryptSignature;
            AddGeneratedSignatureValue(signedXml.GetSignatureValue(), EncryptPrimarySignature);
            primarySignatureDone = true;
            PrimarySignatureValue = signedXml.GetSignatureValue();
        }

        protected abstract void StartPrimarySignatureCore(SecurityToken token, SecurityKeyIdentifier identifier, MessagePartSpecification signatureParts, bool generateTargettablePrimarySignature);

        protected abstract ISignatureValueSecurityElement CompletePrimarySignatureCore(SendSecurityHeaderElement[] signatureConfirmations,
           SecurityToken[] signedEndorsingTokens, SecurityToken[] signedTokens, SendSecurityHeaderElement[] basicTokens, bool isPrimarySignature);

        protected abstract ISignatureValueSecurityElement CreateSupportingSignature(SecurityToken token, SecurityKeyIdentifier identifier);

        protected abstract ISignatureValueSecurityElement CreateSupportingSignature(SecurityToken token, SecurityKeyIdentifier identifier, ISecurityElement primarySignature);

        protected abstract void StartEncryptionCore(SecurityToken token, SecurityKeyIdentifier keyIdentifier);

        protected abstract ISecurityElement CompleteEncryptionCore(SendSecurityHeaderElement primarySignature,
            SendSecurityHeaderElement[] basicTokens, SendSecurityHeaderElement[] signatureConfirmations, SendSecurityHeaderElement[] endorsingSignatures);

        private void SignWithSupportingToken(SecurityToken token, SecurityKeyIdentifierClause identifierClause)
        {
            if (token == null)
            {
                throw TraceUtility.ThrowHelperArgumentNull(nameof(token), Message);
            }
            if (identifierClause == null)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.TokenManagerCannotCreateTokenReference), Message);
            }
            if (!RequireMessageProtection)
            {
                if (ElementContainer.Timestamp == null)
                {
                    throw TraceUtility.ThrowHelperError(new InvalidOperationException(
                        SR.SigningWithoutPrimarySignatureRequiresTimestamp), Message);
                }
            }
            else
            {
                if (!primarySignatureDone)
                {
                    throw TraceUtility.ThrowHelperError(new InvalidOperationException(
                        SR.PrimarySignatureMustBeComputedBeforeSupportingTokenSignatures), Message);
                }
                if (ElementContainer.PrimarySignature.Item == null)
                {
                    throw TraceUtility.ThrowHelperError(new InvalidOperationException(
                        SR.Format(SR.SupportingTokenSignaturesNotExpected)), Message);
                }
            }

            SecurityKeyIdentifier identifier = new SecurityKeyIdentifier(identifierClause);
            ISignatureValueSecurityElement supportingSignature;
            if (!RequireMessageProtection)
            {
                supportingSignature = CreateSupportingSignature(token, identifier);
            }
            else
            {
                supportingSignature = CreateSupportingSignature(token, identifier, ElementContainer.PrimarySignature.Item);
            }
            AddGeneratedSignatureValue(supportingSignature.GetSignatureValue(), encryptSignature);
            SendSecurityHeaderElement supportingSignatureElement = new SendSecurityHeaderElement(supportingSignature.Id, supportingSignature);
            supportingSignatureElement.MarkedForEncryption = encryptSignature;
            ElementContainer.AddEndorsingSignature(supportingSignatureElement);
        }

        private void SignWithSupportingTokens()
        {
            SecurityToken[] endorsingTokens = ElementContainer.GetEndorsingSupportingTokens();
            if (endorsingTokens != null)
            {
                for (int i = 0; i < endorsingTokens.Length; ++i)
                {
                    SecurityToken source = endorsingTokens[i];
                    SecurityKeyIdentifierClause sourceKeyClause = endorsingTokenParameters[i].CreateKeyIdentifierClause(source, GetTokenReferenceStyle(endorsingTokenParameters[i]));
                    if (sourceKeyClause == null)
                    {
                        throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.TokenManagerCannotCreateTokenReference)), Message);
                    }
                    SecurityToken signingToken;
                    SecurityKeyIdentifierClause signingKeyClause;
                    if (endorsingTokenParameters[i].RequireDerivedKeys && !endorsingTokenParameters[i].HasAsymmetricKey)
                    {
                        string derivationAlgorithm = SecurityUtils.GetKeyDerivationAlgorithm(StandardsManager.MessageSecurityVersion.SecureConversationVersion);
                        DerivedKeySecurityToken dkt = new DerivedKeySecurityToken(-1, 0,
                            AlgorithmSuite.GetSignatureKeyDerivationLength(source, StandardsManager.MessageSecurityVersion.SecureConversationVersion), null,
                            DerivedKeySecurityToken.DefaultNonceLength, source, sourceKeyClause, derivationAlgorithm, GenerateId());
                        signingToken = dkt;
                        signingKeyClause = new LocalIdKeyIdentifierClause(dkt.Id, dkt.GetType());
                        ElementContainer.AddEndorsingDerivedSupportingToken(dkt);
                    }
                    else
                    {
                        signingToken = source;
                        signingKeyClause = sourceKeyClause;
                    }
                    SignWithSupportingToken(signingToken, signingKeyClause);
                }
            }
            SecurityToken[] signedEndorsingSupportingTokens = ElementContainer.GetSignedEndorsingSupportingTokens();
            if (signedEndorsingSupportingTokens != null)
            {
                for (int i = 0; i < signedEndorsingSupportingTokens.Length; ++i)
                {
                    SecurityToken source = signedEndorsingSupportingTokens[i];
                    SecurityKeyIdentifierClause sourceKeyClause = signedEndorsingTokenParameters[i].CreateKeyIdentifierClause(source, GetTokenReferenceStyle(signedEndorsingTokenParameters[i]));
                    if (sourceKeyClause == null)
                    {
                        throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.TokenManagerCannotCreateTokenReference)), Message);
                    }
                    SecurityToken signingToken;
                    SecurityKeyIdentifierClause signingKeyClause;
                    if (signedEndorsingTokenParameters[i].RequireDerivedKeys && !signedEndorsingTokenParameters[i].HasAsymmetricKey)
                    {
                        string derivationAlgorithm = SecurityUtils.GetKeyDerivationAlgorithm(StandardsManager.MessageSecurityVersion.SecureConversationVersion);
                        DerivedKeySecurityToken dkt = new DerivedKeySecurityToken(-1, 0,
                            AlgorithmSuite.GetSignatureKeyDerivationLength(source, StandardsManager.MessageSecurityVersion.SecureConversationVersion), null,
                            DerivedKeySecurityToken.DefaultNonceLength, source, sourceKeyClause, derivationAlgorithm, GenerateId());
                        signingToken = dkt;
                        signingKeyClause = new LocalIdKeyIdentifierClause(dkt.Id, dkt.GetType());
                        ElementContainer.AddSignedEndorsingDerivedSupportingToken(dkt);
                    }
                    else
                    {
                        signingToken = source;
                        signingKeyClause = sourceKeyClause;
                    }
                    SignWithSupportingToken(signingToken, signingKeyClause);
                }
            }
        }

        protected bool ShouldUseStrTransformForToken(SecurityToken securityToken, int position, SecurityTokenAttachmentMode mode, out SecurityKeyIdentifierClause keyIdentifierClause)
        {
            IssuedSecurityTokenParameters tokenParams = null;
            keyIdentifierClause = null;

            switch (mode)
            {
                case SecurityTokenAttachmentMode.SignedEndorsing:
                    tokenParams = signedEndorsingTokenParameters[position] as IssuedSecurityTokenParameters;
                    break;
                case SecurityTokenAttachmentMode.Signed:
                    tokenParams = signedTokenParameters[position] as IssuedSecurityTokenParameters;
                    break;
                case SecurityTokenAttachmentMode.SignedEncrypted:
                    tokenParams = basicSupportingTokenParameters[position] as IssuedSecurityTokenParameters;
                    break;
                default:
                    return false;
            }

            if (tokenParams != null && tokenParams.UseStrTransform)
            {
                keyIdentifierClause = tokenParams.CreateKeyIdentifierClause(securityToken, GetTokenReferenceStyle(tokenParams));
                if (keyIdentifierClause == null)
                {
                    throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.TokenManagerCannotCreateTokenReference)), Message);
                }

                return true;
            }
            return false;
        }

        XmlDictionaryString IMessageHeaderWithSharedNamespace.SharedNamespace => XD.UtilityDictionary.Namespace;

        XmlDictionaryString IMessageHeaderWithSharedNamespace.SharedPrefix => XD.UtilityDictionary.Prefix;

        private void AddGeneratedSignatureValue(byte[] signatureValue, bool wasEncrypted)
        {
            // cache outgoing signatures only on the client side
            if (MaintainSignatureConfirmationState && (signatureConfirmationsToSend == null))
            {
                if (signatureValuesGenerated == null)
                {
                    signatureValuesGenerated = new SignatureConfirmations();
                }
                signatureValuesGenerated.AddConfirmation(signatureValue, wasEncrypted);
            }
        }
    }

    internal class TokenElement : ISecurityElement
    {
        private readonly SecurityStandardsManager standardsManager;

        public TokenElement(SecurityToken token, SecurityStandardsManager standardsManager)
        {
            Token = token;
            this.standardsManager = standardsManager;
        }

        public override bool Equals(object item)
        {
            TokenElement element = item as TokenElement;
            return (element != null && Token == element.Token && standardsManager == element.standardsManager);
        }

        public override int GetHashCode()
        {
            return Token.GetHashCode() ^ standardsManager.GetHashCode();
        }

        public bool HasId => true;

        public string Id => Token.Id;

        public SecurityToken Token { get; }

        public void WriteTo(XmlDictionaryWriter writer, DictionaryManager dictionaryManager)
        {
            standardsManager.SecurityTokenSerializer.WriteToken(writer, Token);
        }
    }
}
