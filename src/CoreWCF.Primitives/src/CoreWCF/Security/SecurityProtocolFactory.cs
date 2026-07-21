// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Authentication.ExtendedProtection;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    /*
     * See
     * http://xws/gxa/main/specs/security/security_profiles/SecurityProfiles.doc
     * for details on security protocols

     * Concrete implementations are required to me thread safe after
     * Open() is called;

     * instances of concrete protocol factories are scoped to a
     * channel/listener factory;

     * Each channel/listener factory must have a
     * SecurityProtocolFactory set on it before open/first use; the
     * factory instance cannot be changed once the factory is opened
     * or listening;

     * security protocol instances are scoped to a channel and will be
     * created by the Create calls on protocol factories;

     * security protocol instances are required to be thread-safe.

     * for typical subclasses, factory wide state and immutable
     * settings are expected to be on the ProtocolFactory itself while
     * channel-wide state is maintained internally in each security
     * protocol instance;

     * the security protocol instance set on a channel cannot be
     * changed; however, the protocol instance may change internal
     * state; this covers RM's SCT renego case; by keeping state
     * change internal to protocol instances, we get better
     * coordination with concurrent message security on channels;

     * the primary pivot in creating a security protocol instance is
     * initiator (client) vs. responder (server), NOT sender vs
     * receiver

     * Create calls for input and reply channels will contain the
     * listener-wide state (if any) created by the corresponding call
     * on the factory;

     */

    // Whether we need to add support for targetting different SOAP roles is tracked by 19144
    public abstract class SecurityProtocolFactory : ISecurityCommunicationObject
    {
        internal const bool defaultAddTimestamp = true;
        internal const bool defaultDeriveKeys = true;
        internal const bool defaultDetectReplays = true;
        internal const string defaultMaxClockSkewString = "00:05:00";
        internal const string defaultReplayWindowString = "00:05:00";
        internal static readonly TimeSpan defaultMaxClockSkew = TimeSpan.Parse(defaultMaxClockSkewString, CultureInfo.InvariantCulture);
        internal static readonly TimeSpan defaultReplayWindow = TimeSpan.Parse(defaultReplayWindowString, CultureInfo.InvariantCulture);
        internal const int defaultMaxCachedNonces = 900000;
        internal const string defaultTimestampValidityDurationString = "00:05:00";
        internal static readonly TimeSpan defaultTimestampValidityDuration = TimeSpan.Parse(defaultTimestampValidityDurationString, CultureInfo.InvariantCulture);
        internal const SecurityHeaderLayout defaultSecurityHeaderLayout = SecurityHeaderLayout.Strict;
        private static ReadOnlyCollection<SupportingTokenAuthenticatorSpecification> s_emptyTokenAuthenticators;
        private bool _addTimestamp = defaultAddTimestamp;
        private bool _detectReplays = defaultDetectReplays;
        private SecurityAlgorithmSuite _incomingAlgorithmSuite = SecurityAlgorithmSuite.Default;
        private Dictionary<string, MergedSupportingTokenAuthenticatorSpecification> _mergedSupportingTokenAuthenticatorsMap;
        private int _maxCachedNonces = defaultMaxCachedNonces;
        private TimeSpan _maxClockSkew = defaultMaxClockSkew;
        private NonceCache _nonceCache = null;
        private SecurityAlgorithmSuite _outgoingAlgorithmSuite = SecurityAlgorithmSuite.Default;
        private TimeSpan _replayWindow = defaultReplayWindow;
        private SecurityStandardsManager _standardsManager = SecurityStandardsManager.DefaultInstance;
        private SecurityTokenManager _securityTokenManager;
        private SecurityBindingElement _securityBindingElement;
        private string _requestReplyErrorPropertyName;
        private TimeSpan _timestampValidityDuration = defaultTimestampValidityDuration;

        // AuditLogLocation auditLogLocation;
        private readonly bool _suppressAuditFailure;
        private SecurityHeaderLayout _securityHeaderLayout;
        private bool _expectChannelBasicTokens;
        private bool _expectChannelSignedTokens;
        private bool _expectChannelEndorsingTokens;
        private Uri _listenUri;
        private Uri _privacyNoticeUri;
        private int _privacyNoticeVersion;
        private IMessageFilterTable<EndpointAddress> _endpointFilterTable;
        private BufferManager _streamBufferManager = null;

        protected SecurityProtocolFactory()
        {
            ChannelSupportingTokenAuthenticatorSpecification = new Collection<SupportingTokenAuthenticatorSpecification>();
            ScopedSupportingTokenAuthenticatorSpecification = new Dictionary<string, ICollection<SupportingTokenAuthenticatorSpecification>>();
            CommunicationObject = new WrapperSecurityCommunicationObject(this);
        }

        internal SecurityProtocolFactory(SecurityProtocolFactory factory) : this()
        {
            if (factory == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(factory));
            }

            _addTimestamp = factory._addTimestamp;
            _detectReplays = factory._detectReplays;
            _incomingAlgorithmSuite = factory._incomingAlgorithmSuite;
            _maxCachedNonces = factory._maxCachedNonces;
            _maxClockSkew = factory._maxClockSkew;
            _outgoingAlgorithmSuite = factory._outgoingAlgorithmSuite;
            _replayWindow = factory._replayWindow;
            ChannelSupportingTokenAuthenticatorSpecification = new Collection<SupportingTokenAuthenticatorSpecification>(new List<SupportingTokenAuthenticatorSpecification>(factory.ChannelSupportingTokenAuthenticatorSpecification));
            ScopedSupportingTokenAuthenticatorSpecification = new Dictionary<string, ICollection<SupportingTokenAuthenticatorSpecification>>(factory.ScopedSupportingTokenAuthenticatorSpecification);
            _standardsManager = factory._standardsManager;
            _timestampValidityDuration = factory._timestampValidityDuration;
            // this.auditLogLocation = factory.auditLogLocation;
            _suppressAuditFailure = factory._suppressAuditFailure;
            // this.serviceAuthorizationAuditLevel = factory.serviceAuthorizationAuditLevel;
            // this.messageAuthenticationAuditLevel = factory.messageAuthenticationAuditLevel;
            if (factory._securityBindingElement != null)
            {
                _securityBindingElement = (SecurityBindingElement)factory._securityBindingElement.Clone();
            }
            _securityTokenManager = factory._securityTokenManager;
            _privacyNoticeUri = factory._privacyNoticeUri;
            _privacyNoticeVersion = factory._privacyNoticeVersion;
            _endpointFilterTable = factory._endpointFilterTable;
            ExtendedProtectionPolicy = factory.ExtendedProtectionPolicy;
            _nonceCache = factory._nonceCache;
        }

        internal WrapperSecurityCommunicationObject CommunicationObject { get; }

        public BufferManager StreamBufferManager
        {
            get
            {
                if (_streamBufferManager == null)
                {
                    _streamBufferManager = BufferManager.CreateBufferManager(0, int.MaxValue);
                }

                return _streamBufferManager;
            }
            set
            {
                _streamBufferManager = value;
            }
        }

        public ExtendedProtectionPolicy ExtendedProtectionPolicy { get; set; }

        internal bool IsDuplexReply { get; set; }

        public bool AddTimestamp
        {
            get
            {
                return _addTimestamp;
            }
            set
            {
                ThrowIfImmutable();
                _addTimestamp = value;
            }
        }

        //public AuditLogLocation AuditLogLocation
        //{
        //    get
        //    {
        //        return this.auditLogLocation;
        //    }
        //    set
        //    {
        //        ThrowIfImmutable();
        //        AuditLogLocationHelper.Validate(value);
        //        this.auditLogLocation = value;
        //    }
        //}

        //public bool SuppressAuditFailure
        //{
        //    get
        //    {
        //        return this.suppressAuditFailure;
        //    }
        //    set
        //    {
        //        ThrowIfImmutable();
        //        this.suppressAuditFailure = value;
        //    }
        //}

        //public AuditLevel ServiceAuthorizationAuditLevel
        //{
        //    get
        //    {
        //        return this.serviceAuthorizationAuditLevel;
        //    }
        //    set
        //    {
        //        ThrowIfImmutable();
        //        AuditLevelHelper.Validate(value);
        //        this.serviceAuthorizationAuditLevel = value;
        //    }
        //}

        //public AuditLevel MessageAuthenticationAuditLevel
        //{
        //    get
        //    {
        //        return this.messageAuthenticationAuditLevel;
        //    }
        //    set
        //    {
        //        ThrowIfImmutable();
        //        AuditLevelHelper.Validate(value);
        //        this.messageAuthenticationAuditLevel = value;
        //    }
        //}

        public bool DetectReplays
        {
            get
            {
                return _detectReplays;
            }
            set
            {
                ThrowIfImmutable();
                _detectReplays = value;
            }
        }

        public Uri PrivacyNoticeUri
        {
            get
            {
                return _privacyNoticeUri;
            }
            set
            {
                ThrowIfImmutable();
                _privacyNoticeUri = value;
            }
        }

        public int PrivacyNoticeVersion
        {
            get
            {
                return _privacyNoticeVersion;
            }
            set
            {
                ThrowIfImmutable();
                _privacyNoticeVersion = value;
            }
        }

        internal IMessageFilterTable<EndpointAddress> EndpointFilterTable
        {
            get
            {
                return _endpointFilterTable;
            }
            set
            {
                ThrowIfImmutable();
                _endpointFilterTable = value;
            }
        }

        private static ReadOnlyCollection<SupportingTokenAuthenticatorSpecification> EmptyTokenAuthenticators
        {
            get
            {
                if (s_emptyTokenAuthenticators == null)
                {
                    s_emptyTokenAuthenticators = Array.AsReadOnly(Array.Empty<SupportingTokenAuthenticatorSpecification>());
                }
                return s_emptyTokenAuthenticators;
            }
        }

        internal NonValidatingSecurityTokenAuthenticator<DerivedKeySecurityToken> DerivedKeyTokenAuthenticator { get; }

        internal bool ExpectIncomingMessages { get; private set; }

        internal bool ExpectOutgoingMessages { get; private set; }

        internal bool ExpectKeyDerivation { get; set; }

        internal bool ExpectSupportingTokens { get; set; }

        public SecurityAlgorithmSuite IncomingAlgorithmSuite
        {
            get
            {
                return _incomingAlgorithmSuite;
            }
            set
            {
                ThrowIfImmutable();
                _incomingAlgorithmSuite = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
            }
        }

        public int MaxCachedNonces
        {
            get
            {
                return _maxCachedNonces;
            }
            set
            {
                ThrowIfImmutable();
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                _maxCachedNonces = value;
            }
        }

        public TimeSpan MaxClockSkew
        {
            get
            {
                return _maxClockSkew;
            }
            set
            {
                ThrowIfImmutable();
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                _maxClockSkew = value;
            }
        }

        public NonceCache NonceCache
        {
            get
            {
                return _nonceCache;
            }
            set
            {
                ThrowIfImmutable();
                _nonceCache = value;
            }
        }

        public SecurityAlgorithmSuite OutgoingAlgorithmSuite
        {
            get
            {
                return _outgoingAlgorithmSuite;
            }
            set
            {
                ThrowIfImmutable();
                _outgoingAlgorithmSuite = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
            }
        }

        public TimeSpan ReplayWindow
        {
            get
            {
                return _replayWindow;
            }
            set
            {
                ThrowIfImmutable();
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SRCommon.TimeSpanMustBeGreaterThanTimeSpanZero));
                }
                _replayWindow = value;
            }
        }

        public ICollection<SupportingTokenAuthenticatorSpecification> ChannelSupportingTokenAuthenticatorSpecification { get; private set; }

        public Dictionary<string, ICollection<SupportingTokenAuthenticatorSpecification>> ScopedSupportingTokenAuthenticatorSpecification { get; }

        public SecurityBindingElement SecurityBindingElement
        {
            get { return _securityBindingElement; }
            set
            {
                ThrowIfImmutable();
                if (value != null)
                {
                    value = (SecurityBindingElement)value.Clone();
                }
                _securityBindingElement = value;
            }
        }

        internal SecurityTokenManager SecurityTokenManager
        {
            get { return _securityTokenManager; }
            set
            {
                ThrowIfImmutable();
                _securityTokenManager = value;
            }
        }

        public virtual bool SupportsDuplex => false;

        public SecurityHeaderLayout SecurityHeaderLayout
        {
            get
            {
                return _securityHeaderLayout;
            }
            set
            {
                ThrowIfImmutable();
                _securityHeaderLayout = value;
            }
        }

        public virtual bool SupportsReplayDetection => true;

        public virtual bool SupportsRequestReply => true;

        internal SecurityStandardsManager StandardsManager
        {
            get
            {
                return _standardsManager;
            }
            set
            {
                ThrowIfImmutable();
                _standardsManager = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
            }
        }

        public TimeSpan TimestampValidityDuration
        {
            get
            {
                return _timestampValidityDuration;
            }
            set
            {
                ThrowIfImmutable();
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SRCommon.TimeSpanMustBeGreaterThanTimeSpanZero));
                }
                _timestampValidityDuration = value;
            }
        }

        public Uri ListenUri
        {
            get { return _listenUri; }
            set
            {
                ThrowIfImmutable();
                _listenUri = value;
            }
        }

        internal MessageSecurityVersion MessageSecurityVersion { get; private set; }

        public TimeSpan DefaultOpenTimeout => ServiceDefaults.OpenTimeout;

        public TimeSpan DefaultCloseTimeout => ServiceDefaults.CloseTimeout;

        public virtual void OnAbort()
        {
            foreach (SupportingTokenAuthenticatorSpecification spec in ChannelSupportingTokenAuthenticatorSpecification)
            {
                SecurityUtils.AbortTokenAuthenticatorIfRequired(spec.TokenAuthenticator);
            }
            foreach (string action in ScopedSupportingTokenAuthenticatorSpecification.Keys)
            {
                ICollection<SupportingTokenAuthenticatorSpecification> supportingAuthenticators = ScopedSupportingTokenAuthenticatorSpecification[action];
                foreach (SupportingTokenAuthenticatorSpecification spec in supportingAuthenticators)
                {
                    SecurityUtils.AbortTokenAuthenticatorIfRequired(spec.TokenAuthenticator);
                }
            }
        }

        public virtual void OnClose(TimeSpan timeout)
        {

        }

        public virtual object CreateListenerSecurityState()
        {
            return null;
        }

        internal SecurityProtocol CreateSecurityProtocol(EndpointAddress target, Uri via, bool isReturnLegSecurityRequired, TimeSpan timeout)
        {
            ThrowIfNotOpen();
            SecurityProtocol securityProtocol = OnCreateSecurityProtocol(target, via, timeout);
            if (securityProtocol == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.ProtocolFactoryCouldNotCreateProtocol));
            }
            return securityProtocol;
        }

        public virtual EndpointIdentity GetIdentityOfSelf()
        {
            return null;
        }

        public virtual T GetProperty<T>()
        {
            if (typeof(T) == typeof(Collection<ISecurityContextSecurityTokenCache>))
            {
                ThrowIfNotOpen();
                Collection<ISecurityContextSecurityTokenCache> result = new Collection<ISecurityContextSecurityTokenCache>();
                if (ChannelSupportingTokenAuthenticatorSpecification != null)
                {
                    foreach (SupportingTokenAuthenticatorSpecification spec in ChannelSupportingTokenAuthenticatorSpecification)
                    {
                        if (spec.TokenAuthenticator is ISecurityContextSecurityTokenCacheProvider cacheProvider)
                        {
                            result.Add(cacheProvider.TokenCache);
                        }
                    }
                }
                return (T)(object)(result);
            }
            else
            {
                return default;
            }
        }
        internal abstract SecurityProtocol OnCreateSecurityProtocol(EndpointAddress target, Uri via, TimeSpan timeout);

        private void VerifyTypeUniqueness(ICollection<SupportingTokenAuthenticatorSpecification> supportingTokenAuthenticators)
        {
            // its ok to go brute force here since we are dealing with a small number of authenticators
            foreach (SupportingTokenAuthenticatorSpecification spec in supportingTokenAuthenticators)
            {
                Type authenticatorType = spec.TokenAuthenticator.GetType();
                int numSkipped = 0;
                foreach (SupportingTokenAuthenticatorSpecification spec2 in supportingTokenAuthenticators)
                {
                    Type spec2AuthenticatorType = spec2.TokenAuthenticator.GetType();
                    if (ReferenceEquals(spec, spec2))
                    {
                        if (numSkipped > 0)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.MultipleSupportingAuthenticatorsOfSameType, spec.TokenParameters.GetType())));
                        }
                        ++numSkipped;
                        continue;
                    }
                    else if (authenticatorType.IsAssignableFrom(spec2AuthenticatorType) || spec2AuthenticatorType.IsAssignableFrom(authenticatorType))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.MultipleSupportingAuthenticatorsOfSameType, spec.TokenParameters.GetType())));
                    }
                }
            }
        }

        internal IList<SupportingTokenAuthenticatorSpecification> GetSupportingTokenAuthenticators(string action, out bool expectSignedTokens, out bool expectBasicTokens, out bool expectEndorsingTokens)
        {
            if (_mergedSupportingTokenAuthenticatorsMap != null && _mergedSupportingTokenAuthenticatorsMap.Count > 0)
            {
                if (action != null && _mergedSupportingTokenAuthenticatorsMap.ContainsKey(action))
                {
                    MergedSupportingTokenAuthenticatorSpecification mergedSpec = _mergedSupportingTokenAuthenticatorsMap[action];
                    expectSignedTokens = mergedSpec.ExpectSignedTokens;
                    expectBasicTokens = mergedSpec.ExpectBasicTokens;
                    expectEndorsingTokens = mergedSpec.ExpectEndorsingTokens;
                    return mergedSpec.SupportingTokenAuthenticators;
                }
                else if (_mergedSupportingTokenAuthenticatorsMap.ContainsKey(MessageHeaders.WildcardAction))
                {
                    MergedSupportingTokenAuthenticatorSpecification mergedSpec = _mergedSupportingTokenAuthenticatorsMap[MessageHeaders.WildcardAction];
                    expectSignedTokens = mergedSpec.ExpectSignedTokens;
                    expectBasicTokens = mergedSpec.ExpectBasicTokens;
                    expectEndorsingTokens = mergedSpec.ExpectEndorsingTokens;
                    return mergedSpec.SupportingTokenAuthenticators;
                }
            }
            expectSignedTokens = _expectChannelSignedTokens;
            expectBasicTokens = _expectChannelBasicTokens;
            expectEndorsingTokens = _expectChannelEndorsingTokens;
            // in case the channelSupportingTokenAuthenticators is empty return null so that its Count does not get accessed.
            return (ReferenceEquals(ChannelSupportingTokenAuthenticatorSpecification, EmptyTokenAuthenticators)) ? null : (IList<SupportingTokenAuthenticatorSpecification>)ChannelSupportingTokenAuthenticatorSpecification;
        }

        private async Task MergeSupportingTokenAuthenticatorsAsync(CancellationToken token)
        {
            if (ScopedSupportingTokenAuthenticatorSpecification.Count == 0)
            {
                _mergedSupportingTokenAuthenticatorsMap = null;
            }
            else
            {
                ExpectSupportingTokens = true;
                _mergedSupportingTokenAuthenticatorsMap = new Dictionary<string, MergedSupportingTokenAuthenticatorSpecification>();
                foreach (string action in ScopedSupportingTokenAuthenticatorSpecification.Keys)
                {
                    ICollection<SupportingTokenAuthenticatorSpecification> scopedAuthenticators = ScopedSupportingTokenAuthenticatorSpecification[action];
                    if (scopedAuthenticators == null || scopedAuthenticators.Count == 0)
                    {
                        continue;
                    }
                    Collection<SupportingTokenAuthenticatorSpecification> mergedAuthenticators = new Collection<SupportingTokenAuthenticatorSpecification>();
                    bool expectSignedTokens = _expectChannelSignedTokens;
                    bool expectBasicTokens = _expectChannelBasicTokens;
                    bool expectEndorsingTokens = _expectChannelEndorsingTokens;
                    foreach (SupportingTokenAuthenticatorSpecification spec in ChannelSupportingTokenAuthenticatorSpecification)
                    {
                        mergedAuthenticators.Add(spec);
                    }
                    foreach (SupportingTokenAuthenticatorSpecification spec in scopedAuthenticators)
                    {
                        await SecurityUtils.OpenTokenAuthenticatorIfRequiredAsync(spec.TokenAuthenticator, token);

                        mergedAuthenticators.Add(spec);
                        if (spec.SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.Endorsing ||
                            spec.SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.SignedEndorsing)
                        {
                            if (spec.TokenParameters.RequireDerivedKeys && !spec.TokenParameters.HasAsymmetricKey)
                            {
                                ExpectKeyDerivation = true;
                            }
                        }
                        SecurityTokenAttachmentMode mode = spec.SecurityTokenAttachmentMode;
                        if (mode == SecurityTokenAttachmentMode.SignedEncrypted
                            || mode == SecurityTokenAttachmentMode.Signed
                            || mode == SecurityTokenAttachmentMode.SignedEndorsing)
                        {
                            expectSignedTokens = true;
                            if (mode == SecurityTokenAttachmentMode.SignedEncrypted)
                            {
                                expectBasicTokens = true;
                            }
                        }
                        if (mode == SecurityTokenAttachmentMode.Endorsing || mode == SecurityTokenAttachmentMode.SignedEndorsing)
                        {
                            expectEndorsingTokens = true;
                        }
                    }
                    VerifyTypeUniqueness(mergedAuthenticators);
                    MergedSupportingTokenAuthenticatorSpecification mergedSpec = new MergedSupportingTokenAuthenticatorSpecification
                    {
                        SupportingTokenAuthenticators = mergedAuthenticators,
                        ExpectBasicTokens = expectBasicTokens,
                        ExpectEndorsingTokens = expectEndorsingTokens,
                        ExpectSignedTokens = expectSignedTokens
                    };
                    _mergedSupportingTokenAuthenticatorsMap.Add(action, mergedSpec);
                }
            }
        }

        protected RecipientServiceModelSecurityTokenRequirement CreateRecipientSecurityTokenRequirement()
        {
            RecipientServiceModelSecurityTokenRequirement requirement = new RecipientServiceModelSecurityTokenRequirement
            {
                SecurityBindingElement = _securityBindingElement,
                SecurityAlgorithmSuite = IncomingAlgorithmSuite,
                ListenUri = _listenUri,
                MessageSecurityVersion = MessageSecurityVersion.SecurityTokenVersion
            };
            // requirement.AuditLogLocation = this.auditLogLocation;
            // requirement.SuppressAuditFailure = this.suppressAuditFailure;
            // requirement.MessageAuthenticationAuditLevel = this.messageAuthenticationAuditLevel;
            requirement.Properties[ServiceModelSecurityTokenRequirement.ExtendedProtectionPolicy] = ExtendedProtectionPolicy;
            if (_endpointFilterTable != null)
            {
                requirement.Properties.Add(ServiceModelSecurityTokenRequirement.EndpointFilterTableProperty, _endpointFilterTable);
            }
            return requirement;
        }

        private RecipientServiceModelSecurityTokenRequirement CreateRecipientSecurityTokenRequirement(SecurityTokenParameters parameters, SecurityTokenAttachmentMode attachmentMode)
        {
            RecipientServiceModelSecurityTokenRequirement requirement = CreateRecipientSecurityTokenRequirement();
            parameters.InitializeSecurityTokenRequirement(requirement);
            requirement.KeyUsage = SecurityKeyUsage.Signature;
            requirement.Properties[ServiceModelSecurityTokenRequirement.MessageDirectionProperty] = MessageDirection.Input;
            requirement.Properties[ServiceModelSecurityTokenRequirement.SupportingTokenAttachmentModeProperty] = attachmentMode;
            requirement.Properties[ServiceModelSecurityTokenRequirement.ExtendedProtectionPolicy] = ExtendedProtectionPolicy;
            return requirement;
        }

        private void AddSupportingTokenAuthenticators(SupportingTokenParameters supportingTokenParameters, bool isOptional, IList<SupportingTokenAuthenticatorSpecification> authenticatorSpecList)
        {
            for (int i = 0; i < supportingTokenParameters.Endorsing.Count; ++i)
            {
                SecurityTokenRequirement requirement = CreateRecipientSecurityTokenRequirement(supportingTokenParameters.Endorsing[i], SecurityTokenAttachmentMode.Endorsing);
                try
                {
                    CoreWCF.IdentityModel.Selectors.SecurityTokenAuthenticator authenticator = SecurityTokenManager.CreateSecurityTokenAuthenticator(requirement, out SecurityTokenResolver resolver);
                    SupportingTokenAuthenticatorSpecification authenticatorSpec = new SupportingTokenAuthenticatorSpecification(authenticator, resolver, SecurityTokenAttachmentMode.Endorsing, supportingTokenParameters.Endorsing[i], isOptional);
                    authenticatorSpecList.Add(authenticatorSpec);
                }
                catch (Exception e)
                {
                    if (!isOptional || Fx.IsFatal(e))
                    {
                        throw;
                    }
                }
            }
            for (int i = 0; i < supportingTokenParameters.SignedEndorsing.Count; ++i)
            {
                SecurityTokenRequirement requirement = CreateRecipientSecurityTokenRequirement(supportingTokenParameters.SignedEndorsing[i], SecurityTokenAttachmentMode.SignedEndorsing);
                try
                {
                    CoreWCF.IdentityModel.Selectors.SecurityTokenAuthenticator authenticator = SecurityTokenManager.CreateSecurityTokenAuthenticator(requirement, out SecurityTokenResolver resolver);
                    SupportingTokenAuthenticatorSpecification authenticatorSpec = new SupportingTokenAuthenticatorSpecification(authenticator, resolver, SecurityTokenAttachmentMode.SignedEndorsing, supportingTokenParameters.SignedEndorsing[i], isOptional);
                    authenticatorSpecList.Add(authenticatorSpec);
                }
                catch (Exception e)
                {
                    if (!isOptional || Fx.IsFatal(e))
                    {
                        throw;
                    }
                }
            }
            for (int i = 0; i < supportingTokenParameters.SignedEncrypted.Count; ++i)
            {
                SecurityTokenRequirement requirement = CreateRecipientSecurityTokenRequirement(supportingTokenParameters.SignedEncrypted[i], SecurityTokenAttachmentMode.SignedEncrypted);
                try
                {
                    CoreWCF.IdentityModel.Selectors.SecurityTokenAuthenticator authenticator = SecurityTokenManager.CreateSecurityTokenAuthenticator(requirement, out SecurityTokenResolver resolver);
                    SupportingTokenAuthenticatorSpecification authenticatorSpec = new SupportingTokenAuthenticatorSpecification(authenticator, resolver, SecurityTokenAttachmentMode.SignedEncrypted, supportingTokenParameters.SignedEncrypted[i], isOptional);
                    authenticatorSpecList.Add(authenticatorSpec);
                }
                catch (Exception e)
                {
                    if (!isOptional || Fx.IsFatal(e))
                    {
                        throw;
                    }
                }
            }
            for (int i = 0; i < supportingTokenParameters.Signed.Count; ++i)
            {
                SecurityTokenRequirement requirement = CreateRecipientSecurityTokenRequirement(supportingTokenParameters.Signed[i], SecurityTokenAttachmentMode.Signed);
                try
                {
                    CoreWCF.IdentityModel.Selectors.SecurityTokenAuthenticator authenticator = SecurityTokenManager.CreateSecurityTokenAuthenticator(requirement, out SecurityTokenResolver resolver);
                    SupportingTokenAuthenticatorSpecification authenticatorSpec = new SupportingTokenAuthenticatorSpecification(authenticator, resolver, SecurityTokenAttachmentMode.Signed, supportingTokenParameters.Signed[i], isOptional);
                    authenticatorSpecList.Add(authenticatorSpec);
                }
                catch (Exception e)
                {
                    if (!isOptional || Fx.IsFatal(e))
                    {
                        throw;
                    }
                }
            }
        }

        public Task OpenAsync(TimeSpan timeout)
        {
            return CommunicationObject.OpenAsync();
        }

        public virtual async Task OnOpenAsync(CancellationToken token)
        {
            if (SecurityBindingElement == null)
            {
                OnPropertySettingsError(nameof(SecurityBindingElement), true);
            }
            if (SecurityTokenManager == null)
            {
                OnPropertySettingsError(nameof(SecurityTokenManager), true);
            }
            MessageSecurityVersion = _standardsManager.MessageSecurityVersion;
            ExpectOutgoingMessages = SupportsRequestReply;
            ExpectIncomingMessages = true;
            AddSupportingTokenAuthenticators(_securityBindingElement.EndpointSupportingTokenParameters, false, (IList<SupportingTokenAuthenticatorSpecification>)ChannelSupportingTokenAuthenticatorSpecification);
            AddSupportingTokenAuthenticators(_securityBindingElement.OptionalEndpointSupportingTokenParameters, true, (IList<SupportingTokenAuthenticatorSpecification>)ChannelSupportingTokenAuthenticatorSpecification);
            foreach (string action in _securityBindingElement.OperationSupportingTokenParameters.Keys)
            {
                Collection<SupportingTokenAuthenticatorSpecification> authenticatorSpecList = new Collection<SupportingTokenAuthenticatorSpecification>();
                AddSupportingTokenAuthenticators(_securityBindingElement.OperationSupportingTokenParameters[action], false, authenticatorSpecList);
                ScopedSupportingTokenAuthenticatorSpecification.Add(action, authenticatorSpecList);
            }

            foreach (string action in _securityBindingElement.OptionalOperationSupportingTokenParameters.Keys)
            {
                Collection<SupportingTokenAuthenticatorSpecification> authenticatorSpecList;
                if (ScopedSupportingTokenAuthenticatorSpecification.TryGetValue(action, out ICollection<SupportingTokenAuthenticatorSpecification> existingList))
                {
                    authenticatorSpecList = ((Collection<SupportingTokenAuthenticatorSpecification>)existingList);
                }
                else
                {
                    authenticatorSpecList = new Collection<SupportingTokenAuthenticatorSpecification>();
                    ScopedSupportingTokenAuthenticatorSpecification.Add(action, authenticatorSpecList);
                }
                AddSupportingTokenAuthenticators(_securityBindingElement.OptionalOperationSupportingTokenParameters[action], true, authenticatorSpecList);
            }

            // validate the token authenticator types and create a merged map if needed.
            if (!ChannelSupportingTokenAuthenticatorSpecification.IsReadOnly)
            {
                if (ChannelSupportingTokenAuthenticatorSpecification.Count == 0)
                {
                    ChannelSupportingTokenAuthenticatorSpecification = EmptyTokenAuthenticators;
                }
                else
                {
                    ExpectSupportingTokens = true;
                    foreach (SupportingTokenAuthenticatorSpecification tokenAuthenticatorSpec in ChannelSupportingTokenAuthenticatorSpecification)
                    {
                        await SecurityUtils.OpenTokenAuthenticatorIfRequiredAsync(tokenAuthenticatorSpec.TokenAuthenticator, token);
                        if (tokenAuthenticatorSpec.SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.Endorsing
                            || tokenAuthenticatorSpec.SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.SignedEndorsing)
                        {
                            if (tokenAuthenticatorSpec.TokenParameters.RequireDerivedKeys && !tokenAuthenticatorSpec.TokenParameters.HasAsymmetricKey)
                            {
                                ExpectKeyDerivation = true;
                            }
                        }

                        SecurityTokenAttachmentMode mode = tokenAuthenticatorSpec.SecurityTokenAttachmentMode;
                        if (mode == SecurityTokenAttachmentMode.SignedEncrypted
                            || mode == SecurityTokenAttachmentMode.Signed
                            || mode == SecurityTokenAttachmentMode.SignedEndorsing)
                        {
                            _expectChannelSignedTokens = true;
                            if (mode == SecurityTokenAttachmentMode.SignedEncrypted)
                            {
                                _expectChannelBasicTokens = true;
                            }
                        }

                        if (mode == SecurityTokenAttachmentMode.Endorsing || mode == SecurityTokenAttachmentMode.SignedEndorsing)
                        {
                            _expectChannelEndorsingTokens = true;
                        }
                    }
                    ChannelSupportingTokenAuthenticatorSpecification =
                        new ReadOnlyCollection<SupportingTokenAuthenticatorSpecification>((Collection<SupportingTokenAuthenticatorSpecification>)ChannelSupportingTokenAuthenticatorSpecification);
                }

                VerifyTypeUniqueness(ChannelSupportingTokenAuthenticatorSpecification);
                await MergeSupportingTokenAuthenticatorsAsync(token);
            }

            if (DetectReplays)
            {
                if (!SupportsReplayDetection)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(DetectReplays), SR.Format(SR.SecurityProtocolCannotDoReplayDetection, this));
                }
                if (MaxClockSkew == TimeSpan.MaxValue || ReplayWindow == TimeSpan.MaxValue)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.NoncesCachedInfinitely));
                }

                // If DetectReplays is true and T is null then use the default InMemoryNonceCache. 
                if (_nonceCache == null)
                {
                    // The nonce needs to be cached for replayWindow + 2*clockSkew to eliminate replays
                    _nonceCache = new InMemoryNonceCache(ReplayWindow + MaxClockSkew + MaxClockSkew, MaxCachedNonces);
                }
            }

            //this.derivedKeyTokenAuthenticator = new NonValidatingSecurityTokenAuthenticator<DerivedKeySecurityToken>();
        }

        public virtual async Task OnCloseAsync(CancellationToken token)
        {
            foreach (SupportingTokenAuthenticatorSpecification spec in ChannelSupportingTokenAuthenticatorSpecification)
            {
                await SecurityUtils.CloseTokenAuthenticatorIfRequiredAsync(spec.TokenAuthenticator, token);
            }
            foreach (string action in ScopedSupportingTokenAuthenticatorSpecification.Keys)
            {
                ICollection<SupportingTokenAuthenticatorSpecification> supportingAuthenticators = ScopedSupportingTokenAuthenticatorSpecification[action];
                foreach (SupportingTokenAuthenticatorSpecification spec in supportingAuthenticators)
                {
                    await SecurityUtils.CloseTokenAuthenticatorIfRequiredAsync(spec.TokenAuthenticator, token);
                }
            }
        }

        internal void OnPropertySettingsError(string propertyName, bool requiredForForwardDirection)
        {
            if (requiredForForwardDirection)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(
                    SR.Format(SR.PropertySettingErrorOnProtocolFactory, propertyName, this),
                    propertyName));
            }
            else if (_requestReplyErrorPropertyName == null)
            {
                _requestReplyErrorPropertyName = propertyName;
            }
        }

        internal void ThrowIfImmutable()
        {
            CommunicationObject.ThrowIfDisposedOrImmutable();
        }

        private void ThrowIfNotOpen()
        {
            CommunicationObject.ThrowIfNotOpened();
        }

        public void OnClosed() { }
        public void OnClosing() { }
        public void OnFaulted() { }
        public void OnOpened() { }
        public void OnOpening() { }

        public Task OpenAsync(CancellationToken token)
        {
            return CommunicationObject.OpenAsync(token);
        }

        public Task CloseAsync(bool aborted, CancellationToken token)
        {
            if (aborted)
            {
                CommunicationObject.Abort();
                return Task.CompletedTask;
            }
            else
            {
                return CommunicationObject.CloseAsync(token);
            }
        }
    }

    internal struct MergedSupportingTokenAuthenticatorSpecification
    {
        public Collection<SupportingTokenAuthenticatorSpecification> SupportingTokenAuthenticators;
        public bool ExpectSignedTokens;
        public bool ExpectEndorsingTokens;
        public bool ExpectBasicTokens;
    }
}
