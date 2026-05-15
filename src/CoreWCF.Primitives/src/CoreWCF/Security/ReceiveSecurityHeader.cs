// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Authentication.ExtendedProtection;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Threading;
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
using XmlAttributeHolder = CoreWCF.Channels.XmlAttributeHolder;

namespace CoreWCF.Security
{
    internal abstract class ReceiveSecurityHeader : SecurityHeader
    {
        // client->server symmetric binding case: only primaryTokenAuthenticator is set
        // server->client symmetric binding case: only primary token is set
        // asymmetric binding case: primaryTokenAuthenticator and wrapping token is set

        private SecurityTokenAuthenticator _primaryTokenAuthenticator;
        private SecurityToken _outOfBandPrimaryToken;
        private IList<SecurityToken> _outOfBandPrimaryTokenCollection;
        private SecurityTokenParameters _primaryTokenParameters;
        private TokenTracker _primaryTokenTracker;
        private SecurityToken _wrappingToken;
        private SecurityTokenParameters _wrappingTokenParameters;
        private SecurityTokenAuthenticator _derivedTokenAuthenticator;

        // assumes that the caller has done the check for uniqueness of types
        private IList<SupportingTokenAuthenticatorSpecification> _supportingTokenAuthenticators;
        private ChannelBinding _channelBinding;
        private ExtendedProtectionPolicy _extendedProtectionPolicy;
        private bool _expectEncryption = true;

        // caller should precompute and set expectations
        private bool _expectBasicTokens;
        private bool _expectSignedTokens;
        private bool _expectEndorsingTokens;
        private bool _expectSignature = true;
        private bool _requireSignedPrimaryToken;
        private bool _expectSignatureConfirmation;

        // maps from token to wire form (for basic and signed), and also tracks operations done
        // maps from supporting token parameter to the operations done for that token type
        private List<TokenTracker> _supportingTokenTrackers;
        private SignatureConfirmations _receivedSignatureValues;
        private SignatureConfirmations _receivedSignatureConfirmations;
        private List<SecurityTokenAuthenticator> _allowedAuthenticators;
        private SecurityTokenAuthenticator _pendingSupportingTokenAuthenticator;
        private WrappedKeySecurityToken _wrappedKeyToken;
        private Collection<SecurityToken> _basicTokens;
        private Collection<SecurityToken> _signedTokens;
        private Collection<SecurityToken> _endorsingTokens;
        private Collection<SecurityToken> _signedEndorsingTokens;
        private Dictionary<SecurityToken, ReadOnlyCollection<IAuthorizationPolicy>> _tokenPoliciesMapping;
        private List<SecurityTokenAuthenticator> _wrappedKeyAuthenticator;
        private SecurityHeaderTokenResolver _universalTokenResolver;
        private ReadOnlyCollection<SecurityTokenResolver> _outOfBandTokenResolver;
        private XmlAttributeHolder[] _securityElementAttributes;
        private OrderTracker _orderTracker = new OrderTracker();
        private OperationTracker _signatureTracker = new OperationTracker();
        private OperationTracker _encryptionTracker = new OperationTracker();
        private int _maxDerivedKeys;
        private int _numDerivedKeys;
        private bool _enforceDerivedKeyRequirement = true;
        private NonceCache _nonceCache;
        private TimeSpan _replayWindow;
        private TimeSpan _clockSkew;
        private long _maxReceivedMessageSize = TransportDefaults.MaxReceivedMessageSize;
        private XmlDictionaryReaderQuotas _readerQuotas;
        private MessageProtectionOrder _protectionOrder;
        private bool _hasAtLeastOneSupportingTokenExpectedToBeSigned;
        private bool _hasEndorsingOrSignedEndorsingSupportingTokens;
        private SignatureResourcePool _resourcePool;
        private bool _replayDetectionEnabled = false;
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

        public Collection<SecurityToken> BasicSupportingTokens => _basicTokens;

        public Collection<SecurityToken> SignedSupportingTokens => _signedTokens;

        public Collection<SecurityToken> EndorsingSupportingTokens => _endorsingTokens;

        public ReceiveSecurityHeaderElementManager ElementManager { get; }

        public Collection<SecurityToken> SignedEndorsingSupportingTokens => _signedEndorsingTokens;

        public SecurityTokenAuthenticator DerivedTokenAuthenticator
        {
            get
            {
                return _derivedTokenAuthenticator;
            }
            set
            {
                ThrowIfProcessingStarted();
                _derivedTokenAuthenticator = value;
            }
        }

        public List<SecurityTokenAuthenticator> WrappedKeySecurityTokenAuthenticator
        {
            get
            {
                return _wrappedKeyAuthenticator;
            }
            set
            {
                ThrowIfProcessingStarted();
                _wrappedKeyAuthenticator = value;
            }
        }

        public bool EnforceDerivedKeyRequirement
        {
            get
            {
                return _enforceDerivedKeyRequirement;
            }
            set
            {
                ThrowIfProcessingStarted();
                _enforceDerivedKeyRequirement = value;
            }
        }

        public byte[] PrimarySignatureValue { get; private set; }

        public bool EncryptBeforeSignMode => _orderTracker.EncryptBeforeSignMode;

        public SecurityToken EncryptionToken => _encryptionTracker.Token;

        public bool ExpectBasicTokens
        {
            get { return _expectBasicTokens; }
            set
            {
                ThrowIfProcessingStarted();
                _expectBasicTokens = value;
            }
        }

