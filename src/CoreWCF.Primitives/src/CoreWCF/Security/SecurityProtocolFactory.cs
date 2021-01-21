// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Authentication.ExtendedProtection;
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
        private static ReadOnlyCollection<SupportingTokenAuthenticatorSpecification> emptyTokenAuthenticators;
        private readonly bool actAsInitiator;
        private bool isDuplexReply;
        private bool addTimestamp = defaultAddTimestamp;
        private bool detectReplays = defaultDetectReplays;
        private bool expectIncomingMessages;
        private bool expectOutgoingMessages;
        private SecurityAlgorithmSuite incomingAlgorithmSuite = SecurityAlgorithmSuite.Default;
        // per receiver protocol factory lists
        private ICollection<SupportingTokenAuthenticatorSpecification> channelSupportingTokenAuthenticatorSpecification;
        private readonly Dictionary<string, ICollection<SupportingTokenAuthenticatorSpecification>> scopedSupportingTokenAuthenticatorSpecification;
        private Dictionary<string, MergedSupportingTokenAuthenticatorSpecification> mergedSupportingTokenAuthenticatorsMap;
        private int maxCachedNonces = defaultMaxCachedNonces;
        private TimeSpan maxClockSkew = defaultMaxClockSkew;
        private NonceCache nonceCache = null;
        private SecurityAlgorithmSuite outgoingAlgorithmSuite = SecurityAlgorithmSuite.Default;
        private TimeSpan replayWindow = defaultReplayWindow;
        private SecurityStandardsManager standardsManager = SecurityStandardsManager.DefaultInstance;
        private SecurityTokenManager securityTokenManager;
        private SecurityBindingElement securityBindingElement;
        private string requestReplyErrorPropertyName;
        private readonly NonValidatingSecurityTokenAuthenticator<DerivedKeySecurityToken> derivedKeyTokenAuthenticator;
        private TimeSpan timestampValidityDuration = defaultTimestampValidityDuration;

        // AuditLogLocation auditLogLocation;
        private readonly bool suppressAuditFailure;
        private SecurityHeaderLayout securityHeaderLayout;

        // AuditLevel serviceAuthorizationAuditLevel;
        // AuditLevel messageAuthenticationAuditLevel;
        private bool expectKeyDerivation;
        private bool expectChannelBasicTokens;
        private bool expectChannelSignedTokens;
        private bool expectChannelEndorsingTokens;
        private bool expectSupportingTokens;
        private Uri listenUri;
        private MessageSecurityVersion messageSecurityVersion;
        private readonly WrapperSecurityCommunicationObject communicationObject;
        private Uri privacyNoticeUri;
        private int privacyNoticeVersion;
        private IMessageFilterTable<EndpointAddress> endpointFilterTable;
        private ExtendedProtectionPolicy extendedProtectionPolicy;
        private BufferManager streamBufferManager = null;

        protected SecurityProtocolFactory()
        {
            channelSupportingTokenAuthenticatorSpecification = new Collection<SupportingTokenAuthenticatorSpecification>();
            scopedSupportingTokenAuthenticatorSpecification = new Dictionary<string, ICollection<SupportingTokenAuthenticatorSpecification>>();
            communicationObject = new WrapperSecurityCommunicationObject(this);
        }

        internal SecurityProtocolFactory(SecurityProtocolFactory factory) : this()
        {
            if (factory == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(factory));
            }

            actAsInitiator = factory.actAsInitiator;
            addTimestamp = factory.addTimestamp;
            detectReplays = factory.detectReplays;
            incomingAlgorithmSuite = factory.incomingAlgorithmSuite;
            maxCachedNonces = factory.maxCachedNonces;
            maxClockSkew = factory.maxClockSkew;
            outgoingAlgorithmSuite = factory.outgoingAlgorithmSuite;
            replayWindow = factory.replayWindow;
            channelSupportingTokenAuthenticatorSpecification = new Collection<SupportingTokenAuthenticatorSpecification>(new List<SupportingTokenAuthenticatorSpecification>(factory.channelSupportingTokenAuthenticatorSpecification));
            scopedSupportingTokenAuthenticatorSpecification = new Dictionary<string, ICollection<SupportingTokenAuthenticatorSpecification>>(factory.scopedSupportingTokenAuthenticatorSpecification);
            standardsManager = factory.standardsManager;
            timestampValidityDuration = factory.timestampValidityDuration;
            // this.auditLogLocation = factory.auditLogLocation;
            suppressAuditFailure = factory.suppressAuditFailure;
            // this.serviceAuthorizationAuditLevel = factory.serviceAuthorizationAuditLevel;
            // this.messageAuthenticationAuditLevel = factory.messageAuthenticationAuditLevel;
            if (factory.securityBindingElement != null)
            {
                securityBindingElement = (SecurityBindingElement)factory.securityBindingElement.Clone();
            }
            securityTokenManager = factory.securityTokenManager;
            privacyNoticeUri = factory.privacyNoticeUri;
            privacyNoticeVersion = factory.privacyNoticeVersion;
            endpointFilterTable = factory.endpointFilterTable;
            extendedProtectionPolicy = factory.extendedProtectionPolicy;
            nonceCache = factory.nonceCache;
        }

        internal WrapperSecurityCommunicationObject CommunicationObject => communicationObject;

        // The ActAsInitiator value is set automatically on Open and
        // remains unchanged thereafter.  ActAsInitiator is true for
        // the initiator of the message exchange, such as the sender
        // of a datagram, sender of a request and sender of either leg
        // of a duplex exchange.
        public bool ActAsInitiator => actAsInitiator;

        public BufferManager StreamBufferManager
        {
            get
            {
                if (streamBufferManager == null)
                {
                    streamBufferManager = BufferManager.CreateBufferManager(0, int.MaxValue);
                }

                return streamBufferManager;
            }
            set
            {
                streamBufferManager = value;
            }
        }

        public ExtendedProtectionPolicy ExtendedProtectionPolicy
        {
            get { return extendedProtectionPolicy; }
            set { extendedProtectionPolicy = value; }
        }

        internal bool IsDuplexReply
        {
            get
            {
                return isDuplexReply;
            }
            set
            {
                isDuplexReply = value;
            }
        }

        public bool AddTimestamp
        {
            get
            {
                return addTimestamp;
            }
            set
            {
                ThrowIfImmutable();
                addTimestamp = value;
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
                return detectReplays;
            }
            set
            {
                ThrowIfImmutable();
                detectReplays = value;
            }
        }

        public Uri PrivacyNoticeUri
        {
            get
            {
                return privacyNoticeUri;
            }
            set
            {
                ThrowIfImmutable();
                privacyNoticeUri = value;
            }
        }

        public int PrivacyNoticeVersion
        {
            get
            {
                return privacyNoticeVersion;
            }
            set
            {
                ThrowIfImmutable();
                privacyNoticeVersion = value;
            }
        }

        internal IMessageFilterTable<EndpointAddress> EndpointFilterTable
        {
            get
            {
                return endpointFilterTable;
            }
            set
            {
                ThrowIfImmutable();
                endpointFilterTable = value;
            }
        }

        private static ReadOnlyCollection<SupportingTokenAuthenticatorSpecification> EmptyTokenAuthenticators
        {
            get
            {
                if (emptyTokenAuthenticators == null)
                {
                    emptyTokenAuthenticators = Array.AsReadOnly(new SupportingTokenAuthenticatorSpecification[0]);
                }
                return emptyTokenAuthenticators;
            }
        }

        internal NonValidatingSecurityTokenAuthenticator<DerivedKeySecurityToken> DerivedKeyTokenAuthenticator => derivedKeyTokenAuthenticator;

        internal bool ExpectIncomingMessages => expectIncomingMessages;

        internal bool ExpectOutgoingMessages => expectOutgoingMessages;

        internal bool ExpectKeyDerivation
        {
            get { return expectKeyDerivation; }
            set { expectKeyDerivation = value; }
        }

        internal bool ExpectSupportingTokens
        {
            get { return expectSupportingTokens; }
            set { expectSupportingTokens = value; }
        }

        public SecurityAlgorithmSuite IncomingAlgorithmSuite
        {
            get
            {
                return incomingAlgorithmSuite;
            }
            set
            {
                ThrowIfImmutable();
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
                }
                incomingAlgorithmSuite = value;
            }
        }

        public int MaxCachedNonces
        {
            get
            {
                return maxCachedNonces;
            }
            set
            {
                ThrowIfImmutable();
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                maxCachedNonces = value;
            }
        }

        public TimeSpan MaxClockSkew
        {
            get
            {
                return maxClockSkew;
            }
            set
            {
                ThrowIfImmutable();
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                maxClockSkew = value;
            }
        }

        public NonceCache NonceCache
        {
            get
            {
                return nonceCache;
            }
            set
            {
                ThrowIfImmutable();
                nonceCache = value;
            }
        }

        public SecurityAlgorithmSuite OutgoingAlgorithmSuite
        {
            get
            {
                return outgoingAlgorithmSuite;
            }
            set
            {
                ThrowIfImmutable();
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
                }
                outgoingAlgorithmSuite = value;
            }
        }

        public TimeSpan ReplayWindow
        {
            get
            {
                return replayWindow;
            }
            set
            {
                ThrowIfImmutable();
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }
                replayWindow = value;
            }
        }

        public ICollection<SupportingTokenAuthenticatorSpecification> ChannelSupportingTokenAuthenticatorSpecification => channelSupportingTokenAuthenticatorSpecification;

        public Dictionary<string, ICollection<SupportingTokenAuthenticatorSpecification>> ScopedSupportingTokenAuthenticatorSpecification => scopedSupportingTokenAuthenticatorSpecification;

        public SecurityBindingElement SecurityBindingElement
        {
            get { return securityBindingElement; }
            set
            {
                ThrowIfImmutable();
                if (value != null)
                {
                    value = (SecurityBindingElement)value.Clone();
                }
                securityBindingElement = value;
            }
        }

        internal SecurityTokenManager SecurityTokenManager
        {
            get { return securityTokenManager; }
            set
            {
                ThrowIfImmutable();
                securityTokenManager = value;
            }
        }

        public virtual bool SupportsDuplex => false;

        public SecurityHeaderLayout SecurityHeaderLayout
        {
            get
            {
                return securityHeaderLayout;
            }
            set
            {
                ThrowIfImmutable();
                securityHeaderLayout = value;
            }
        }

        public virtual bool SupportsReplayDetection => true;

        public virtual bool SupportsRequestReply => true;

        internal SecurityStandardsManager StandardsManager
        {
            get
            {
                return standardsManager;
            }
            set
            {
                ThrowIfImmutable();
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
                }
                standardsManager = value;
            }
        }

        public TimeSpan TimestampValidityDuration
        {
            get
            {
                return timestampValidityDuration;
            }
            set
            {
                ThrowIfImmutable();
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }
                timestampValidityDuration = value;
            }
        }

        public Uri ListenUri
        {
            get { return listenUri; }
            set
            {
                ThrowIfImmutable();
                listenUri = value;
            }
        }

        internal MessageSecurityVersion MessageSecurityVersion => messageSecurityVersion;

        public TimeSpan DefaultOpenTimeout => ServiceDefaults.OpenTimeout;

        public TimeSpan DefaultCloseTimeout => ServiceDefaults.CloseTimeout;

        public virtual void OnAbort()
        {
            if (!actAsInitiator)
            {
                foreach (SupportingTokenAuthenticatorSpecification spec in channelSupportingTokenAuthenticatorSpecification)
                {
                    SecurityUtils.AbortTokenAuthenticatorIfRequired(spec.TokenAuthenticator);
                }
                foreach (string action in scopedSupportingTokenAuthenticatorSpecification.Keys)
                {
                    ICollection<SupportingTokenAuthenticatorSpecification> supportingAuthenticators = scopedSupportingTokenAuthenticatorSpecification[action];
                    foreach (SupportingTokenAuthenticatorSpecification spec in supportingAuthenticators)
                    {
                        SecurityUtils.AbortTokenAuthenticatorIfRequired(spec.TokenAuthenticator);
                    }
                }
            }
        }

        public virtual void OnClose(TimeSpan timeout)
        {
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            if (!actAsInitiator)
            {
                foreach (SupportingTokenAuthenticatorSpecification spec in channelSupportingTokenAuthenticatorSpecification)
                {
                    SecurityUtils.CloseTokenAuthenticatorIfRequiredAsync(spec.TokenAuthenticator, timeoutHelper.GetCancellationToken());
                }
                foreach (string action in scopedSupportingTokenAuthenticatorSpecification.Keys)
                {
                    ICollection<SupportingTokenAuthenticatorSpecification> supportingAuthenticators = scopedSupportingTokenAuthenticatorSpecification[action];
                    foreach (SupportingTokenAuthenticatorSpecification spec in supportingAuthenticators)
                    {
                        SecurityUtils.CloseTokenAuthenticatorIfRequiredAsync(spec.TokenAuthenticator, timeoutHelper.GetCancellationToken());
                    }
                }
            }
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
                if (channelSupportingTokenAuthenticatorSpecification != null)
                {
                    foreach (SupportingTokenAuthenticatorSpecification spec in channelSupportingTokenAuthenticatorSpecification)
                    {
                        if (spec.TokenAuthenticator is ISecurityContextSecurityTokenCacheProvider)
                        {
                            result.Add(((ISecurityContextSecurityTokenCacheProvider)spec.TokenAuthenticator).TokenCache);
                        }
                    }
                }
                return (T)(object)(result);
            }
            else
            {
                return default(T);
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
                    if (object.ReferenceEquals(spec, spec2))
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
            if (mergedSupportingTokenAuthenticatorsMap != null && mergedSupportingTokenAuthenticatorsMap.Count > 0)
            {
                if (action != null && mergedSupportingTokenAuthenticatorsMap.ContainsKey(action))
                {
                    MergedSupportingTokenAuthenticatorSpecification mergedSpec = mergedSupportingTokenAuthenticatorsMap[action];
                    expectSignedTokens = mergedSpec.ExpectSignedTokens;
                    expectBasicTokens = mergedSpec.ExpectBasicTokens;
                    expectEndorsingTokens = mergedSpec.ExpectEndorsingTokens;
                    return mergedSpec.SupportingTokenAuthenticators;
                }
                else if (mergedSupportingTokenAuthenticatorsMap.ContainsKey(MessageHeaders.WildcardAction))
                {
                    MergedSupportingTokenAuthenticatorSpecification mergedSpec = mergedSupportingTokenAuthenticatorsMap[MessageHeaders.WildcardAction];
                    expectSignedTokens = mergedSpec.ExpectSignedTokens;
                    expectBasicTokens = mergedSpec.ExpectBasicTokens;
                    expectEndorsingTokens = mergedSpec.ExpectEndorsingTokens;
                    return mergedSpec.SupportingTokenAuthenticators;
                }
            }
            expectSignedTokens = expectChannelSignedTokens;
            expectBasicTokens = expectChannelBasicTokens;
            expectEndorsingTokens = expectChannelEndorsingTokens;
            // in case the channelSupportingTokenAuthenticators is empty return null so that its Count does not get accessed.
            return (Object.ReferenceEquals(channelSupportingTokenAuthenticatorSpecification, EmptyTokenAuthenticators)) ? null : (IList<SupportingTokenAuthenticatorSpecification>)channelSupportingTokenAuthenticatorSpecification;
        }

        private void MergeSupportingTokenAuthenticators(TimeSpan timeout)
        {
            if (scopedSupportingTokenAuthenticatorSpecification.Count == 0)
            {
                mergedSupportingTokenAuthenticatorsMap = null;
            }
            else
            {
                TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
                expectSupportingTokens = true;
                mergedSupportingTokenAuthenticatorsMap = new Dictionary<string, MergedSupportingTokenAuthenticatorSpecification>();
                foreach (string action in scopedSupportingTokenAuthenticatorSpecification.Keys)
                {
                    ICollection<SupportingTokenAuthenticatorSpecification> scopedAuthenticators = scopedSupportingTokenAuthenticatorSpecification[action];
                    if (scopedAuthenticators == null || scopedAuthenticators.Count == 0)
                    {
                        continue;
                    }
                    Collection<SupportingTokenAuthenticatorSpecification> mergedAuthenticators = new Collection<SupportingTokenAuthenticatorSpecification>();
                    bool expectSignedTokens = expectChannelSignedTokens;
                    bool expectBasicTokens = expectChannelBasicTokens;
                    bool expectEndorsingTokens = expectChannelEndorsingTokens;
                    foreach (SupportingTokenAuthenticatorSpecification spec in channelSupportingTokenAuthenticatorSpecification)
                    {
                        mergedAuthenticators.Add(spec);
                    }
                    foreach (SupportingTokenAuthenticatorSpecification spec in scopedAuthenticators)
                    {
                        SecurityUtils.OpenTokenAuthenticatorIfRequiredAsync(spec.TokenAuthenticator, timeoutHelper.GetCancellationToken());

                        mergedAuthenticators.Add(spec);
                        if (spec.SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.Endorsing ||
                            spec.SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.SignedEndorsing)
                        {
                            if (spec.TokenParameters.RequireDerivedKeys && !spec.TokenParameters.HasAsymmetricKey)
                            {
                                expectKeyDerivation = true;
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
                    MergedSupportingTokenAuthenticatorSpecification mergedSpec = new MergedSupportingTokenAuthenticatorSpecification();
                    mergedSpec.SupportingTokenAuthenticators = mergedAuthenticators;
                    mergedSpec.ExpectBasicTokens = expectBasicTokens;
                    mergedSpec.ExpectEndorsingTokens = expectEndorsingTokens;
                    mergedSpec.ExpectSignedTokens = expectSignedTokens;
                    mergedSupportingTokenAuthenticatorsMap.Add(action, mergedSpec);
                }
            }
        }

        protected RecipientServiceModelSecurityTokenRequirement CreateRecipientSecurityTokenRequirement()
        {
            RecipientServiceModelSecurityTokenRequirement requirement = new RecipientServiceModelSecurityTokenRequirement();
            requirement.SecurityBindingElement = securityBindingElement;
            requirement.SecurityAlgorithmSuite = IncomingAlgorithmSuite;
            requirement.ListenUri = listenUri;
            requirement.MessageSecurityVersion = MessageSecurityVersion.SecurityTokenVersion;
            // requirement.AuditLogLocation = this.auditLogLocation;
            // requirement.SuppressAuditFailure = this.suppressAuditFailure;
            // requirement.MessageAuthenticationAuditLevel = this.messageAuthenticationAuditLevel;
            requirement.Properties[ServiceModelSecurityTokenRequirement.ExtendedProtectionPolicy] = extendedProtectionPolicy;
            if (endpointFilterTable != null)
            {
                requirement.Properties.Add(ServiceModelSecurityTokenRequirement.EndpointFilterTableProperty, endpointFilterTable);
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
            requirement.Properties[ServiceModelSecurityTokenRequirement.ExtendedProtectionPolicy] = extendedProtectionPolicy;
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
            return communicationObject.OpenAsync();
        }

        public virtual Task OnOpenAsync(TimeSpan timeout)
        {
            if (SecurityBindingElement == null)
            {
                OnPropertySettingsError("SecurityBindingElement", true);
            }
            if (SecurityTokenManager == null)
            {
                OnPropertySettingsError("SecurityTokenManager", true);
            }
            messageSecurityVersion = standardsManager.MessageSecurityVersion;
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            expectOutgoingMessages = ActAsInitiator || SupportsRequestReply;
            expectIncomingMessages = !ActAsInitiator || SupportsRequestReply;
            if (!actAsInitiator)
            {
                AddSupportingTokenAuthenticators(securityBindingElement.EndpointSupportingTokenParameters, false, (IList<SupportingTokenAuthenticatorSpecification>)channelSupportingTokenAuthenticatorSpecification);
                AddSupportingTokenAuthenticators(securityBindingElement.OptionalEndpointSupportingTokenParameters, true, (IList<SupportingTokenAuthenticatorSpecification>)channelSupportingTokenAuthenticatorSpecification);
                foreach (string action in securityBindingElement.OperationSupportingTokenParameters.Keys)
                {
                    Collection<SupportingTokenAuthenticatorSpecification> authenticatorSpecList = new Collection<SupportingTokenAuthenticatorSpecification>();
                    AddSupportingTokenAuthenticators(securityBindingElement.OperationSupportingTokenParameters[action], false, authenticatorSpecList);
                    scopedSupportingTokenAuthenticatorSpecification.Add(action, authenticatorSpecList);
                }
                foreach (string action in securityBindingElement.OptionalOperationSupportingTokenParameters.Keys)
                {
                    Collection<SupportingTokenAuthenticatorSpecification> authenticatorSpecList;
                    if (scopedSupportingTokenAuthenticatorSpecification.TryGetValue(action, out ICollection<SupportingTokenAuthenticatorSpecification> existingList))
                    {
                        authenticatorSpecList = ((Collection<SupportingTokenAuthenticatorSpecification>)existingList);
                    }
                    else
                    {
                        authenticatorSpecList = new Collection<SupportingTokenAuthenticatorSpecification>();
                        scopedSupportingTokenAuthenticatorSpecification.Add(action, authenticatorSpecList);
                    }
                    AddSupportingTokenAuthenticators(securityBindingElement.OptionalOperationSupportingTokenParameters[action], true, authenticatorSpecList);
                }
                // validate the token authenticator types and create a merged map if needed.
                if (!channelSupportingTokenAuthenticatorSpecification.IsReadOnly)
                {
                    if (channelSupportingTokenAuthenticatorSpecification.Count == 0)
                    {
                        channelSupportingTokenAuthenticatorSpecification = EmptyTokenAuthenticators;
                    }
                    else
                    {
                        expectSupportingTokens = true;
                        foreach (SupportingTokenAuthenticatorSpecification tokenAuthenticatorSpec in channelSupportingTokenAuthenticatorSpecification)
                        {
                            SecurityUtils.OpenTokenAuthenticatorIfRequiredAsync(tokenAuthenticatorSpec.TokenAuthenticator, timeoutHelper.GetCancellationToken());
                            if (tokenAuthenticatorSpec.SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.Endorsing
                                || tokenAuthenticatorSpec.SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.SignedEndorsing)
                            {
                                if (tokenAuthenticatorSpec.TokenParameters.RequireDerivedKeys && !tokenAuthenticatorSpec.TokenParameters.HasAsymmetricKey)
                                {
                                    expectKeyDerivation = true;
                                }
                            }
                            SecurityTokenAttachmentMode mode = tokenAuthenticatorSpec.SecurityTokenAttachmentMode;
                            if (mode == SecurityTokenAttachmentMode.SignedEncrypted
                                || mode == SecurityTokenAttachmentMode.Signed
                                || mode == SecurityTokenAttachmentMode.SignedEndorsing)
                            {
                                expectChannelSignedTokens = true;
                                if (mode == SecurityTokenAttachmentMode.SignedEncrypted)
                                {
                                    expectChannelBasicTokens = true;
                                }
                            }
                            if (mode == SecurityTokenAttachmentMode.Endorsing || mode == SecurityTokenAttachmentMode.SignedEndorsing)
                            {
                                expectChannelEndorsingTokens = true;
                            }
                        }
                        channelSupportingTokenAuthenticatorSpecification =
                            new ReadOnlyCollection<SupportingTokenAuthenticatorSpecification>((Collection<SupportingTokenAuthenticatorSpecification>)channelSupportingTokenAuthenticatorSpecification);
                    }
                }
                VerifyTypeUniqueness(channelSupportingTokenAuthenticatorSpecification);
                MergeSupportingTokenAuthenticators(timeoutHelper.RemainingTime());
            }

            if (DetectReplays)
            {
                if (!SupportsReplayDetection)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("DetectReplays", SR.Format(SR.SecurityProtocolCannotDoReplayDetection, this));
                }
                if (MaxClockSkew == TimeSpan.MaxValue || ReplayWindow == TimeSpan.MaxValue)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.NoncesCachedInfinitely));
                }

                // If DetectReplays is true and nonceCache is null then use the default InMemoryNonceCache. 
                if (nonceCache == null)
                {
                    //TODO below (InMemoryNonceCache) is coming along with WindowsAuth, so uncomment
                    // The nonce needs to be cached for replayWindow + 2*clockSkew to eliminate replays
                    // this.nonceCache = new InMemoryNonceCache(this.ReplayWindow + this.MaxClockSkew + this.MaxClockSkew, this.MaxCachedNonces);
                }
            }

            //this.derivedKeyTokenAuthenticator = new NonValidatingSecurityTokenAuthenticator<DerivedKeySecurityToken>();
            return Task.CompletedTask;
        }

        public virtual Task OnCloseAsync(TimeSpan timeout)
        {
            OnClose(timeout);
            return Task.CompletedTask;
        }


        internal void Open(string propertyName, bool requiredForForwardDirection, SecurityTokenAuthenticator authenticator, TimeSpan timeout)
        {
            if (authenticator != null)
            {
                TimeoutHelper helper = new TimeoutHelper(timeout);
                SecurityUtils.OpenTokenAuthenticatorIfRequiredAsync(authenticator, helper.GetCancellationToken());
            }
            else
            {
                OnPropertySettingsError(propertyName, requiredForForwardDirection);
            }
        }

        internal void Open(string propertyName, bool requiredForForwardDirection, SecurityTokenProvider provider, TimeSpan timeout)
        {
            if (provider != null)
            {
                SecurityUtils.OpenTokenProviderIfRequiredAsync(provider, new TimeoutHelper(timeout).GetCancellationToken());
            }
            else
            {
                OnPropertySettingsError(propertyName, requiredForForwardDirection);
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
            else if (requestReplyErrorPropertyName == null)
            {
                requestReplyErrorPropertyName = propertyName;
            }
        }

        private void ThrowIfReturnDirectionSecurityNotSupported()
        {
            if (requestReplyErrorPropertyName != null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(
                    SR.Format(SR.PropertySettingErrorOnProtocolFactory, requestReplyErrorPropertyName, this),
                    requestReplyErrorPropertyName));
            }
        }

        internal void ThrowIfImmutable()
        {
            communicationObject.ThrowIfDisposedOrImmutable();
        }

        private void ThrowIfNotOpen()
        {
            communicationObject.ThrowIfNotOpened();
        }

        public void OnClosed()
        {
            throw new NotImplementedException();
        }

        public void OnClosing()
        {
            throw new NotImplementedException();
        }

        public void OnFaulted()
        {
            throw new NotImplementedException();
        }

        public void OnOpened()
        {
            throw new NotImplementedException();
        }

        public void OnOpening()
        {
            throw new NotImplementedException();
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
