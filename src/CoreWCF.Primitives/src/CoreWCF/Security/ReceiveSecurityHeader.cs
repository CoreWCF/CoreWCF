// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Authentication.ExtendedProtection;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
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
using XmlAttributeHolder = CoreWCF.Channels.XmlAttributeHolder;

namespace CoreWCF.Security
{
    internal abstract class ReceiveSecurityHeader : SecurityHeader
    {
        // client->server symmetric binding case: only primaryTokenAuthenticator is set
        // server->client symmetric binding case: only primary token is set
        // asymmetric binding case: primaryTokenAuthenticator and wrapping token is set

        private SecurityTokenAuthenticator primaryTokenAuthenticator;
        private readonly bool allowFirstTokenMismatch;
        private SecurityToken outOfBandPrimaryToken;
        private IList<SecurityToken> outOfBandPrimaryTokenCollection;
        private SecurityTokenParameters primaryTokenParameters;
        private TokenTracker primaryTokenTracker;
        private SecurityToken wrappingToken;
        private SecurityTokenParameters wrappingTokenParameters;
        private readonly SecurityToken expectedEncryptionToken;
        private readonly SecurityTokenParameters expectedEncryptionTokenParameters;
        private SecurityTokenAuthenticator derivedTokenAuthenticator;

        // assumes that the caller has done the check for uniqueness of types
        private IList<SupportingTokenAuthenticatorSpecification> supportingTokenAuthenticators;
        private ChannelBinding channelBinding;
        private ExtendedProtectionPolicy extendedProtectionPolicy;
        private bool expectEncryption = true;

        // caller should precompute and set expectations
        private bool expectBasicTokens;
        private bool expectSignedTokens;
        private bool expectEndorsingTokens;
        private bool expectSignature = true;
        private bool requireSignedPrimaryToken;
        private bool expectSignatureConfirmation;

        // maps from token to wire form (for basic and signed), and also tracks operations done
        // maps from supporting token parameter to the operations done for that token type
        private List<TokenTracker> supportingTokenTrackers;
        private SignatureConfirmations receivedSignatureValues;
        private SignatureConfirmations receivedSignatureConfirmations;
        private List<SecurityTokenAuthenticator> allowedAuthenticators;
        private SecurityTokenAuthenticator pendingSupportingTokenAuthenticator;
        private WrappedKeySecurityToken wrappedKeyToken;
        private Collection<SecurityToken> basicTokens;
        private Collection<SecurityToken> signedTokens;
        private Collection<SecurityToken> endorsingTokens;
        private Collection<SecurityToken> signedEndorsingTokens;
        private Dictionary<SecurityToken, ReadOnlyCollection<IAuthorizationPolicy>> tokenPoliciesMapping;
        private List<SecurityTokenAuthenticator> wrappedKeyAuthenticator;
        private SecurityHeaderTokenResolver universalTokenResolver;
        private ReadOnlyCollection<SecurityTokenResolver> outOfBandTokenResolver;
        private SecurityTokenResolver combinedPrimaryTokenResolver;
        private XmlAttributeHolder[] securityElementAttributes;
        private OrderTracker orderTracker = new OrderTracker();
        private OperationTracker signatureTracker = new OperationTracker();
        private OperationTracker encryptionTracker = new OperationTracker();
        private int maxDerivedKeys;
        private int numDerivedKeys;
        private bool enforceDerivedKeyRequirement = true;
        private NonceCache nonceCache;
        private TimeSpan replayWindow;
        private TimeSpan clockSkew;
        private TimeoutHelper timeoutHelper;
        private long maxReceivedMessageSize = TransportDefaults.MaxReceivedMessageSize;
        private XmlDictionaryReaderQuotas readerQuotas;
        private MessageProtectionOrder protectionOrder;
        private bool hasAtLeastOneSupportingTokenExpectedToBeSigned;
        private bool hasEndorsingOrSignedEndorsingSupportingTokens;
        private SignatureResourcePool resourcePool;
        private bool replayDetectionEnabled = false;
        private const int AppendPosition = -1;

        // EventTraceActivity eventTraceActivity;

        protected ReceiveSecurityHeader(Message message, string actor, bool mustUnderstand, bool relay,
            SecurityStandardsManager standardsManager,
            SecurityAlgorithmSuite algorithmSuite,
            int headerIndex,
            MessageDirection direction)
            : base(message, actor, mustUnderstand, relay, standardsManager, algorithmSuite, direction)
        {
            HeaderIndex = headerIndex;
            ElementManager = new ReceiveSecurityHeaderElementManager(this);
        }

        public Collection<SecurityToken> BasicSupportingTokens => basicTokens;

        public Collection<SecurityToken> SignedSupportingTokens => signedTokens;

        public Collection<SecurityToken> EndorsingSupportingTokens => endorsingTokens;

        public ReceiveSecurityHeaderElementManager ElementManager { get; }

        public Collection<SecurityToken> SignedEndorsingSupportingTokens => signedEndorsingTokens;

        public SecurityTokenAuthenticator DerivedTokenAuthenticator
        {
            get
            {
                return derivedTokenAuthenticator;
            }
            set
            {
                ThrowIfProcessingStarted();
                derivedTokenAuthenticator = value;
            }
        }

        public List<SecurityTokenAuthenticator> WrappedKeySecurityTokenAuthenticator
        {
            get
            {
                return wrappedKeyAuthenticator;
            }
            set
            {
                ThrowIfProcessingStarted();
                wrappedKeyAuthenticator = value;
            }
        }

        public bool EnforceDerivedKeyRequirement
        {
            get
            {
                return enforceDerivedKeyRequirement;
            }
            set
            {
                ThrowIfProcessingStarted();
                enforceDerivedKeyRequirement = value;
            }
        }

        public byte[] PrimarySignatureValue { get; private set; }

        public bool EncryptBeforeSignMode => orderTracker.EncryptBeforeSignMode;

        public SecurityToken EncryptionToken => encryptionTracker.Token;

        public bool ExpectBasicTokens
        {
            get { return expectBasicTokens; }
            set
            {
                ThrowIfProcessingStarted();
                expectBasicTokens = value;
            }
        }

        public bool ReplayDetectionEnabled
        {
            get { return replayDetectionEnabled; }
            set
            {
                ThrowIfProcessingStarted();
                replayDetectionEnabled = value;
            }
        }

        public bool ExpectEncryption
        {
            get { return expectEncryption; }
            set
            {
                ThrowIfProcessingStarted();
                expectEncryption = value;
            }
        }

        public bool ExpectSignature
        {
            get { return expectSignature; }
            set
            {
                ThrowIfProcessingStarted();
                expectSignature = value;
            }
        }

        public bool ExpectSignatureConfirmation
        {
            get { return expectSignatureConfirmation; }
            set
            {
                ThrowIfProcessingStarted();
                expectSignatureConfirmation = value;
            }
        }

        public bool ExpectSignedTokens
        {
            get { return expectSignedTokens; }
            set
            {
                ThrowIfProcessingStarted();
                expectSignedTokens = value;
            }
        }

        public bool RequireSignedPrimaryToken
        {
            get { return requireSignedPrimaryToken; }
            set
            {
                ThrowIfProcessingStarted();
                requireSignedPrimaryToken = value;
            }
        }

        public bool ExpectEndorsingTokens
        {
            get { return expectEndorsingTokens; }
            set
            {
                ThrowIfProcessingStarted();
                expectEndorsingTokens = value;
            }
        }

        public bool HasAtLeastOneItemInsideSecurityHeaderEncrypted { get; set; } = false;

        public SecurityHeaderTokenResolver PrimaryTokenResolver { get; private set; }

        public SecurityTokenResolver CombinedUniversalTokenResolver { get; private set; }

        public SecurityTokenResolver CombinedPrimaryTokenResolver => combinedPrimaryTokenResolver;

        //protected EventTraceActivity EventTraceActivity
        //{
        //    get
        //    {
        //        if (this.eventTraceActivity == null && FxTrace.Trace.IsEnd2EndActivityTracingEnabled)
        //        {
        //            this.eventTraceActivity = EventTraceActivityHelper.TryExtractActivity((OperationContext.Current != null) ? OperationContext.Current.IncomingMessage : null);
        //        }

        //        return this.eventTraceActivity;
        //    }
        //}