        public bool ReplayDetectionEnabled
        {
            get { return _replayDetectionEnabled; }
            set
            {
                ThrowIfProcessingStarted();
                _replayDetectionEnabled = value;
            }
        }

        public bool ExpectEncryption
        {
            get { return _expectEncryption; }
            set
            {
                ThrowIfProcessingStarted();
                _expectEncryption = value;
            }
        }

        public bool ExpectSignature
        {
            get { return _expectSignature; }
            set
            {
                ThrowIfProcessingStarted();
                _expectSignature = value;
            }
        }

        public bool ExpectSignatureConfirmation
        {
            get { return _expectSignatureConfirmation; }
            set
            {
                ThrowIfProcessingStarted();
                _expectSignatureConfirmation = value;
            }
        }

        public bool ExpectSignedTokens
        {
            get { return _expectSignedTokens; }
            set
            {
                ThrowIfProcessingStarted();
                _expectSignedTokens = value;
            }
        }

        public bool RequireSignedPrimaryToken
        {
            get { return _requireSignedPrimaryToken; }
            set
            {
                ThrowIfProcessingStarted();
                _requireSignedPrimaryToken = value;
            }
        }

        public bool ExpectEndorsingTokens
        {
            get { return _expectEndorsingTokens; }
            set
            {
                ThrowIfProcessingStarted();
                _expectEndorsingTokens = value;
            }
        }

        public bool HasAtLeastOneItemInsideSecurityHeaderEncrypted { get; set; } = false;

        public SecurityHeaderTokenResolver PrimaryTokenResolver { get; private set; }

        public SecurityTokenResolver CombinedUniversalTokenResolver { get; private set; }