        protected void VerifySignatureEncryption()
        {
            if ((protectionOrder == MessageProtectionOrder.SignBeforeEncryptAndEncryptSignature) &&
                (!orderTracker.AllSignaturesEncrypted))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                                SR.PrimarySignatureIsRequiredToBeEncrypted));
            }
        }

        internal int HeaderIndex { get; }

        internal long MaxReceivedMessageSize
        {
            get
            {
                return maxReceivedMessageSize;
            }
            set
            {
                ThrowIfProcessingStarted();
                maxReceivedMessageSize = value;
            }
        }

        internal XmlDictionaryReaderQuotas ReaderQuotas
        {
            get { return readerQuotas; }
            set
            {
                ThrowIfProcessingStarted();

                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                readerQuotas = value;
            }
        }

        public override string Name => StandardsManager.SecurityVersion.HeaderName.Value;

        public override string Namespace => StandardsManager.SecurityVersion.HeaderNamespace.Value;

        public Message ProcessedMessage => Message;

        public MessagePartSpecification RequiredEncryptionParts
        {
            get { return encryptionTracker.Parts; }
            set
            {
                ThrowIfProcessingStarted();
                if (value == null)
                {
                    throw TraceUtility.ThrowHelperError(new ArgumentNullException("value"), Message);
                }
                if (!value.IsReadOnly)
                {
                    throw TraceUtility.ThrowHelperError(new InvalidOperationException(
                        SR.MessagePartSpecificationMustBeImmutable), Message);
                }
                encryptionTracker.Parts = value;
            }
        }

        public MessagePartSpecification RequiredSignatureParts
        {
            get { return signatureTracker.Parts; }
            set
            {
                ThrowIfProcessingStarted();
                if (value == null)
                {
                    throw TraceUtility.ThrowHelperError(new ArgumentNullException("value"), Message);
                }
                if (!value.IsReadOnly)
                {
                    throw TraceUtility.ThrowHelperError(new InvalidOperationException(
                        SR.MessagePartSpecificationMustBeImmutable), Message);
                }
                signatureTracker.Parts = value;
            }
        }

        protected SignatureResourcePool ResourcePool
        {
            get
            {
                if (resourcePool == null)
                {
                    resourcePool = new SignatureResourcePool();
                }
                return resourcePool;
            }
        }

        internal SecurityVerifiedMessage SecurityVerifiedMessage { get; private set; }

        public SecurityToken SignatureToken => signatureTracker.Token;

        public Dictionary<SecurityToken, ReadOnlyCollection<IAuthorizationPolicy>> SecurityTokenAuthorizationPoliciesMapping
        {
            get
            {
                if (tokenPoliciesMapping == null)
                {
                    tokenPoliciesMapping = new Dictionary<SecurityToken, ReadOnlyCollection<IAuthorizationPolicy>>();
                }
                return tokenPoliciesMapping;
            }
        }

        public SecurityTimestamp Timestamp { get; private set; }

        public int MaxDerivedKeyLength { get; private set; }

        internal XmlDictionaryReader CreateSecurityHeaderReader()
        {
            return SecurityVerifiedMessage.GetReaderAtSecurityHeader();
        }

        public SignatureConfirmations GetSentSignatureConfirmations()
        {
            return receivedSignatureConfirmations;
        }

        public void ConfigureSymmetricBindingServerReceiveHeader(SecurityTokenAuthenticator primaryTokenAuthenticator, SecurityTokenParameters primaryTokenParameters, IList<SupportingTokenAuthenticatorSpecification> supportingTokenAuthenticators)
        {
            this.primaryTokenAuthenticator = primaryTokenAuthenticator;
            this.primaryTokenParameters = primaryTokenParameters;
            this.supportingTokenAuthenticators = supportingTokenAuthenticators;
        }

        // encrypted key case
        public void ConfigureSymmetricBindingServerReceiveHeader(SecurityToken wrappingToken, SecurityTokenParameters wrappingTokenParameters, IList<SupportingTokenAuthenticatorSpecification> supportingTokenAuthenticators)
        {
            this.wrappingToken = wrappingToken;
            this.wrappingTokenParameters = wrappingTokenParameters;
            this.supportingTokenAuthenticators = supportingTokenAuthenticators;
        }

        public void ConfigureAsymmetricBindingServerReceiveHeader(SecurityTokenAuthenticator primaryTokenAuthenticator, SecurityTokenParameters primaryTokenParameters, SecurityToken wrappingToken, SecurityTokenParameters wrappingTokenParameters, IList<SupportingTokenAuthenticatorSpecification> supportingTokenAuthenticators)
        {
            this.primaryTokenAuthenticator = primaryTokenAuthenticator;
            this.primaryTokenParameters = primaryTokenParameters;
            this.wrappingToken = wrappingToken;
            this.wrappingTokenParameters = wrappingTokenParameters;
            this.supportingTokenAuthenticators = supportingTokenAuthenticators;
        }

        public void ConfigureTransportBindingServerReceiveHeader(IList<SupportingTokenAuthenticatorSpecification> supportingTokenAuthenticators)
        {
            this.supportingTokenAuthenticators = supportingTokenAuthenticators;
        }



        public void ConfigureSymmetricBindingClientReceiveHeader(SecurityToken primaryToken, SecurityTokenParameters primaryTokenParameters)
        {
            outOfBandPrimaryToken = primaryToken;
            this.primaryTokenParameters = primaryTokenParameters;
        }

        public void ConfigureSymmetricBindingClientReceiveHeader(IList<SecurityToken> primaryTokens, SecurityTokenParameters primaryTokenParameters)
        {
            outOfBandPrimaryTokenCollection = primaryTokens;
            this.primaryTokenParameters = primaryTokenParameters;
        }

        public void ConfigureOutOfBandTokenResolver(ReadOnlyCollection<SecurityTokenResolver> outOfBandResolvers)
        {
            if (outOfBandResolvers == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(outOfBandResolvers));
            }

            if (outOfBandResolvers.Count == 0)
            {
                return;
            }
            outOfBandTokenResolver = outOfBandResolvers;
        }

        protected abstract EncryptedData ReadSecurityHeaderEncryptedItem(XmlDictionaryReader reader, bool readXmlreferenceKeyInfoClause);

        protected abstract byte[] DecryptSecurityHeaderElement(EncryptedData encryptedData, WrappedKeySecurityToken wrappedKeyToken, out SecurityToken encryptionToken);

        protected abstract WrappedKeySecurityToken DecryptWrappedKey(XmlDictionaryReader reader);

        public SignatureConfirmations GetSentSignatureValues()
        {
            return receivedSignatureValues;
        }

        protected abstract bool IsReaderAtEncryptedKey(XmlDictionaryReader reader);

        protected abstract bool IsReaderAtEncryptedData(XmlDictionaryReader reader);

        protected abstract bool IsReaderAtReferenceList(XmlDictionaryReader reader);

        protected abstract bool IsReaderAtSignature(XmlDictionaryReader reader);

        protected abstract bool IsReaderAtSecurityTokenReference(XmlDictionaryReader reader);

        protected abstract void OnDecryptionOfSecurityHeaderItemRequiringReferenceListEntry(string id);

        private void MarkHeaderAsUnderstood()
        {
            // header decryption does not reorder or delete headers
            MessageHeaderInfo header = Message.Headers[HeaderIndex];
            Fx.Assert(header.Name == Name && header.Namespace == Namespace && header.Actor == Actor, "security header index mismatch");
            Message.Headers.UnderstoodHeaders.Add(header);
        }

        protected override void OnWriteStartHeader(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            StandardsManager.SecurityVersion.WriteStartHeader(writer);
            Channels.XmlAttributeHolder[] attributes = securityElementAttributes;
            for (int i = 0; i < attributes.Length; ++i)
            {
                writer.WriteAttributeString(attributes[i].Prefix, attributes[i].LocalName, attributes[i].NamespaceUri, attributes[i].Value);
            }
        }

        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            XmlDictionaryReader securityHeaderReader = GetReaderAtSecurityHeader();
            securityHeaderReader.ReadStartElement();
            for (int i = 0; i < ElementManager.Count; ++i)
            {
                ElementManager.GetElementEntry(i, out ReceiveSecurityHeaderEntry entry);
                XmlDictionaryReader reader = null;
                if (entry.encrypted)
                {
                    reader = ElementManager.GetReader(i, false);
                    writer.WriteNode(reader, false);
                    reader.Close();
                    securityHeaderReader.Skip();
                }
                else
                {
                    writer.WriteNode(securityHeaderReader, false);
                }
            }
            securityHeaderReader.Close();
        }

        private XmlDictionaryReader GetReaderAtSecurityHeader()
        {
            XmlDictionaryReader reader = SecurityVerifiedMessage.GetReaderAtFirstHeader();
            for (int i = 0; i < HeaderIndex; ++i)
            {
                reader.Skip();
            }

            return reader;
        }

        private Collection<SecurityToken> EnsureSupportingTokens(ref Collection<SecurityToken> list)
        {
            if (list == null)
            {
                list = new Collection<SecurityToken>();
            }

            return list;
        }

        private void VerifySupportingToken(TokenTracker tracker)
        {
            if (tracker == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tracker));
            }

            Fx.Assert(tracker.spec != null, "Supporting token trackers cannot have null specification.");

            SupportingTokenAuthenticatorSpecification spec = tracker.spec;

            if (tracker.token == null)
            {
                if (spec.IsTokenOptional)
                {
                    return;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.SupportingTokenNotProvided, spec.TokenParameters, spec.SecurityTokenAttachmentMode)));
                }
            }
            switch (spec.SecurityTokenAttachmentMode)
            {
                case SecurityTokenAttachmentMode.Endorsing:
                    if (!tracker.IsEndorsing)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.SupportingTokenIsNotEndorsing, spec.TokenParameters)));
                    }
                    if (EnforceDerivedKeyRequirement && spec.TokenParameters.RequireDerivedKeys && !spec.TokenParameters.HasAsymmetricKey && !tracker.IsDerivedFrom)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.SupportingSignatureIsNotDerivedFrom, spec.TokenParameters)));
                    }
                    EnsureSupportingTokens(ref endorsingTokens).Add(tracker.token);
                    break;
                case SecurityTokenAttachmentMode.Signed:
                    if (!tracker.IsSigned && RequireMessageProtection)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.SupportingTokenIsNotSigned, spec.TokenParameters)));
                    }
                    EnsureSupportingTokens(ref signedTokens).Add(tracker.token);
                    break;
                case SecurityTokenAttachmentMode.SignedEncrypted:
                    if (!tracker.IsSigned && RequireMessageProtection)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.SupportingTokenIsNotSigned, spec.TokenParameters)));
                    }
                    if (!tracker.IsEncrypted && RequireMessageProtection)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.SupportingTokenIsNotEncrypted, spec.TokenParameters)));
                    }
                    EnsureSupportingTokens(ref basicTokens).Add(tracker.token);
                    break;
                case SecurityTokenAttachmentMode.SignedEndorsing:
                    if (!tracker.IsSigned && RequireMessageProtection)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.SupportingTokenIsNotSigned, spec.TokenParameters)));
                    }
                    if (!tracker.IsEndorsing)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.SupportingTokenIsNotEndorsing, spec.TokenParameters)));
                    }
                    if (EnforceDerivedKeyRequirement && spec.TokenParameters.RequireDerivedKeys && !spec.TokenParameters.HasAsymmetricKey && !tracker.IsDerivedFrom)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.SupportingSignatureIsNotDerivedFrom, spec.TokenParameters)));
                    }
                    EnsureSupportingTokens(ref signedEndorsingTokens).Add(tracker.token);
                    break;

                default:
                    Fx.Assert("Unknown token attachment mode " + spec.SecurityTokenAttachmentMode);
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnknownTokenAttachmentMode, spec.SecurityTokenAttachmentMode)));
            }
        }

        // replay detection done if enableReplayDetection is set to true.
        public void SetTimeParameters(NonceCache nonceCache, TimeSpan replayWindow, TimeSpan clockSkew)
        {
            this.nonceCache = nonceCache;
            this.replayWindow = replayWindow;
            this.clockSkew = clockSkew;
        }

        public void Process(TimeSpan timeout, ChannelBinding channelBinding, ExtendedProtectionPolicy extendedProtectionPolicy)
        {
            Fx.Assert(ReaderQuotas != null, "Reader quotas must be set before processing");
            MessageProtectionOrder actualProtectionOrder = protectionOrder;
            bool wasProtectionOrderDowngraded = false;
            if (protectionOrder == MessageProtectionOrder.SignBeforeEncryptAndEncryptSignature)
            {
                if (RequiredEncryptionParts == null || !RequiredEncryptionParts.IsBodyIncluded)
                {
                    // Let's downgrade for now. If after signature verification we find a header that 
                    // is signed and encrypted, we will check for signature encryption too.
                    actualProtectionOrder = MessageProtectionOrder.SignBeforeEncrypt;
                    wasProtectionOrderDowngraded = true;
                }
            }

            this.channelBinding = channelBinding;
            this.extendedProtectionPolicy = extendedProtectionPolicy;
            orderTracker.SetRequiredProtectionOrder(actualProtectionOrder);

            SetProcessingStarted();
            timeoutHelper = new TimeoutHelper(timeout);
            Message = SecurityVerifiedMessage = new SecurityVerifiedMessage(Message, this);
            XmlDictionaryReader reader = CreateSecurityHeaderReader();
            reader.MoveToStartElement();
            if (reader.IsEmptyElement)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.SecurityHeaderIsEmpty), Message);
            }
            if (RequireMessageProtection)
            {
                securityElementAttributes = XmlAttributeHolder.ReadAttributes(reader);
            }
            else
            {
                securityElementAttributes = XmlAttributeHolder.emptyArray;
            }
            reader.ReadStartElement();

            if (primaryTokenParameters != null)
            {
                primaryTokenTracker = new TokenTracker(null, outOfBandPrimaryToken, allowFirstTokenMismatch);
            }
            // universalTokenResolver is used for resolving tokens
            universalTokenResolver = new SecurityHeaderTokenResolver(this);
            // primary token resolver is used for resolving primary signature and decryption
            PrimaryTokenResolver = new SecurityHeaderTokenResolver(this);
            if (outOfBandPrimaryToken != null)
            {
                universalTokenResolver.Add(outOfBandPrimaryToken, SecurityTokenReferenceStyle.External, primaryTokenParameters);
                PrimaryTokenResolver.Add(outOfBandPrimaryToken, SecurityTokenReferenceStyle.External, primaryTokenParameters);
            }
            else if (outOfBandPrimaryTokenCollection != null)
            {
                for (int i = 0; i < outOfBandPrimaryTokenCollection.Count; ++i)
                {
                    universalTokenResolver.Add(outOfBandPrimaryTokenCollection[i], SecurityTokenReferenceStyle.External, primaryTokenParameters);
                    PrimaryTokenResolver.Add(outOfBandPrimaryTokenCollection[i], SecurityTokenReferenceStyle.External, primaryTokenParameters);
                }
            }
            if (wrappingToken != null)
            {
                universalTokenResolver.ExpectedWrapper = wrappingToken;
                universalTokenResolver.ExpectedWrapperTokenParameters = wrappingTokenParameters;
                PrimaryTokenResolver.ExpectedWrapper = wrappingToken;
                PrimaryTokenResolver.ExpectedWrapperTokenParameters = wrappingTokenParameters;
            }
            else if (expectedEncryptionToken != null)
            {
                universalTokenResolver.Add(expectedEncryptionToken, SecurityTokenReferenceStyle.External, expectedEncryptionTokenParameters);
                PrimaryTokenResolver.Add(expectedEncryptionToken, SecurityTokenReferenceStyle.External, expectedEncryptionTokenParameters);
            }

            if (outOfBandTokenResolver == null)
            {
                CombinedUniversalTokenResolver = universalTokenResolver;
                combinedPrimaryTokenResolver = PrimaryTokenResolver;
            }
            else
            {
                CombinedUniversalTokenResolver = new AggregateSecurityHeaderTokenResolver(universalTokenResolver, outOfBandTokenResolver);
                combinedPrimaryTokenResolver = new AggregateSecurityHeaderTokenResolver(PrimaryTokenResolver, outOfBandTokenResolver);
            }

            allowedAuthenticators = new List<SecurityTokenAuthenticator>();
            if (primaryTokenAuthenticator != null)
            {
                allowedAuthenticators.Add(primaryTokenAuthenticator);
            }
            if (DerivedTokenAuthenticator != null)
            {
                allowedAuthenticators.Add(DerivedTokenAuthenticator);
            }
            pendingSupportingTokenAuthenticator = null;
            int numSupportingTokensRequiringDerivation = 0;
            if (supportingTokenAuthenticators != null && supportingTokenAuthenticators.Count > 0)
            {
                supportingTokenTrackers = new List<TokenTracker>(supportingTokenAuthenticators.Count);
                for (int i = 0; i < supportingTokenAuthenticators.Count; ++i)
                {
                    SupportingTokenAuthenticatorSpecification spec = supportingTokenAuthenticators[i];
                    switch (spec.SecurityTokenAttachmentMode)
                    {
                        case SecurityTokenAttachmentMode.Endorsing:
                            hasEndorsingOrSignedEndorsingSupportingTokens = true;
                            break;
                        case SecurityTokenAttachmentMode.Signed:
                            hasAtLeastOneSupportingTokenExpectedToBeSigned = true;
                            break;
                        case SecurityTokenAttachmentMode.SignedEndorsing:
                            hasEndorsingOrSignedEndorsingSupportingTokens = true;
                            hasAtLeastOneSupportingTokenExpectedToBeSigned = true;
                            break;
                        case SecurityTokenAttachmentMode.SignedEncrypted:
                            hasAtLeastOneSupportingTokenExpectedToBeSigned = true;
                            break;
                    }

                    if ((primaryTokenAuthenticator != null) && (primaryTokenAuthenticator.GetType().Equals(spec.TokenAuthenticator.GetType())))
                    {
                        pendingSupportingTokenAuthenticator = spec.TokenAuthenticator;
                    }
                    else
                    {
                        allowedAuthenticators.Add(spec.TokenAuthenticator);
                    }
                    if (spec.TokenParameters.RequireDerivedKeys && !spec.TokenParameters.HasAsymmetricKey &&
                        (spec.SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.Endorsing || spec.SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.SignedEndorsing))
                    {
                        ++numSupportingTokensRequiringDerivation;
                    }
                    supportingTokenTrackers.Add(new TokenTracker(spec));
                }
            }

            if (DerivedTokenAuthenticator != null)
            {
                // we expect key derivation. Compute quotas for derived keys
                int maxKeyDerivationLengthInBits = AlgorithmSuite.DefaultEncryptionKeyDerivationLength >= AlgorithmSuite.DefaultSignatureKeyDerivationLength ?
                    AlgorithmSuite.DefaultEncryptionKeyDerivationLength : AlgorithmSuite.DefaultSignatureKeyDerivationLength;
                MaxDerivedKeyLength = maxKeyDerivationLengthInBits / 8;
                // the upper bound of derived keys is (1 for primary signature + 1 for encryption + supporting token signatures requiring derivation)*2
                // the multiplication by 2 is to take care of interop scenarios that may arise that require more derived keys than the lower bound.
                maxDerivedKeys = (1 + 1 + numSupportingTokensRequiringDerivation) * 2;
            }

            SecurityHeaderElementInferenceEngine engine = SecurityHeaderElementInferenceEngine.GetInferenceEngine(Layout);
            engine.ExecuteProcessingPasses(this, reader);
            if (RequireMessageProtection)
            {
                ElementManager.EnsureAllRequiredSecurityHeaderTargetsWereProtected();
                ExecuteMessageProtectionPass(hasAtLeastOneSupportingTokenExpectedToBeSigned);
                if (RequiredSignatureParts != null && SignatureToken == null)
                {
                    throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.RequiredSignatureMissing), Message);
                }
            }

            EnsureDecryptionComplete();

            signatureTracker.SetDerivationSourceIfRequired();
            encryptionTracker.SetDerivationSourceIfRequired();
            if (EncryptionToken != null)
            {
                if (wrappingToken != null)
                {
                    if (!(EncryptionToken is WrappedKeySecurityToken) || ((WrappedKeySecurityToken)EncryptionToken).WrappingToken != wrappingToken)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.EncryptedKeyWasNotEncryptedWithTheRequiredEncryptingToken, wrappingToken)));
                    }
                }
                else if (expectedEncryptionToken != null)
                {
                    if (EncryptionToken != expectedEncryptionToken)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.MessageWasNotEncryptedWithTheRequiredEncryptingToken));
                    }
                }
                else if (SignatureToken != null && EncryptionToken != SignatureToken)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.SignatureAndEncryptionTokenMismatch, SignatureToken, EncryptionToken)));
                }
            }

            // ensure that the primary signature was signed with derived keys if required
            if (EnforceDerivedKeyRequirement)
            {
                if (SignatureToken != null)
                {
                    if (primaryTokenParameters != null)
                    {
                        if (primaryTokenParameters.RequireDerivedKeys && !primaryTokenParameters.HasAsymmetricKey && !primaryTokenTracker.IsDerivedFrom)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.PrimarySignatureWasNotSignedByDerivedKey, primaryTokenParameters)));
                        }
                    }
                    else if (wrappingTokenParameters != null && wrappingTokenParameters.RequireDerivedKeys)
                    {
                        if (!signatureTracker.IsDerivedToken)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.PrimarySignatureWasNotSignedByDerivedWrappedKey, wrappingTokenParameters)));
                        }
                    }
                }

                // verify that the encryption is using key derivation
                if (EncryptionToken != null)
                {
                    if (wrappingTokenParameters != null)
                    {
                        if (wrappingTokenParameters.RequireDerivedKeys && !encryptionTracker.IsDerivedToken)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.MessageWasNotEncryptedByDerivedWrappedKey, wrappingTokenParameters)));
                        }
                    }
                    else if (expectedEncryptionTokenParameters != null)
                    {
                        if (expectedEncryptionTokenParameters.RequireDerivedKeys && !encryptionTracker.IsDerivedToken)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.MessageWasNotEncryptedByDerivedEncryptionToken, expectedEncryptionTokenParameters)));
                        }
                    }
                    else if (primaryTokenParameters != null && !primaryTokenParameters.HasAsymmetricKey && primaryTokenParameters.RequireDerivedKeys && !encryptionTracker.IsDerivedToken)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.MessageWasNotEncryptedByDerivedEncryptionToken, primaryTokenParameters)));
                    }
                }
            }

            if (wasProtectionOrderDowngraded && (BasicSupportingTokens != null) && (BasicSupportingTokens.Count > 0))
            {
                // Basic tokens are always signed and encrypted. So check if Signatures 
                // are encrypted as well.
                VerifySignatureEncryption();
            }

            // verify all supporting token parameters have their requirements met
            if (supportingTokenTrackers != null)
            {
                for (int i = 0; i < supportingTokenTrackers.Count; ++i)
                {
                    VerifySupportingToken(supportingTokenTrackers[i]);
                }
            }

            if (replayDetectionEnabled)
            {
                if (Timestamp == null)
                {
                    throw TraceUtility.ThrowHelperError(new MessageSecurityException(
                        SR.NoTimestampAvailableInSecurityHeaderToDoReplayDetection), Message);
                }
                if (PrimarySignatureValue == null)
                {
                    throw TraceUtility.ThrowHelperError(new MessageSecurityException(
                        SR.NoSignatureAvailableInSecurityHeaderToDoReplayDetection), Message);
                }

                AddNonce(nonceCache, PrimarySignatureValue);

                // if replay detection is on, redo creation range checks to ensure full coverage
                Timestamp.ValidateFreshness(replayWindow, clockSkew);
            }

            if (ExpectSignatureConfirmation)
            {
                ElementManager.VerifySignatureConfirmationWasFound();
            }

            MarkHeaderAsUnderstood();
        }

        private static void AddNonce(NonceCache cache, byte[] nonce)
        {
            if (!cache.TryAddNonce(nonce))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.InvalidOrReplayedNonce, true));
            }
        }

        private static void CheckNonce(NonceCache cache, byte[] nonce)
        {
            if (cache.CheckNonce(nonce))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.InvalidOrReplayedNonce, true));
            }
        }

        protected abstract void EnsureDecryptionComplete();

        protected abstract void ExecuteMessageProtectionPass(bool hasAtLeastOneSupportingTokenExpectedToBeSigned);

        internal void ExecuteSignatureEncryptionProcessingPass()
        {
            for (int position = 0; position < ElementManager.Count; position++)
            {
                ElementManager.GetElementEntry(position, out ReceiveSecurityHeaderEntry entry);
                switch (entry.elementCategory)
                {
                    case ReceiveSecurityHeaderElementCategory.Signature:
                        if (entry.bindingMode == ReceiveSecurityHeaderBindingModes.Primary)
                        {
                            ProcessPrimarySignature((SignedXml)entry.element, entry.encrypted);
                        }
                        else
                        {
                            ProcessSupportingSignature((SignedXml)entry.element, entry.encrypted);
                        }
                        break;
                    case ReceiveSecurityHeaderElementCategory.ReferenceList:
                        ProcessReferenceList((ReferenceList)entry.element);
                        break;
                    case ReceiveSecurityHeaderElementCategory.Token:
                        WrappedKeySecurityToken wrappedKeyToken = entry.element as WrappedKeySecurityToken;
                        if ((wrappedKeyToken != null) && (wrappedKeyToken.ReferenceList != null))
                        {
                            Fx.Assert(Layout != SecurityHeaderLayout.Strict, "Invalid Calling sequence. This method assumes it will be called only during Lax mode.");
                            // ExecuteSignatureEncryptionProcessingPass is called only durng Lax mode. In this
                            // case when we have a EncryptedKey with a ReferencList inside it, we would not 
                            // have processed the ReferenceList during reading pass. Process this here.
                            ProcessReferenceList(wrappedKeyToken.ReferenceList, wrappedKeyToken);
                        }
                        break;
                    case ReceiveSecurityHeaderElementCategory.Timestamp:
                    case ReceiveSecurityHeaderElementCategory.EncryptedKey:
                    case ReceiveSecurityHeaderElementCategory.EncryptedData:
                    case ReceiveSecurityHeaderElementCategory.SignatureConfirmation:
                    case ReceiveSecurityHeaderElementCategory.SecurityTokenReference:
                        // no op
                        break;
                    default:
                        Fx.Assert("invalid element category");
                        break;
                }
            }
        }

        internal void ExecuteSubheaderDecryptionPass()
        {
            for (int position = 0; position < ElementManager.Count; position++)
            {
                if (ElementManager.GetElementCategory(position) == ReceiveSecurityHeaderElementCategory.EncryptedData)
                {
                    EncryptedData encryptedData = ElementManager.GetElement<EncryptedData>(position);
                    bool dummy = false;
                    ProcessEncryptedData(encryptedData, timeoutHelper.RemainingTime(), position, false, ref dummy);
                }
            }
        }

        internal void ExecuteReadingPass(XmlDictionaryReader reader)
        {
            int position = 0;
            while (reader.IsStartElement())
            {
                if (IsReaderAtSignature(reader))
                {
                    ReadSignature(reader, AppendPosition, null);
                }
                else if (IsReaderAtReferenceList(reader))
                {
                    ReadReferenceList(reader);
                }
                else if (StandardsManager.WSUtilitySpecificationVersion.IsReaderAtTimestamp(reader))
                {
                    ReadTimestamp(reader);
                }
                else if (IsReaderAtEncryptedKey(reader))
                {
                    ReadEncryptedKey(reader, false);
                }
                else if (IsReaderAtEncryptedData(reader))
                {
                    ReadEncryptedData(reader);
                }
                else if (StandardsManager.SecurityVersion.IsReaderAtSignatureConfirmation(reader))
                {
                    ReadSignatureConfirmation(reader, AppendPosition, null);
                }
                else if (IsReaderAtSecurityTokenReference(reader))
                {
                    ReadSecurityTokenReference(reader);
                }
                else
                {
                    ReadToken(reader, AppendPosition, null, null, null, timeoutHelper.RemainingTime());
                }
                position++;
            }

            reader.ReadEndElement(); // wsse:Security
            reader.Close();
        }

        internal void ExecuteFullPass(XmlDictionaryReader reader)
        {
            bool primarySignatureFound = !RequireMessageProtection;
            int position = 0;
            while (reader.IsStartElement())
            {
                if (IsReaderAtSignature(reader))
                {
                    SignedXml signedXml = ReadSignature(reader, AppendPosition, null);
                    if (primarySignatureFound)
                    {
                        ElementManager.SetBindingMode(position, ReceiveSecurityHeaderBindingModes.Endorsing);
                        ProcessSupportingSignature(signedXml, false);
                    }
                    else
                    {
                        primarySignatureFound = true;
                        ElementManager.SetBindingMode(position, ReceiveSecurityHeaderBindingModes.Primary);
                        ProcessPrimarySignature(signedXml, false);
                    }
                }
                else if (IsReaderAtReferenceList(reader))
                {
                    ReferenceList referenceList = ReadReferenceList(reader);
                    ProcessReferenceList(referenceList);
                }
                else if (StandardsManager.WSUtilitySpecificationVersion.IsReaderAtTimestamp(reader))
                {
                    ReadTimestamp(reader);
                }
                else if (IsReaderAtEncryptedKey(reader))
                {
                    ReadEncryptedKey(reader, true);
                }
                else if (IsReaderAtEncryptedData(reader))
                {
                    EncryptedData encryptedData = ReadEncryptedData(reader);
                    ProcessEncryptedData(encryptedData, timeoutHelper.RemainingTime(), position, true, ref primarySignatureFound);
                }
                else if (StandardsManager.SecurityVersion.IsReaderAtSignatureConfirmation(reader))
                {
                    ReadSignatureConfirmation(reader, AppendPosition, null);
                }
                else if (IsReaderAtSecurityTokenReference(reader))
                {
                    ReadSecurityTokenReference(reader);
                }
                else
                {
                    ReadToken(reader, AppendPosition, null, null, null, timeoutHelper.RemainingTime());
                }
                position++;
            }
            reader.ReadEndElement(); // wsse:Security
            reader.Close();
        }

        internal void EnsureDerivedKeyLimitNotReached()
        {
            ++numDerivedKeys;
            if (numDerivedKeys > maxDerivedKeys)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.DerivedKeyLimitExceeded, maxDerivedKeys)));
            }
        }

        internal void ExecuteDerivedKeyTokenStubPass(bool isFinalPass)
        {
            for (int position = 0; position < ElementManager.Count; position++)
            {
                if (ElementManager.GetElementCategory(position) == ReceiveSecurityHeaderElementCategory.Token)
                {
                    DerivedKeySecurityTokenStub stub = ElementManager.GetElement(position) as DerivedKeySecurityTokenStub;
                    if (stub != null)
                    {
                        universalTokenResolver.TryResolveToken(stub.TokenToDeriveIdentifier, out SecurityToken sourceToken);
                        if (sourceToken != null)
                        {
                            EnsureDerivedKeyLimitNotReached();
                            DerivedKeySecurityToken derivedKeyToken = stub.CreateToken(sourceToken, MaxDerivedKeyLength);
                            ElementManager.SetElement(position, derivedKeyToken);
                            AddDerivedKeyTokenToResolvers(derivedKeyToken);
                        }
                        else if (isFinalPass)
                        {
                            throw TraceUtility.ThrowHelperError(new MessageSecurityException(
                                SR.Format(SR.UnableToResolveKeyInfoClauseInDerivedKeyToken, stub.TokenToDeriveIdentifier)), Message);
                        }
                    }
                }
            }
        }

        private SecurityToken GetRootToken(SecurityToken token)
        {
            if (token is DerivedKeySecurityToken)
            {
                return ((DerivedKeySecurityToken)token).TokenToDerive;
            }
            else
            {
                return token;
            }
        }

        private void RecordEncryptionTokenAndRemoveReferenceListEntry(string id, SecurityToken encryptionToken)
        {
            if (id == null)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.MissingIdInEncryptedElement), Message);
            }

            OnDecryptionOfSecurityHeaderItemRequiringReferenceListEntry(id);
            RecordEncryptionToken(encryptionToken);
        }

        private EncryptedData ReadEncryptedData(XmlDictionaryReader reader)
        {
            EncryptedData encryptedData = ReadSecurityHeaderEncryptedItem(reader, MessageDirection == MessageDirection.Output);

            ElementManager.AppendEncryptedData(encryptedData);
            return encryptedData;
        }

        internal XmlDictionaryReader CreateDecryptedReader(byte[] decryptedBuffer)
        {
            return ContextImportHelper.CreateSplicedReader(
                decryptedBuffer,
                SecurityVerifiedMessage.GetEnvelopeAttributes(),
                SecurityVerifiedMessage.GetHeaderAttributes(),
                securityElementAttributes,
                ReaderQuotas
                );
        }

        private void ProcessEncryptedData(EncryptedData encryptedData, TimeSpan timeout, int position, bool eagerMode, ref bool primarySignatureFound)
        {
            // if (TD.EncryptedDataProcessingStartIsEnabled())
            // {
            //     TD.EncryptedDataProcessingStart(this.EventTraceActivity);
            // }

            string id = encryptedData.Id;

            byte[] decryptedBuffer = DecryptSecurityHeaderElement(encryptedData, wrappedKeyToken, out SecurityToken encryptionToken);

            XmlDictionaryReader decryptedReader = CreateDecryptedReader(decryptedBuffer);

            if (IsReaderAtSignature(decryptedReader))
            {
                RecordEncryptionTokenAndRemoveReferenceListEntry(id, encryptionToken);
                SignedXml signedXml = ReadSignature(decryptedReader, position, decryptedBuffer);
                if (eagerMode)
                {
                    if (primarySignatureFound)
                    {
                        ElementManager.SetBindingMode(position, ReceiveSecurityHeaderBindingModes.Endorsing);
                        ProcessSupportingSignature(signedXml, true);
                    }
                    else
                    {
                        primarySignatureFound = true;
                        ElementManager.SetBindingMode(position, ReceiveSecurityHeaderBindingModes.Primary);
                        ProcessPrimarySignature(signedXml, true);
                    }
                }
            }
            else if (StandardsManager.SecurityVersion.IsReaderAtSignatureConfirmation(decryptedReader))
            {
                RecordEncryptionTokenAndRemoveReferenceListEntry(id, encryptionToken);
                ReadSignatureConfirmation(decryptedReader, position, decryptedBuffer);
            }
            else
            {
                if (IsReaderAtEncryptedData(decryptedReader))
                {

                    // The purpose of this code is to process a token that arrived at a client as encryptedData.

                    // This is a common scenario for supporting tokens.

                    // We pass readXmlReferenceKeyIdentifierClause as false here because we do not expect the client 
                    // to receive an encrypted token for itself from the service. The encrypted token is encrypted for some other service. 
                    // Hence we assume that the KeyInfoClause entry in it is not an XMLReference entry that the client is supposed to understand.

                    // What if the service sends its authentication token as an EncryptedData to the client?

                    EncryptedData ed = ReadSecurityHeaderEncryptedItem(decryptedReader, false);
                    byte[] db = DecryptSecurityHeaderElement(ed, wrappedKeyToken, out SecurityToken securityToken);
                    XmlDictionaryReader dr = CreateDecryptedReader(db);


                    // read the actual token and put it into the system
                    ReadToken(dr, position, db, encryptionToken, id, timeout);

                    ElementManager.GetElementEntry(position, out ReceiveSecurityHeaderEntry rshe);

                    // In EncryptBeforeSignMode, we have encrypted the outer token, remember the right id.
                    // The reason why I have both id's is in that case that one or the other is passed
                    // we won't have a problem with which one.  SHP accounting should ensure each item has 
                    // the correct hash.
                    if (EncryptBeforeSignMode)
                    {
                        rshe.encryptedFormId = encryptedData.Id;
                        rshe.encryptedFormWsuId = encryptedData.WsuId;
                    }
                    else
                    {
                        rshe.encryptedFormId = ed.Id;
                        rshe.encryptedFormWsuId = ed.WsuId;
                    }

                    rshe.decryptedBuffer = decryptedBuffer;

                    // setting this to true, will allow a different id match in ReceiveSecurityHeaderEntry.Match
                    // to one of the ids set above as the token id will not match what the signature reference is looking for.

                    rshe.doubleEncrypted = true;

                    ElementManager.ReplaceHeaderEntry(position, rshe);
                }
                else
                {
                    ReadToken(decryptedReader, position, decryptedBuffer, encryptionToken, id, timeout);
                }
            }

            //  if (TD.EncryptedDataProcessingSuccessIsEnabled())
            //  {
            //      TD.EncryptedDataProcessingSuccess(this.EventTraceActivity);
            //  }
        }

        private void ReadEncryptedKey(XmlDictionaryReader reader, bool processReferenceListIfPresent)
        {
            orderTracker.OnEncryptedKey();

            WrappedKeySecurityToken wrappedKeyToken = DecryptWrappedKey(reader);
            if (wrappedKeyToken.WrappingToken != wrappingToken)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.EncryptedKeyWasNotEncryptedWithTheRequiredEncryptingToken, wrappingToken)));
            }
            universalTokenResolver.Add(wrappedKeyToken);
            PrimaryTokenResolver.Add(wrappedKeyToken);
            if (wrappedKeyToken.ReferenceList != null)
            {
                if (!EncryptedKeyContainsReferenceList)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.EncryptedKeyWithReferenceListNotAllowed));
                }
                if (!ExpectEncryption)
                {
                    throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.EncryptionNotExpected), Message);
                }
                if (processReferenceListIfPresent)
                {
                    ProcessReferenceList(wrappedKeyToken.ReferenceList, wrappedKeyToken);
                }
                this.wrappedKeyToken = wrappedKeyToken;
            }
            ElementManager.AppendToken(wrappedKeyToken, ReceiveSecurityHeaderBindingModes.Primary, null);
        }

        private ReferenceList ReadReferenceList(XmlDictionaryReader reader)
        {
            if (!ExpectEncryption)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.EncryptionNotExpected)), Message);
            }
            ReferenceList referenceList = ReadReferenceListCore(reader);
            ElementManager.AppendReferenceList(referenceList);
            return referenceList;
        }

        protected abstract ReferenceList ReadReferenceListCore(XmlDictionaryReader reader);

        private void ProcessReferenceList(ReferenceList referenceList)
        {
            ProcessReferenceList(referenceList, null);
        }

        private void ProcessReferenceList(ReferenceList referenceList, WrappedKeySecurityToken wrappedKeyToken)
        {
            orderTracker.OnProcessReferenceList();
            ProcessReferenceListCore(referenceList, wrappedKeyToken);
        }

        protected abstract void ProcessReferenceListCore(ReferenceList referenceList, WrappedKeySecurityToken wrappedKeyToken);

        private SignedXml ReadSignature(XmlDictionaryReader reader, int position, byte[] decryptedBuffer)
        {
            Fx.Assert((position == AppendPosition) == (decryptedBuffer == null), "inconsistent position, decryptedBuffer parameters");
            if (!ExpectSignature)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.SignatureNotExpected), Message);
            }
            SignedXml signedXml = ReadSignatureCore(reader);
            int readerIndex;
            if (decryptedBuffer == null)
            {
                ElementManager.AppendSignature(signedXml);
                readerIndex = ElementManager.Count - 1;
            }
            else
            {
                ElementManager.SetSignatureAfterDecryption(position, signedXml, decryptedBuffer);
                readerIndex = position;
            }
            return signedXml;
        }

        protected abstract void ReadSecurityTokenReference(XmlDictionaryReader reader);

        private void ProcessPrimarySignature(SignedXml signedXml, bool isFromDecryptedSource)
        {
            orderTracker.OnProcessSignature(isFromDecryptedSource);

            PrimarySignatureValue = signedXml.SignatureValue;
            if (replayDetectionEnabled)
            {
                CheckNonce(nonceCache, PrimarySignatureValue);
            }

            SecurityToken signingToken = VerifySignature(signedXml, true, PrimaryTokenResolver, null, null);
            // verify that the signing token is the same as the primary token
            SecurityToken rootSigningToken = GetRootToken(signingToken);
            bool isDerivedKeySignature = signingToken is DerivedKeySecurityToken;
            if (primaryTokenTracker != null)
            {
                primaryTokenTracker.RecordToken(rootSigningToken);
                primaryTokenTracker.IsDerivedFrom = isDerivedKeySignature;
            }
            AddIncomingSignatureValue(signedXml.SignatureValue, isFromDecryptedSource);
        }

        private void ReadSignatureConfirmation(XmlDictionaryReader reader, int position, byte[] decryptedBuffer)
        {
            Fx.Assert((position == AppendPosition) == (decryptedBuffer == null), "inconsistent position, decryptedBuffer parameters");
            if (!ExpectSignatureConfirmation)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.SignatureConfirmationsNotExpected), Message);
            }
            if (orderTracker.PrimarySignatureDone)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.SignatureConfirmationsOccursAfterPrimarySignature), Message);
            }
            ISignatureValueSecurityElement sigConfElement = StandardsManager.SecurityVersion.ReadSignatureConfirmation(reader);
            if (decryptedBuffer == null)
            {
                AddIncomingSignatureConfirmation(sigConfElement.GetSignatureValue(), false);
                ElementManager.AppendSignatureConfirmation(sigConfElement);
            }
            else
            {
                AddIncomingSignatureConfirmation(sigConfElement.GetSignatureValue(), true);
                ElementManager.SetSignatureConfirmationAfterDecryption(position, sigConfElement, decryptedBuffer);
            }
        }

        private TokenTracker GetSupportingTokenTracker(SecurityToken token)
        {
            if (supportingTokenTrackers == null)
            {
                return null;
            }

            for (int i = 0; i < supportingTokenTrackers.Count; ++i)
            {
                if (supportingTokenTrackers[i].token == token)
                {
                    return supportingTokenTrackers[i];
                }
            }
            return null;
        }

        protected TokenTracker GetSupportingTokenTracker(SecurityTokenAuthenticator tokenAuthenticator, out SupportingTokenAuthenticatorSpecification spec)
        {
            spec = null;
            if (supportingTokenAuthenticators == null)
            {
                return null;
            }

            for (int i = 0; i < supportingTokenAuthenticators.Count; ++i)
            {
                if (supportingTokenAuthenticators[i].TokenAuthenticator == tokenAuthenticator)
                {
                    spec = supportingTokenAuthenticators[i];
                    return supportingTokenTrackers[i];
                }
            }
            return null;
        }

        protected TAuthenticator FindAllowedAuthenticator<TAuthenticator>(bool removeIfPresent)
            where TAuthenticator : SecurityTokenAuthenticator
        {
            if (allowedAuthenticators == null)
            {
                return null;
            }
            for (int i = 0; i < allowedAuthenticators.Count; ++i)
            {
                if (allowedAuthenticators[i] is TAuthenticator)
                {
                    TAuthenticator result = (TAuthenticator)allowedAuthenticators[i];
                    if (removeIfPresent)
                    {
                        allowedAuthenticators.RemoveAt(i);
                    }
                    return result;
                }
            }
            return null;
        }

        private void ProcessSupportingSignature(SignedXml signedXml, bool isFromDecryptedSource)
        {
            if (!ExpectEndorsingTokens)
            {
                throw TraceUtility.ThrowHelperError(new InvalidOperationException(SR.SupportingTokenSignaturesNotExpected), Message);
            }
            string id;
            XmlDictionaryReader reader;
            object signatureTarget;
            if (!RequireMessageProtection)
            {
                if (Timestamp == null)
                {
                    throw TraceUtility.ThrowHelperError(new MessageSecurityException(
                        SR.SigningWithoutPrimarySignatureRequiresTimestamp), Message);
                }
                reader = null;
                id = Timestamp.Id;
                // We would have pre-computed the timestamp digest, if the transport reader
                // was capable of canonicalization. If we were not able to compute the digest
                // before hand then the signature verification step will get a new reader
                // and will recompute the digest.
                signatureTarget = null;
            }
            else
            {
                ElementManager.GetPrimarySignature(out reader, out id);
                if (reader == null)
                {
                    throw TraceUtility.ThrowHelperError(new MessageSecurityException(
                        SR.NoPrimarySignatureAvailableForSupportingTokenSignatureVerification), Message);
                }
                signatureTarget = reader;
            }
            SecurityToken signingToken = VerifySignature(signedXml, false, universalTokenResolver, signatureTarget, id);
            if (reader != null)
            {
                reader.Close();
            }
            if (signingToken == null)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.SignatureVerificationFailed), Message);
            }
            SecurityToken rootSigningToken = GetRootToken(signingToken);
            TokenTracker tracker = GetSupportingTokenTracker(rootSigningToken);
            if (tracker == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.UnknownSupportingToken, signingToken)));
            }

            if (tracker.AlreadyReadEndorsingSignature)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.MoreThanOneSupportingSignature, signingToken)));
            }

            tracker.IsEndorsing = true;
            tracker.AlreadyReadEndorsingSignature = true;
            tracker.IsDerivedFrom = (signingToken is DerivedKeySecurityToken);
            AddIncomingSignatureValue(signedXml.SignatureValue, isFromDecryptedSource);
        }

        private void ReadTimestamp(XmlDictionaryReader reader)
        {
            if (Timestamp != null)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.DuplicateTimestampInSecurityHeader), Message);
            }
            bool expectTimestampToBeSigned = RequireMessageProtection || hasEndorsingOrSignedEndorsingSupportingTokens;
            string expectedDigestAlgorithm = expectTimestampToBeSigned ? AlgorithmSuite.DefaultDigestAlgorithm : null;
            SignatureResourcePool resourcePool = expectTimestampToBeSigned ? ResourcePool : null;
            Timestamp = StandardsManager.WSUtilitySpecificationVersion.ReadTimestamp(reader, expectedDigestAlgorithm, resourcePool);
            Timestamp.ValidateRangeAndFreshness(replayWindow, clockSkew);
            ElementManager.AppendTimestamp(Timestamp);
        }

        private bool IsPrimaryToken(SecurityToken token)
        {
            bool result = (token == outOfBandPrimaryToken
                || (primaryTokenTracker != null && token == primaryTokenTracker.token)
                || (token == expectedEncryptionToken)
                || ((token is WrappedKeySecurityToken) && ((WrappedKeySecurityToken)token).WrappingToken == wrappingToken));
            if (!result && outOfBandPrimaryTokenCollection != null)
            {
                for (int i = 0; i < outOfBandPrimaryTokenCollection.Count; ++i)
                {
                    if (outOfBandPrimaryTokenCollection[i] == token)
                    {
                        result = true;
                        break;
                    }
                }
            }
            return result;
        }

        private void ReadToken(XmlDictionaryReader reader, int position, byte[] decryptedBuffer,
            SecurityToken encryptionToken, string idInEncryptedForm, TimeSpan timeout)
        {
            Fx.Assert((position == AppendPosition) == (decryptedBuffer == null), "inconsistent position, decryptedBuffer parameters");
            Fx.Assert((position == AppendPosition) == (encryptionToken == null), "inconsistent position, encryptionToken parameters");
            string localName = reader.LocalName;
            string namespaceUri = reader.NamespaceURI;
            string valueType = reader.GetAttribute(XD.SecurityJan2004Dictionary.ValueType, null);

            SecurityToken token = ReadToken(reader, CombinedUniversalTokenResolver, allowedAuthenticators, out SecurityTokenAuthenticator usedTokenAuthenticator);
            if (token == null)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.TokenManagerCouldNotReadToken, localName, namespaceUri, valueType)), Message);
            }
            DerivedKeySecurityToken derivedKeyToken = token as DerivedKeySecurityToken;
            if (derivedKeyToken != null)
            {
                EnsureDerivedKeyLimitNotReached();
                derivedKeyToken.InitializeDerivedKey(MaxDerivedKeyLength);
            }

            if (
                //(usedTokenAuthenticator is SspiNegotiationTokenAuthenticator) ||
                (usedTokenAuthenticator == primaryTokenAuthenticator))
            {
                allowedAuthenticators.Remove(usedTokenAuthenticator);
            }

            ReceiveSecurityHeaderBindingModes mode;
            TokenTracker supportingTokenTracker = null;
            if (usedTokenAuthenticator == primaryTokenAuthenticator)
            {
                // this is the primary token. Add to resolver as such
                universalTokenResolver.Add(token, SecurityTokenReferenceStyle.Internal, primaryTokenParameters);
                PrimaryTokenResolver.Add(token, SecurityTokenReferenceStyle.Internal, primaryTokenParameters);
                if (pendingSupportingTokenAuthenticator != null)
                {
                    allowedAuthenticators.Add(pendingSupportingTokenAuthenticator);
                    pendingSupportingTokenAuthenticator = null;
                }
                primaryTokenTracker.RecordToken(token);
                mode = ReceiveSecurityHeaderBindingModes.Primary;
            }
            else if (usedTokenAuthenticator == DerivedTokenAuthenticator)
            {
                if (token is DerivedKeySecurityTokenStub)
                {
                    if (Layout == SecurityHeaderLayout.Strict)
                    {
                        DerivedKeySecurityTokenStub tmpToken = (DerivedKeySecurityTokenStub)token;
                        throw TraceUtility.ThrowHelperError(new MessageSecurityException(
                            SR.Format(SR.UnableToResolveKeyInfoClauseInDerivedKeyToken, tmpToken.TokenToDeriveIdentifier)), Message);
                    }
                }
                else
                {
                    AddDerivedKeyTokenToResolvers(token);
                }
                mode = ReceiveSecurityHeaderBindingModes.Unknown;
            }
            else
            {
                supportingTokenTracker = GetSupportingTokenTracker(usedTokenAuthenticator, out SupportingTokenAuthenticatorSpecification supportingTokenSpec);
                if (supportingTokenTracker == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.UnknownTokenAuthenticatorUsedInTokenProcessing, usedTokenAuthenticator)));
                }
                if (supportingTokenTracker.token != null)
                {
                    supportingTokenTracker = new TokenTracker(supportingTokenSpec);
                    supportingTokenTrackers.Add(supportingTokenTracker);
                }

                supportingTokenTracker.RecordToken(token);
                if (encryptionToken != null)
                {
                    supportingTokenTracker.IsEncrypted = true;
                }

                SecurityTokenAttachmentModeHelper.Categorize(supportingTokenSpec.SecurityTokenAttachmentMode,
                   out bool isBasic, out bool isSignedButNotBasic, out mode);
                if (isBasic)
                {
                    if (!ExpectBasicTokens)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.BasicTokenNotExpected));
                    }

                    // only basic tokens have to be part of the reference list. Encrypted Saml tokens dont for example
                    if (RequireMessageProtection && encryptionToken != null)
                    {
                        RecordEncryptionTokenAndRemoveReferenceListEntry(idInEncryptedForm, encryptionToken);
                    }
                }
                if (isSignedButNotBasic && !ExpectSignedTokens)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.SignedSupportingTokenNotExpected));
                }
                universalTokenResolver.Add(token, SecurityTokenReferenceStyle.Internal, supportingTokenSpec.TokenParameters);
            }
            if (position == AppendPosition)
            {
                ElementManager.AppendToken(token, mode, supportingTokenTracker);
            }
            else
            {
                ElementManager.SetTokenAfterDecryption(position, token, mode, decryptedBuffer, supportingTokenTracker);
            }
        }

        private SecurityToken ReadToken(XmlReader reader, SecurityTokenResolver tokenResolver, IList<SecurityTokenAuthenticator> allowedTokenAuthenticators, out SecurityTokenAuthenticator usedTokenAuthenticator)
        {
            SecurityToken token = StandardsManager.SecurityTokenSerializer.ReadToken(reader, tokenResolver);
            if (token is DerivedKeySecurityTokenStub)
            {
                if (DerivedTokenAuthenticator == null)
                {
                    // No Authenticator registered for DerivedKeySecurityToken
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                        SR.Format(SR.UnableToFindTokenAuthenticator, typeof(DerivedKeySecurityToken))));
                }

                // This is just the stub. Nothing to Validate. Set the usedTokenAuthenticator to 
                // DerivedKeySecurityTokenAuthenticator.
                usedTokenAuthenticator = DerivedTokenAuthenticator;
                return token;
            }

            for (int i = 0; i < allowedTokenAuthenticators.Count; ++i)
            {
                SecurityTokenAuthenticator tokenAuthenticator = allowedTokenAuthenticators[i];
                if (tokenAuthenticator.CanValidateToken(token))
                {
                    ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies;
                    //ServiceCredentialsSecurityTokenManager.KerberosSecurityTokenAuthenticatorWrapper kerbTokenAuthenticator =
                    //        tokenAuthenticator as ServiceCredentialsSecurityTokenManager.KerberosSecurityTokenAuthenticatorWrapper;
                    //if (kerbTokenAuthenticator != null)
                    //{
                    //    authorizationPolicies = kerbTokenAuthenticator.ValidateToken(token, this.channelBinding, this.extendedProtectionPolicy);
                    //}
                    //else
                    //{
                    authorizationPolicies = tokenAuthenticator.ValidateToken(token);
                    // }
                    SecurityTokenAuthorizationPoliciesMapping.Add(token, authorizationPolicies);
                    usedTokenAuthenticator = tokenAuthenticator;
                    return token;
                }
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                SR.Format(SR.UnableToFindTokenAuthenticator, token.GetType())));
        }

        private void AddDerivedKeyTokenToResolvers(SecurityToken token)
        {
            universalTokenResolver.Add(token);
            // add it to the primary token resolver only if its root is primary
            SecurityToken rootToken = GetRootToken(token);
            if (IsPrimaryToken(rootToken))
            {
                PrimaryTokenResolver.Add(token);
            }
        }

        private void AddIncomingSignatureConfirmation(byte[] signatureValue, bool isFromDecryptedSource)
        {
            if (MaintainSignatureConfirmationState)
            {
                if (receivedSignatureConfirmations == null)
                {
                    receivedSignatureConfirmations = new SignatureConfirmations();
                }
                receivedSignatureConfirmations.AddConfirmation(signatureValue, isFromDecryptedSource);
            }
        }

        private void AddIncomingSignatureValue(byte[] signatureValue, bool isFromDecryptedSource)
        {
            // cache incoming signatures only on the server side
            if (MaintainSignatureConfirmationState && !ExpectSignatureConfirmation)
            {
                if (receivedSignatureValues == null)
                {
                    receivedSignatureValues = new SignatureConfirmations();
                }
                receivedSignatureValues.AddConfirmation(signatureValue, isFromDecryptedSource);
            }
        }

        protected void RecordEncryptionToken(SecurityToken token)
        {
            encryptionTracker.RecordToken(token);
        }

        protected void RecordSignatureToken(SecurityToken token)
        {
            signatureTracker.RecordToken(token);
        }

        public void SetRequiredProtectionOrder(MessageProtectionOrder protectionOrder)
        {
            ThrowIfProcessingStarted();
            this.protectionOrder = protectionOrder;
        }

        protected abstract SignedXml ReadSignatureCore(XmlDictionaryReader signatureReader);

        protected abstract SecurityToken VerifySignature(SignedXml signedXml, bool isPrimarySignature,
            SecurityHeaderTokenResolver resolver, object signatureTarget, string id);

        protected abstract bool TryDeleteReferenceListEntry(string id);

        private struct OrderTracker
        {
            private static readonly ReceiverProcessingOrder[] stateTransitionTableOnDecrypt = new ReceiverProcessingOrder[]
                {
                    ReceiverProcessingOrder.Decrypt, ReceiverProcessingOrder.VerifyDecrypt, ReceiverProcessingOrder.Decrypt,
                    ReceiverProcessingOrder.Mixed, ReceiverProcessingOrder.VerifyDecrypt, ReceiverProcessingOrder.Mixed
                };
            private static readonly ReceiverProcessingOrder[] stateTransitionTableOnVerify = new ReceiverProcessingOrder[]
                {
                    ReceiverProcessingOrder.Verify, ReceiverProcessingOrder.Verify, ReceiverProcessingOrder.DecryptVerify,
                    ReceiverProcessingOrder.DecryptVerify, ReceiverProcessingOrder.Mixed, ReceiverProcessingOrder.Mixed
                };
            private const int MaxAllowedWrappedKeys = 1;
            private int referenceListCount;
            private ReceiverProcessingOrder state;
            private int signatureCount;
            private int unencryptedSignatureCount;
            private int numWrappedKeys;
            private MessageProtectionOrder protectionOrder;
            private bool enforce;

            public bool AllSignaturesEncrypted => unencryptedSignatureCount == 0;

            public bool EncryptBeforeSignMode => enforce && protectionOrder == MessageProtectionOrder.EncryptBeforeSign;

            public bool EncryptBeforeSignOrderRequirementMet => state != ReceiverProcessingOrder.DecryptVerify && state != ReceiverProcessingOrder.Mixed;

            public bool PrimarySignatureDone => signatureCount > 0;

            public bool SignBeforeEncryptOrderRequirementMet => state != ReceiverProcessingOrder.VerifyDecrypt && state != ReceiverProcessingOrder.Mixed;

            private void EnforceProtectionOrder()
            {
                switch (protectionOrder)
                {
                    case MessageProtectionOrder.SignBeforeEncryptAndEncryptSignature:
                        if (!AllSignaturesEncrypted)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                             SR.PrimarySignatureIsRequiredToBeEncrypted));
                        }
                        goto case MessageProtectionOrder.SignBeforeEncrypt;
                    case MessageProtectionOrder.SignBeforeEncrypt:
                        if (!SignBeforeEncryptOrderRequirementMet)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                                SR.Format(SR.MessageProtectionOrderMismatch, protectionOrder)));
                        }
                        break;
                    case MessageProtectionOrder.EncryptBeforeSign:
                        if (!EncryptBeforeSignOrderRequirementMet)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                                SR.Format(SR.MessageProtectionOrderMismatch, protectionOrder)));
                        }
                        break;
                    default:
                        Fx.Assert("");
                        break;
                }
            }

            public void OnProcessReferenceList()
            {
                Fx.Assert(enforce, "OrderTracker should have 'enforce' set to true.");
                if (referenceListCount > 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                        SR.AtMostOneReferenceListIsSupportedWithDefaultPolicyCheck));
                }
                referenceListCount++;
                state = stateTransitionTableOnDecrypt[(int)state];
                if (enforce)
                {
                    EnforceProtectionOrder();
                }
            }

            public void OnProcessSignature(bool isEncrypted)
            {
                Fx.Assert(enforce, "OrderTracker should have 'enforce' set to true.");
                if (signatureCount > 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.AtMostOneSignatureIsSupportedWithDefaultPolicyCheck));
                }
                signatureCount++;
                if (!isEncrypted)
                {
                    unencryptedSignatureCount++;
                }
                state = stateTransitionTableOnVerify[(int)state];
                if (enforce)
                {
                    EnforceProtectionOrder();
                }
            }

            public void OnEncryptedKey()
            {
                Fx.Assert(enforce, "OrderTracker should have 'enforce' set to true.");

                if (numWrappedKeys == MaxAllowedWrappedKeys)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.WrappedKeyLimitExceeded, numWrappedKeys)));
                }

                numWrappedKeys++;
            }

            public void SetRequiredProtectionOrder(MessageProtectionOrder protectionOrder)
            {
                this.protectionOrder = protectionOrder;
                enforce = true;
            }

            private enum ReceiverProcessingOrder : int
            {
                None = 0,
                Verify = 1,
                Decrypt = 2,
                DecryptVerify = 3,
                VerifyDecrypt = 4,
                Mixed = 5
            }
        }

        private struct OperationTracker
        {
            private bool isDerivedToken;

            public MessagePartSpecification Parts { get; set; }

            public SecurityToken Token { get; private set; }

            public bool IsDerivedToken => isDerivedToken;

            public void RecordToken(SecurityToken token)
            {
                if (Token == null)
                {
                    Token = token;
                }
                else if (!ReferenceEquals(Token, token))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.MismatchInSecurityOperationToken));
                }
            }

            public void SetDerivationSourceIfRequired()
            {
                DerivedKeySecurityToken derivedKeyToken = Token as DerivedKeySecurityToken;
                if (derivedKeyToken != null)
                {
                    Token = derivedKeyToken.TokenToDerive;
                    isDerivedToken = true;
                }
            }
        }
    }

    internal class TokenTracker
    {
        public SecurityToken token;
        public bool IsDerivedFrom;
        public bool IsSigned;
        public bool IsEncrypted;
        public bool IsEndorsing;
        public bool AlreadyReadEndorsingSignature;
        private bool allowFirstTokenMismatch;
        public SupportingTokenAuthenticatorSpecification spec;

        public TokenTracker(SupportingTokenAuthenticatorSpecification spec)
            : this(spec, null, false)
        {
        }

        public TokenTracker(SupportingTokenAuthenticatorSpecification spec, SecurityToken token, bool allowFirstTokenMismatch)
        {
            this.spec = spec;
            this.token = token;
            this.allowFirstTokenMismatch = allowFirstTokenMismatch;
        }

        public void RecordToken(SecurityToken token)
        {
            if (this.token == null)
            {
                this.token = token;
            }
            else if (allowFirstTokenMismatch)
            {
                if (!AreTokensEqual(this.token, token))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.MismatchInSecurityOperationToken));
                }
                this.token = token;
                allowFirstTokenMismatch = false;
            }
            else if (!object.ReferenceEquals(this.token, token))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.MismatchInSecurityOperationToken));
            }
        }

        private static bool AreTokensEqual(SecurityToken outOfBandToken, SecurityToken replyToken)
        {
            // we support the serialized reply token legacy feature only for X509 certificates.
            // in this case the thumbprint of the reply certificate must match the outofband certificate's thumbprint
            if ((outOfBandToken is X509SecurityToken) && (replyToken is X509SecurityToken))
            {
                byte[] outOfBandCertificateThumbprint = ((X509SecurityToken)outOfBandToken).Certificate.GetCertHash();
                byte[] replyCertificateThumbprint = ((X509SecurityToken)replyToken).Certificate.GetCertHash();
                return (CryptoHelper.IsEqual(outOfBandCertificateThumbprint, replyCertificateThumbprint));
            }
            else
            {
                return false;
            }
        }
    }

    internal class AggregateSecurityHeaderTokenResolver : CoreWCF.IdentityModel.Tokens.AggregateTokenResolver
    {
        private readonly SecurityHeaderTokenResolver tokenResolver;

        public AggregateSecurityHeaderTokenResolver(SecurityHeaderTokenResolver tokenResolver, ReadOnlyCollection<SecurityTokenResolver> outOfBandTokenResolvers) :
            base(outOfBandTokenResolvers)
        {
            if (tokenResolver == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenResolver));
            }

            this.tokenResolver = tokenResolver;
        }

        protected override bool TryResolveSecurityKeyCore(SecurityKeyIdentifierClause keyIdentifierClause, out SecurityKey key)
        {
            bool resolved = false;
            key = null;

            resolved = tokenResolver.TryResolveSecurityKey(keyIdentifierClause, false, out key);

            if (!resolved)
            {
                resolved = base.TryResolveSecurityKeyCore(keyIdentifierClause, out key);
            }

            if (!resolved)
            {
                resolved = SecurityUtils.TryCreateKeyFromIntrinsicKeyClause(keyIdentifierClause, this, out key);
            }

            return resolved;
        }

        protected override bool TryResolveTokenCore(SecurityKeyIdentifier keyIdentifier, out SecurityToken token)
        {
            bool resolved = false;
            token = null;

            resolved = tokenResolver.TryResolveToken(keyIdentifier, false, false, out token);

            if (!resolved)
            {
                resolved = base.TryResolveTokenCore(keyIdentifier, out token);
            }

            if (!resolved)
            {
                for (int i = 0; i < keyIdentifier.Count; ++i)
                {
                    if (TryResolveTokenFromIntrinsicKeyClause(keyIdentifier[i], out token))
                    {
                        resolved = true;
                        break;
                    }
                }
            }

            return resolved;
        }

        private bool TryResolveTokenFromIntrinsicKeyClause(SecurityKeyIdentifierClause keyIdentifierClause, out SecurityToken token)
        {
            token = null;
            if (keyIdentifierClause is RsaKeyIdentifierClause)
            {
                token = new RsaSecurityToken(((RsaKeyIdentifierClause)keyIdentifierClause).Rsa);
                return true;
            }
            else if (keyIdentifierClause is X509RawDataKeyIdentifierClause)
            {
                token = new X509SecurityToken(new X509Certificate2(((X509RawDataKeyIdentifierClause)keyIdentifierClause).GetX509RawData()), false);
                return true;
            }
            else if (keyIdentifierClause is EncryptedKeyIdentifierClause)
            {
                EncryptedKeyIdentifierClause keyClause = (EncryptedKeyIdentifierClause)keyIdentifierClause;
                SecurityKeyIdentifier wrappingTokenReference = keyClause.EncryptingKeyIdentifier;
                if (TryResolveToken(wrappingTokenReference, out SecurityToken unwrappingToken))
                {
                    token = SecurityUtils.CreateTokenFromEncryptedKeyClause(keyClause, unwrappingToken);
                    return true;
                }
            }
            return false;
        }

        protected override bool TryResolveTokenCore(SecurityKeyIdentifierClause keyIdentifierClause, out SecurityToken token)
        {
            bool resolved = false;
            token = null;

            resolved = tokenResolver.TryResolveToken(keyIdentifierClause, false, false, out token);

            if (!resolved)
            {
                resolved = base.TryResolveTokenCore(keyIdentifierClause, out token);
            }

            if (!resolved)
            {
                resolved = TryResolveTokenFromIntrinsicKeyClause(keyIdentifierClause, out token);
            }

            return resolved;
        }
    }
}