        public SecurityTokenResolver CombinedPrimaryTokenResolver { get; private set; }

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
            if ((_protectionOrder == MessageProtectionOrder.SignBeforeEncryptAndEncryptSignature) &&
                (!_orderTracker.AllSignaturesEncrypted))
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
                return _maxReceivedMessageSize;
            }
            set
            {
                ThrowIfProcessingStarted();
                _maxReceivedMessageSize = value;
            }
        }

        internal XmlDictionaryReaderQuotas ReaderQuotas
        {
            get { return _readerQuotas; }
            set
            {
                ThrowIfProcessingStarted();
                _readerQuotas = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        public override string Name => StandardsManager.SecurityVersion.HeaderName.Value;

        public override string Namespace => StandardsManager.SecurityVersion.HeaderNamespace.Value;

        public Message ProcessedMessage => Message;

        public MessagePartSpecification RequiredEncryptionParts
        {
            get { return _encryptionTracker.Parts; }
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
                _encryptionTracker.Parts = value;
            }
        }

        public MessagePartSpecification RequiredSignatureParts
        {
            get { return _signatureTracker.Parts; }
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
                _signatureTracker.Parts = value;
            }
        }

        protected SignatureResourcePool ResourcePool
        {
            get
            {
                if (_resourcePool == null)
                {
                    _resourcePool = new SignatureResourcePool();
                }
                return _resourcePool;
            }
        }

        internal SecurityVerifiedMessage SecurityVerifiedMessage { get; private set; }

        public SecurityToken SignatureToken => _signatureTracker.Token;

        public Dictionary<SecurityToken, ReadOnlyCollection<IAuthorizationPolicy>> SecurityTokenAuthorizationPoliciesMapping
        {
            get
            {
                if (_tokenPoliciesMapping == null)
                {
                    _tokenPoliciesMapping = new Dictionary<SecurityToken, ReadOnlyCollection<IAuthorizationPolicy>>();
                }
                return _tokenPoliciesMapping;
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
            return _receivedSignatureConfirmations;
        }

        public void ConfigureSymmetricBindingServerReceiveHeader(SecurityTokenAuthenticator primaryTokenAuthenticator, SecurityTokenParameters primaryTokenParameters, IList<SupportingTokenAuthenticatorSpecification> supportingTokenAuthenticators)
        {
            _primaryTokenAuthenticator = primaryTokenAuthenticator;
            _primaryTokenParameters = primaryTokenParameters;
            _supportingTokenAuthenticators = supportingTokenAuthenticators;
        }

        // encrypted key case
        public void ConfigureSymmetricBindingServerReceiveHeader(SecurityToken wrappingToken, SecurityTokenParameters wrappingTokenParameters, IList<SupportingTokenAuthenticatorSpecification> supportingTokenAuthenticators)
        {
            _wrappingToken = wrappingToken;
            _wrappingTokenParameters = wrappingTokenParameters;
            _supportingTokenAuthenticators = supportingTokenAuthenticators;
        }

        public void ConfigureAsymmetricBindingServerReceiveHeader(SecurityTokenAuthenticator primaryTokenAuthenticator, SecurityTokenParameters primaryTokenParameters, SecurityToken wrappingToken, SecurityTokenParameters wrappingTokenParameters, IList<SupportingTokenAuthenticatorSpecification> supportingTokenAuthenticators)
        {
            _primaryTokenAuthenticator = primaryTokenAuthenticator;
            _primaryTokenParameters = primaryTokenParameters;
            _wrappingToken = wrappingToken;
            _wrappingTokenParameters = wrappingTokenParameters;
            _supportingTokenAuthenticators = supportingTokenAuthenticators;
        }

        public void ConfigureTransportBindingServerReceiveHeader(IList<SupportingTokenAuthenticatorSpecification> supportingTokenAuthenticators)
        {
            _supportingTokenAuthenticators = supportingTokenAuthenticators;
        }



        public void ConfigureSymmetricBindingClientReceiveHeader(SecurityToken primaryToken, SecurityTokenParameters primaryTokenParameters)
        {
            _outOfBandPrimaryToken = primaryToken;
            _primaryTokenParameters = primaryTokenParameters;
        }

        public void ConfigureSymmetricBindingClientReceiveHeader(IList<SecurityToken> primaryTokens, SecurityTokenParameters primaryTokenParameters)
        {
            _outOfBandPrimaryTokenCollection = primaryTokens;
            _primaryTokenParameters = primaryTokenParameters;
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
            _outOfBandTokenResolver = outOfBandResolvers;
        }

        protected abstract EncryptedData ReadSecurityHeaderEncryptedItem(XmlDictionaryReader reader, bool readXmlreferenceKeyInfoClause);

        protected abstract byte[] DecryptSecurityHeaderElement(EncryptedData encryptedData, WrappedKeySecurityToken wrappedKeyToken, out SecurityToken encryptionToken);

        protected abstract WrappedKeySecurityToken DecryptWrappedKey(XmlDictionaryReader reader);

        public SignatureConfirmations GetSentSignatureValues()
        {
            return _receivedSignatureValues;
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
            Channels.XmlAttributeHolder[] attributes = _securityElementAttributes;
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
                if (entry.encrypted)
                {
                    XmlDictionaryReader reader = ElementManager.GetReader(i, false);
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

            Fx.Assert(tracker.Spec != null, "Supporting token trackers cannot have null specification.");

            SupportingTokenAuthenticatorSpecification spec = tracker.Spec;

            if (tracker.Token == null)
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
                    EnsureSupportingTokens(ref _endorsingTokens).Add(tracker.Token);
                    break;
                case SecurityTokenAttachmentMode.Signed:
                    if (!tracker.IsSigned && RequireMessageProtection)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.SupportingTokenIsNotSigned, spec.TokenParameters)));
                    }
                    EnsureSupportingTokens(ref _signedTokens).Add(tracker.Token);
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
                    EnsureSupportingTokens(ref _basicTokens).Add(tracker.Token);
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
                    EnsureSupportingTokens(ref _signedEndorsingTokens).Add(tracker.Token);
                    break;

                default:
                    Fx.Assert("Unknown token attachment mode " + spec.SecurityTokenAttachmentMode);
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnknownTokenAttachmentMode, spec.SecurityTokenAttachmentMode)));
            }
        }

        // replay detection done if enableReplayDetection is set to true.
        public void SetTimeParameters(NonceCache nonceCache, TimeSpan replayWindow, TimeSpan clockSkew)
        {
            _nonceCache = nonceCache;
            _replayWindow = replayWindow;
            _clockSkew = clockSkew;
        }

        public async ValueTask ProcessAsync(ChannelBinding channelBinding, ExtendedProtectionPolicy extendedProtectionPolicy)
        {
            Fx.Assert(ReaderQuotas != null, "Reader quotas must be set before processing");
            MessageProtectionOrder actualProtectionOrder = _protectionOrder;
            bool wasProtectionOrderDowngraded = false;
            if (_protectionOrder == MessageProtectionOrder.SignBeforeEncryptAndEncryptSignature)
            {
                if (RequiredEncryptionParts == null || !RequiredEncryptionParts.IsBodyIncluded)
                {
                    // Let's downgrade for now. If after signature verification we find a header that
                    // is signed and encrypted, we will check for signature encryption too.
                    actualProtectionOrder = MessageProtectionOrder.SignBeforeEncrypt;
                    wasProtectionOrderDowngraded = true;
                }
            }

            _channelBinding = channelBinding;
            _extendedProtectionPolicy = extendedProtectionPolicy;
            _orderTracker.SetRequiredProtectionOrder(actualProtectionOrder);

            SetProcessingStarted();
            Message = SecurityVerifiedMessage = new SecurityVerifiedMessage(Message, this);
            XmlDictionaryReader reader = CreateSecurityHeaderReader();
            reader.MoveToStartElement();
            if (reader.IsEmptyElement)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.SecurityHeaderIsEmpty), Message);
            }
            if (RequireMessageProtection)
            {
                _securityElementAttributes = XmlAttributeHolder.ReadAttributes(reader);
            }
            else
            {
                _securityElementAttributes = XmlAttributeHolder.emptyArray;
            }
            reader.ReadStartElement();

            if (_primaryTokenParameters != null)
            {
                _primaryTokenTracker = new TokenTracker(null, _outOfBandPrimaryToken, allowFirstTokenMismatch: false);
            }
            // universalTokenResolver is used for resolving tokens
            _universalTokenResolver = new SecurityHeaderTokenResolver(this);
            // primary token resolver is used for resolving primary signature and decryption
            PrimaryTokenResolver = new SecurityHeaderTokenResolver(this);
            if (_outOfBandPrimaryToken != null)
            {
                _universalTokenResolver.Add(_outOfBandPrimaryToken, SecurityTokenReferenceStyle.External, _primaryTokenParameters);
                PrimaryTokenResolver.Add(_outOfBandPrimaryToken, SecurityTokenReferenceStyle.External, _primaryTokenParameters);
            }
            else if (_outOfBandPrimaryTokenCollection != null)
            {
                for (int i = 0; i < _outOfBandPrimaryTokenCollection.Count; ++i)
                {
                    _universalTokenResolver.Add(_outOfBandPrimaryTokenCollection[i], SecurityTokenReferenceStyle.External, _primaryTokenParameters);
                    PrimaryTokenResolver.Add(_outOfBandPrimaryTokenCollection[i], SecurityTokenReferenceStyle.External, _primaryTokenParameters);
                }
            }
            if (_wrappingToken != null)
            {
                _universalTokenResolver.ExpectedWrapper = _wrappingToken;
                _universalTokenResolver.ExpectedWrapperTokenParameters = _wrappingTokenParameters;
                PrimaryTokenResolver.ExpectedWrapper = _wrappingToken;
                PrimaryTokenResolver.ExpectedWrapperTokenParameters = _wrappingTokenParameters;
            }

            if (_outOfBandTokenResolver == null)
            {
                CombinedUniversalTokenResolver = _universalTokenResolver;
                CombinedPrimaryTokenResolver = PrimaryTokenResolver;
            }
            else
            {
                CombinedUniversalTokenResolver = new AggregateSecurityHeaderTokenResolver(_universalTokenResolver, _outOfBandTokenResolver);
                CombinedPrimaryTokenResolver = new AggregateSecurityHeaderTokenResolver(PrimaryTokenResolver, _outOfBandTokenResolver);
            }

            _allowedAuthenticators = new List<SecurityTokenAuthenticator>();
            if (_primaryTokenAuthenticator != null)
            {
                _allowedAuthenticators.Add(_primaryTokenAuthenticator);
            }
            if (DerivedTokenAuthenticator != null)
            {
                _allowedAuthenticators.Add(DerivedTokenAuthenticator);
            }
            _pendingSupportingTokenAuthenticator = null;
            int numSupportingTokensRequiringDerivation = 0;
            if (_supportingTokenAuthenticators != null && _supportingTokenAuthenticators.Count > 0)
            {
                _supportingTokenTrackers = new List<TokenTracker>(_supportingTokenAuthenticators.Count);
                for (int i = 0; i < _supportingTokenAuthenticators.Count; ++i)
                {
                    SupportingTokenAuthenticatorSpecification spec = _supportingTokenAuthenticators[i];
                    switch (spec.SecurityTokenAttachmentMode)
                    {
                        case SecurityTokenAttachmentMode.Endorsing:
                            _hasEndorsingOrSignedEndorsingSupportingTokens = true;
                            break;
                        case SecurityTokenAttachmentMode.Signed:
                            _hasAtLeastOneSupportingTokenExpectedToBeSigned = true;
                            break;
                        case SecurityTokenAttachmentMode.SignedEndorsing:
                            _hasEndorsingOrSignedEndorsingSupportingTokens = true;
                            _hasAtLeastOneSupportingTokenExpectedToBeSigned = true;
                            break;
                        case SecurityTokenAttachmentMode.SignedEncrypted:
                            _hasAtLeastOneSupportingTokenExpectedToBeSigned = true;
                            break;
                    }

                    if ((_primaryTokenAuthenticator != null) && (_primaryTokenAuthenticator.GetType().Equals(spec.TokenAuthenticator.GetType())))
                    {
                        _pendingSupportingTokenAuthenticator = spec.TokenAuthenticator;
                    }
                    else
                    {
                        _allowedAuthenticators.Add(spec.TokenAuthenticator);
                    }
                    if (spec.TokenParameters.RequireDerivedKeys && !spec.TokenParameters.HasAsymmetricKey &&
                        (spec.SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.Endorsing || spec.SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.SignedEndorsing))
                    {
                        ++numSupportingTokensRequiringDerivation;
                    }
                    _supportingTokenTrackers.Add(new TokenTracker(spec));
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
                _maxDerivedKeys = (1 + 1 + numSupportingTokensRequiringDerivation) * 2;
            }

            SecurityHeaderElementInferenceEngine engine = SecurityHeaderElementInferenceEngine.GetInferenceEngine(Layout);
            await engine.ExecuteProcessingPassesAsync(this, reader);
            if (RequireMessageProtection)
            {
                ElementManager.EnsureAllRequiredSecurityHeaderTargetsWereProtected();
                ExecuteMessageProtectionPass(_hasAtLeastOneSupportingTokenExpectedToBeSigned);
                if (RequiredSignatureParts != null && SignatureToken == null)
                {
                    throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.RequiredSignatureMissing), Message);
                }
            }

            EnsureDecryptionComplete();

            _signatureTracker.SetDerivationSourceIfRequired();
            _encryptionTracker.SetDerivationSourceIfRequired();
            if (EncryptionToken != null)
            {
                if (_wrappingToken != null)
                {
                    if (!(EncryptionToken is WrappedKeySecurityToken token) || token.WrappingToken != _wrappingToken)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.EncryptedKeyWasNotEncryptedWithTheRequiredEncryptingToken, _wrappingToken)));
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
                    if (_primaryTokenParameters != null)
                    {
                        if (_primaryTokenParameters.RequireDerivedKeys && !_primaryTokenParameters.HasAsymmetricKey && !_primaryTokenTracker.IsDerivedFrom)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.PrimarySignatureWasNotSignedByDerivedKey, _primaryTokenParameters)));
                        }
                    }
                    else if (_wrappingTokenParameters != null && _wrappingTokenParameters.RequireDerivedKeys)
                    {
                        if (!_signatureTracker.IsDerivedToken)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.PrimarySignatureWasNotSignedByDerivedWrappedKey, _wrappingTokenParameters)));
                        }
                    }
                }

                // verify that the encryption is using key derivation
                if (EncryptionToken != null)
                {
                    if (_wrappingTokenParameters != null)
                    {
                        if (_wrappingTokenParameters.RequireDerivedKeys && !_encryptionTracker.IsDerivedToken)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.MessageWasNotEncryptedByDerivedWrappedKey, _wrappingTokenParameters)));
                        }
                    }
                    else if (_primaryTokenParameters != null && !_primaryTokenParameters.HasAsymmetricKey && _primaryTokenParameters.RequireDerivedKeys && !_encryptionTracker.IsDerivedToken)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.MessageWasNotEncryptedByDerivedEncryptionToken, _primaryTokenParameters)));
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
            if (_supportingTokenTrackers != null)
            {
                for (int i = 0; i < _supportingTokenTrackers.Count; ++i)
                {
                    VerifySupportingToken(_supportingTokenTrackers[i]);
                }
            }

            if (_replayDetectionEnabled)
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

                AddNonce(_nonceCache, PrimarySignatureValue);

                // if replay detection is on, redo creation range checks to ensure full coverage
                Timestamp.ValidateFreshness(_replayWindow, _clockSkew);
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

        internal async ValueTask ExecuteSignatureEncryptionProcessingPassAsync()
        {
            for (int position = 0; position < ElementManager.Count; position++)
            {
                ElementManager.GetElementEntry(position, out ReceiveSecurityHeaderEntry entry);
                switch (entry.elementCategory)
                {
                    case ReceiveSecurityHeaderElementCategory.Signature:
                        if (entry.bindingMode == ReceiveSecurityHeaderBindingModes.Primary)
                        {
                            await ProcessPrimarySignatureAsync((SignedXml)entry.element, entry.encrypted);
                        }
                        else
                        {
                            await ProcessSupportingSignatureAsync((SignedXml)entry.element, entry.encrypted);
                        }
                        break;
                    case ReceiveSecurityHeaderElementCategory.ReferenceList:
                        ProcessReferenceList((ReferenceList)entry.element);
                        break;
                    case ReceiveSecurityHeaderElementCategory.Token:
                        if ((entry.element is WrappedKeySecurityToken wrappedKeyToken) && (wrappedKeyToken.ReferenceList != null))
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

        internal async ValueTask ExecuteSubheaderDecryptionPassAsync()
        {
            for (int position = 0; position < ElementManager.Count; position++)
            {
                if (ElementManager.GetElementCategory(position) == ReceiveSecurityHeaderElementCategory.EncryptedData)
                {
                    EncryptedData encryptedData = ElementManager.GetElement<EncryptedData>(position);
                    bool dummy = false;
                    await ProcessEncryptedDataAsync(encryptedData, position, false, dummy);
                }
            }
        }

        internal async ValueTask ExecuteReadingPassAsync(XmlDictionaryReader reader)
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
                    await ReadTokenAsync(reader, AppendPosition, null, null, null);
                }
                position++;
            }

            reader.ReadEndElement(); // wsse:Security
            reader.Close();
        }

        internal async ValueTask ExecuteFullPassAsync(XmlDictionaryReader reader)
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
                        await ProcessSupportingSignatureAsync(signedXml, false);
                    }
                    else
                    {
                        primarySignatureFound = true;
                        ElementManager.SetBindingMode(position, ReceiveSecurityHeaderBindingModes.Primary);
                        await ProcessPrimarySignatureAsync(signedXml, false);
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
                    primarySignatureFound = await ProcessEncryptedDataAsync(encryptedData, position, true, primarySignatureFound);
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
                    await ReadTokenAsync(reader, AppendPosition, null, null, null);
                }
                position++;
            }
            reader.ReadEndElement(); // wsse:Security
            reader.Close();
        }

        internal void EnsureDerivedKeyLimitNotReached()
        {
            ++_numDerivedKeys;
            if (_numDerivedKeys > _maxDerivedKeys)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.DerivedKeyLimitExceeded, _maxDerivedKeys)));
            }
        }

        internal void ExecuteDerivedKeyTokenStubPass(bool isFinalPass)
        {
            for (int position = 0; position < ElementManager.Count; position++)
            {
                if (ElementManager.GetElementCategory(position) == ReceiveSecurityHeaderElementCategory.Token)
                {
                    if (ElementManager.GetElement(position) is DerivedKeySecurityTokenStub stub)
                    {
                        _universalTokenResolver.TryResolveToken(stub.TokenToDeriveIdentifier, out SecurityToken sourceToken);
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
            if (token is DerivedKeySecurityToken derivedToken)
            {
                return derivedToken.TokenToDerive;
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
                _securityElementAttributes,
                ReaderQuotas
                );
        }

        private async ValueTask<bool> ProcessEncryptedDataAsync(EncryptedData encryptedData, int position, bool eagerMode, bool primarySignatureFound)
        {
            // if (TD.EncryptedDataProcessingStartIsEnabled())
            // {
            //     TD.EncryptedDataProcessingStart(this.EventTraceActivity);
            // }

            bool result = primarySignatureFound;

            string id = encryptedData.Id;

            byte[] decryptedBuffer = DecryptSecurityHeaderElement(encryptedData, _wrappedKeyToken, out SecurityToken encryptionToken);

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
                        await ProcessSupportingSignatureAsync(signedXml, true);
                    }
                    else
                    {
                        result = true;
                        ElementManager.SetBindingMode(position, ReceiveSecurityHeaderBindingModes.Primary);
                        await ProcessPrimarySignatureAsync(signedXml, true);
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
                    byte[] db = DecryptSecurityHeaderElement(ed, _wrappedKeyToken, out SecurityToken securityToken);
                    XmlDictionaryReader dr = CreateDecryptedReader(db);


                    // read the actual token and put it into the system
                    await ReadTokenAsync(dr, position, db, encryptionToken, id);

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
                    await ReadTokenAsync(decryptedReader, position, decryptedBuffer, encryptionToken, id);
                }
            }

            return result;

            //  if (TD.EncryptedDataProcessingSuccessIsEnabled())
            //  {
            //      TD.EncryptedDataProcessingSuccess(this.EventTraceActivity);
            //  }
        }

        private void ReadEncryptedKey(XmlDictionaryReader reader, bool processReferenceListIfPresent)
        {
            _orderTracker.OnEncryptedKey();

            WrappedKeySecurityToken wrappedKeyToken = DecryptWrappedKey(reader);
            if (wrappedKeyToken.WrappingToken != _wrappingToken)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.EncryptedKeyWasNotEncryptedWithTheRequiredEncryptingToken, _wrappingToken)));
            }
            _universalTokenResolver.Add(wrappedKeyToken);
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
                _wrappedKeyToken = wrappedKeyToken;
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
            _orderTracker.OnProcessReferenceList();
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
            }
            return signedXml;
        }

        protected abstract void ReadSecurityTokenReference(XmlDictionaryReader reader);

        private async ValueTask ProcessPrimarySignatureAsync(SignedXml signedXml, bool isFromDecryptedSource)
        {
            _orderTracker.OnProcessSignature(isFromDecryptedSource);

            PrimarySignatureValue = signedXml.SignatureValue;
            if (_replayDetectionEnabled)
            {
                CheckNonce(_nonceCache, PrimarySignatureValue);
            }

            SecurityToken signingToken = await VerifySignatureAsync(signedXml, true, PrimaryTokenResolver, null, null);
            // verify that the signing token is the same as the primary token
            SecurityToken rootSigningToken = GetRootToken(signingToken);
            bool isDerivedKeySignature = signingToken is DerivedKeySecurityToken;
            if (_primaryTokenTracker != null)
            {
                _primaryTokenTracker.RecordToken(rootSigningToken);
                _primaryTokenTracker.IsDerivedFrom = isDerivedKeySignature;
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
            if (_orderTracker.PrimarySignatureDone)
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
            if (_supportingTokenTrackers == null)
            {
                return null;
            }

            for (int i = 0; i < _supportingTokenTrackers.Count; ++i)
            {
                if (_supportingTokenTrackers[i].Token == token)
                {
                    return _supportingTokenTrackers[i];
                }
            }
            return null;
        }

        protected TokenTracker GetSupportingTokenTracker(SecurityTokenAuthenticator tokenAuthenticator, out SupportingTokenAuthenticatorSpecification spec)
        {
            spec = null;
            if (_supportingTokenAuthenticators == null)
            {
                return null;
            }

            for (int i = 0; i < _supportingTokenAuthenticators.Count; ++i)
            {
                if (_supportingTokenAuthenticators[i].TokenAuthenticator == tokenAuthenticator)
                {
                    spec = _supportingTokenAuthenticators[i];
                    return _supportingTokenTrackers[i];
                }
            }
            return null;
        }

        protected TAuthenticator FindAllowedAuthenticator<TAuthenticator>(bool removeIfPresent)
            where TAuthenticator : SecurityTokenAuthenticator
        {
            if (_allowedAuthenticators == null)
            {
                return null;
            }
            for (int i = 0; i < _allowedAuthenticators.Count; ++i)
            {
                if (_allowedAuthenticators[i] is TAuthenticator result)
                {
                    if (removeIfPresent)
                    {
                        _allowedAuthenticators.RemoveAt(i);
                    }
                    return result;
                }
            }
            return null;
        }

        private async ValueTask ProcessSupportingSignatureAsync(SignedXml signedXml, bool isFromDecryptedSource)
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
                signatureTarget = reader ?? throw TraceUtility.ThrowHelperError(new MessageSecurityException(
                        SR.NoPrimarySignatureAvailableForSupportingTokenSignatureVerification), Message);
            }
            SecurityToken signingToken = await VerifySignatureAsync(signedXml, false, _universalTokenResolver, signatureTarget, id);
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
            bool expectTimestampToBeSigned = RequireMessageProtection || _hasEndorsingOrSignedEndorsingSupportingTokens;
            string expectedDigestAlgorithm = expectTimestampToBeSigned ? AlgorithmSuite.DefaultDigestAlgorithm : null;
            SignatureResourcePool resourcePool = expectTimestampToBeSigned ? ResourcePool : null;
            Timestamp = StandardsManager.WSUtilitySpecificationVersion.ReadTimestamp(reader, expectedDigestAlgorithm, resourcePool);
            Timestamp.ValidateRangeAndFreshness(_replayWindow, _clockSkew);
            ElementManager.AppendTimestamp(Timestamp);
        }

        private bool IsPrimaryToken(SecurityToken token)
        {
            bool result = (token == _outOfBandPrimaryToken
                || (_primaryTokenTracker != null && token == _primaryTokenTracker.Token)
                || ((token is WrappedKeySecurityToken) && ((WrappedKeySecurityToken)token).WrappingToken == _wrappingToken));
            if (!result && _outOfBandPrimaryTokenCollection != null)
            {
                for (int i = 0; i < _outOfBandPrimaryTokenCollection.Count; ++i)
                {
                    if (_outOfBandPrimaryTokenCollection[i] == token)
                    {
                        result = true;
                        break;
                    }
                }
            }
            return result;
        }

        private async ValueTask ReadTokenAsync(XmlDictionaryReader reader, int position, byte[] decryptedBuffer,
            SecurityToken encryptionToken, string idInEncryptedForm)
        {
            Fx.Assert((position == AppendPosition) == (decryptedBuffer == null), "inconsistent position, decryptedBuffer parameters");
            Fx.Assert((position == AppendPosition) == (encryptionToken == null), "inconsistent position, encryptionToken parameters");
            string localName = reader.LocalName;
            string namespaceUri = reader.NamespaceURI;
            string valueType = reader.GetAttribute(XD.SecurityJan2004Dictionary.ValueType, null);

            (SecurityToken token, SecurityTokenAuthenticator usedTokenAuthenticator) = await ReadTokenAsync(reader, CombinedUniversalTokenResolver, _allowedAuthenticators);
            if (token == null)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.TokenManagerCouldNotReadToken, localName, namespaceUri, valueType)), Message);
            }
            if (token is DerivedKeySecurityToken derivedKeyToken)
            {
                EnsureDerivedKeyLimitNotReached();
                derivedKeyToken.InitializeDerivedKey(MaxDerivedKeyLength);
            }

            if (
                //(usedTokenAuthenticator is SspiNegotiationTokenAuthenticator) ||
                (usedTokenAuthenticator == _primaryTokenAuthenticator))
            {
                _allowedAuthenticators.Remove(usedTokenAuthenticator);
            }

            ReceiveSecurityHeaderBindingModes mode;
            TokenTracker supportingTokenTracker = null;
            if (usedTokenAuthenticator == _primaryTokenAuthenticator)
            {
                // this is the primary token. Add to resolver as such
                _universalTokenResolver.Add(token, SecurityTokenReferenceStyle.Internal, _primaryTokenParameters);
                PrimaryTokenResolver.Add(token, SecurityTokenReferenceStyle.Internal, _primaryTokenParameters);
                if (_pendingSupportingTokenAuthenticator != null)
                {
                    _allowedAuthenticators.Add(_pendingSupportingTokenAuthenticator);
                    _pendingSupportingTokenAuthenticator = null;
                }
                _primaryTokenTracker.RecordToken(token);
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
                if (supportingTokenTracker.Token != null)
                {
                    supportingTokenTracker = new TokenTracker(supportingTokenSpec);
                    _supportingTokenTrackers.Add(supportingTokenTracker);
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
                _universalTokenResolver.Add(token, SecurityTokenReferenceStyle.Internal, supportingTokenSpec.TokenParameters);
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

        private async ValueTask<(SecurityToken, SecurityTokenAuthenticator)> ReadTokenAsync(XmlReader reader, SecurityTokenResolver tokenResolver, IList<SecurityTokenAuthenticator> allowedTokenAuthenticators)
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

                // This is just the stub. Nothing to Validate. Return the DerivedKeySecurityTokenAuthenticator.
                return (token, DerivedTokenAuthenticator);
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
                    authorizationPolicies = await tokenAuthenticator.ValidateTokenAsync(token);
                    // }
                    SecurityTokenAuthorizationPoliciesMapping.Add(token, authorizationPolicies);
                    return (token, tokenAuthenticator);
                }
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                SR.Format(SR.UnableToFindTokenAuthenticator, token.GetType())));
        }

        private void AddDerivedKeyTokenToResolvers(SecurityToken token)
        {
            _universalTokenResolver.Add(token);
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
                if (_receivedSignatureConfirmations == null)
                {
                    _receivedSignatureConfirmations = new SignatureConfirmations();
                }
                _receivedSignatureConfirmations.AddConfirmation(signatureValue, isFromDecryptedSource);
            }
        }

        private void AddIncomingSignatureValue(byte[] signatureValue, bool isFromDecryptedSource)
        {
            // cache incoming signatures only on the server side
            if (MaintainSignatureConfirmationState && !ExpectSignatureConfirmation)
            {
                if (_receivedSignatureValues == null)
                {
                    _receivedSignatureValues = new SignatureConfirmations();
                }
                _receivedSignatureValues.AddConfirmation(signatureValue, isFromDecryptedSource);
            }
        }

        protected void RecordEncryptionToken(SecurityToken token)
        {
            _encryptionTracker.RecordToken(token);
        }

        protected void RecordSignatureToken(SecurityToken token)
        {
            _signatureTracker.RecordToken(token);
        }

        public void SetRequiredProtectionOrder(MessageProtectionOrder protectionOrder)
        {
            ThrowIfProcessingStarted();
            _protectionOrder = protectionOrder;
        }

        protected abstract SignedXml ReadSignatureCore(XmlDictionaryReader signatureReader);

        protected abstract ValueTask<SecurityToken> VerifySignatureAsync(SignedXml signedXml, bool isPrimarySignature,
            SecurityHeaderTokenResolver resolver, object signatureTarget, string id);

        protected abstract bool TryDeleteReferenceListEntry(string id);

        private struct OrderTracker
        {
            private static readonly ReceiverProcessingOrder[] s_stateTransitionTableOnDecrypt = new ReceiverProcessingOrder[]
                {
                    ReceiverProcessingOrder.Decrypt, ReceiverProcessingOrder.VerifyDecrypt, ReceiverProcessingOrder.Decrypt,
                    ReceiverProcessingOrder.Mixed, ReceiverProcessingOrder.VerifyDecrypt, ReceiverProcessingOrder.Mixed
                };
            private static readonly ReceiverProcessingOrder[] s_stateTransitionTableOnVerify = new ReceiverProcessingOrder[]
                {
                    ReceiverProcessingOrder.Verify, ReceiverProcessingOrder.Verify, ReceiverProcessingOrder.DecryptVerify,
                    ReceiverProcessingOrder.DecryptVerify, ReceiverProcessingOrder.Mixed, ReceiverProcessingOrder.Mixed
                };
            private const int MaxAllowedWrappedKeys = 1;
            private int _referenceListCount;
            private ReceiverProcessingOrder _state;
            private int _signatureCount;
            private int _unencryptedSignatureCount;
            private int _numWrappedKeys;
            private MessageProtectionOrder _protectionOrder;
            private bool _enforce;

            public bool AllSignaturesEncrypted => _unencryptedSignatureCount == 0;

            public bool EncryptBeforeSignMode => _enforce && _protectionOrder == MessageProtectionOrder.EncryptBeforeSign;

            public bool EncryptBeforeSignOrderRequirementMet => _state != ReceiverProcessingOrder.DecryptVerify && _state != ReceiverProcessingOrder.Mixed;

            public bool PrimarySignatureDone => _signatureCount > 0;

            public bool SignBeforeEncryptOrderRequirementMet => _state != ReceiverProcessingOrder.VerifyDecrypt && _state != ReceiverProcessingOrder.Mixed;

            private void EnforceProtectionOrder()
            {
                switch (_protectionOrder)
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
                                SR.Format(SR.MessageProtectionOrderMismatch, _protectionOrder)));
                        }
                        break;
                    case MessageProtectionOrder.EncryptBeforeSign:
                        if (!EncryptBeforeSignOrderRequirementMet)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                                SR.Format(SR.MessageProtectionOrderMismatch, _protectionOrder)));
                        }
                        break;
                    default:
                        Fx.Assert("");
                        break;
                }
            }

            public void OnProcessReferenceList()
            {
                Fx.Assert(_enforce, "OrderTracker should have 'enforce' set to true.");
                if (_referenceListCount > 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                        SR.AtMostOneReferenceListIsSupportedWithDefaultPolicyCheck));
                }
                _referenceListCount++;
                _state = s_stateTransitionTableOnDecrypt[(int)_state];
                if (_enforce)
                {
                    EnforceProtectionOrder();
                }
            }

            public void OnProcessSignature(bool isEncrypted)
            {
                Fx.Assert(_enforce, "OrderTracker should have 'enforce' set to true.");
                if (_signatureCount > 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.AtMostOneSignatureIsSupportedWithDefaultPolicyCheck));
                }
                _signatureCount++;
                if (!isEncrypted)
                {
                    _unencryptedSignatureCount++;
                }
                _state = s_stateTransitionTableOnVerify[(int)_state];
                if (_enforce)
                {
                    EnforceProtectionOrder();
                }
            }

            public void OnEncryptedKey()
            {
                Fx.Assert(_enforce, "OrderTracker should have 'enforce' set to true.");

                if (_numWrappedKeys == MaxAllowedWrappedKeys)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.WrappedKeyLimitExceeded, _numWrappedKeys)));
                }

                _numWrappedKeys++;
            }

            public void SetRequiredProtectionOrder(MessageProtectionOrder protectionOrder)
            {
                _protectionOrder = protectionOrder;
                _enforce = true;
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
            public MessagePartSpecification Parts { get; set; }

            public SecurityToken Token { get; private set; }

            public bool IsDerivedToken { get; private set; }

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
                if (Token is DerivedKeySecurityToken derivedKeyToken)
                {
                    Token = derivedKeyToken.TokenToDerive;
                    IsDerivedToken = true;
                }
            }
        }
    }

    internal class TokenTracker
    {
        public SecurityToken Token;
        public bool IsDerivedFrom;
        public bool IsSigned;
        public bool IsEncrypted;
        public bool IsEndorsing;
        public bool AlreadyReadEndorsingSignature;
        public SupportingTokenAuthenticatorSpecification Spec;
        private bool _allowFirstTokenMismatch;

        public TokenTracker(SupportingTokenAuthenticatorSpecification spec)
            : this(spec, null, false)
        {
        }

        public TokenTracker(SupportingTokenAuthenticatorSpecification spec, SecurityToken token, bool allowFirstTokenMismatch)
        {
            Spec = spec;
            Token = token;
            _allowFirstTokenMismatch = allowFirstTokenMismatch;
        }

        public void RecordToken(SecurityToken token)
        {
            if (Token == null)
            {
                Token = token;
            }
            else if (_allowFirstTokenMismatch)
            {
                if (!AreTokensEqual(Token, token))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.MismatchInSecurityOperationToken));
                }
                Token = token;
                _allowFirstTokenMismatch = false;
            }
            else if (!ReferenceEquals(Token, token))
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
        private readonly SecurityHeaderTokenResolver _tokenResolver;

        public AggregateSecurityHeaderTokenResolver(SecurityHeaderTokenResolver tokenResolver, ReadOnlyCollection<SecurityTokenResolver> outOfBandTokenResolvers) :
            base(outOfBandTokenResolvers)
        {
            _tokenResolver = tokenResolver ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenResolver));
        }

        protected override bool TryResolveSecurityKeyCore(SecurityKeyIdentifierClause keyIdentifierClause, out SecurityKey key)
        {
            bool resolved = _tokenResolver.TryResolveSecurityKey(keyIdentifierClause, false, out key);

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
            bool resolved = _tokenResolver.TryResolveToken(keyIdentifier, false, false, out token);

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
            else if (keyIdentifierClause is EncryptedKeyIdentifierClause keyClause)
            {
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
            bool resolved = _tokenResolver.TryResolveToken(keyIdentifierClause, false, false, out token);

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
