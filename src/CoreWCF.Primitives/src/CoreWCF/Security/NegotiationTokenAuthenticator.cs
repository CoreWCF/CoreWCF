// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal abstract class NegotiationTokenAuthenticator<T> : CommunicationObjectSecurityTokenAuthenticator, IIssuanceSecurityTokenAuthenticator, ISecurityContextSecurityTokenCacheProvider
        where T : NegotiationTokenAuthenticatorState
    {
        internal const string defaultServerMaxNegotiationLifetimeString = "00:01:00";
        internal const string defaultServerIssuedTokenLifetimeString = "10:00:00";
        internal const string defaultServerIssuedTransitionTokenLifetimeString = "00:15:00";
        internal const int defaultServerMaxActiveNegotiations = 128;
        internal static readonly TimeSpan defaultServerMaxNegotiationLifetime = TimeSpan.Parse(defaultServerMaxNegotiationLifetimeString, CultureInfo.InvariantCulture);
        internal static readonly TimeSpan defaultServerIssuedTokenLifetime = TimeSpan.Parse(defaultServerIssuedTokenLifetimeString, CultureInfo.InvariantCulture);
        internal static readonly TimeSpan defaultServerIssuedTransitionTokenLifetime = TimeSpan.Parse(defaultServerIssuedTransitionTokenLifetimeString, CultureInfo.InvariantCulture);
        internal const int defaultServerMaxCachedTokens = 1000;
        internal const bool defaultServerMaintainState = true;
        internal static readonly SecurityStandardsManager defaultStandardsManager = SecurityStandardsManager.DefaultInstance;
        internal static readonly SecurityStateEncoder defaultSecurityStateEncoder = new DataProtectionSecurityStateEncoder();
        private NegotiationTokenAuthenticatorStateCache<T> stateCache;
        private RenewedSecurityTokenHandler renewedSecurityTokenHandler;
        private NegotiationHost negotiationHost;
        private bool encryptStateInServiceToken;
        private TimeSpan serviceTokenLifetime;
        private int maximumCachedNegotiationState;
        private TimeSpan negotiationTimeout;
        private bool isClientAnonymous;
        private SecurityStandardsManager standardsManager;
        private SecurityAlgorithmSuite securityAlgorithmSuite;
        private SecurityTokenParameters issuedSecurityTokenParameters;
        private ISecurityContextSecurityTokenCache issuedTokenCache;
        private BindingContext issuerBindingContext;
        private Uri listenUri;
        private string sctUri;

        // AuditLogLocation auditLogLocation;
        private bool suppressAuditFailure;

        // AuditLevel messageAuthenticationAuditLevel;
        private SecurityStateEncoder securityStateEncoder;
        private SecurityContextCookieSerializer cookieSerializer;
        private IMessageFilterTable<EndpointAddress> endpointFilterTable;
        private IssuedSecurityTokenHandler issuedSecurityTokenHandler;
        private int maxMessageSize;
        private IList<Type> knownTypes;
        private int maximumConcurrentNegotiations;
        private List<IChannel> activeNegotiationChannels1;
        private List<IChannel> activeNegotiationChannels2;
        private IOThreadTimer idlingNegotiationSessionTimer;
        private bool isTimerCancelled;

        protected NegotiationTokenAuthenticator() : base() => InitializeDefaults();

        public IssuedSecurityTokenHandler IssuedSecurityTokenHandler
        {
            get => issuedSecurityTokenHandler;
            set => issuedSecurityTokenHandler = value;

        }

        public RenewedSecurityTokenHandler RenewedSecurityTokenHandler
        {
            get => renewedSecurityTokenHandler;
            set => renewedSecurityTokenHandler = value;
        }

        // settings
        public bool EncryptStateInServiceToken
        {
            get => encryptStateInServiceToken;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                encryptStateInServiceToken = value;
            }
        }

        public TimeSpan ServiceTokenLifetime
        {
            get => serviceTokenLifetime;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.Format(SR.SFxTimeoutOutOfRangeTooBig)));
                }
                serviceTokenLifetime = value;
            }
        }

        public int MaximumCachedNegotiationState
        {
            get => maximumCachedNegotiationState;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.Format(SR.ValueMustBeNonNegative)));
                }
                maximumCachedNegotiationState = value;
            }
        }

        public int MaximumConcurrentNegotiations
        {
            get => maximumConcurrentNegotiations;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.Format(SR.ValueMustBeNonNegative)));
                }
                maximumConcurrentNegotiations = value;
            }
        }

        public TimeSpan NegotiationTimeout
        {
            get => negotiationTimeout;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.Format(SR.SFxTimeoutOutOfRangeTooBig)));
                }
                negotiationTimeout = value;
            }
        }

        public bool IsClientAnonymous
        {
            get => isClientAnonymous;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                isClientAnonymous = value;
            }
        }

        public SecurityAlgorithmSuite SecurityAlgorithmSuite
        {
            get => securityAlgorithmSuite;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                securityAlgorithmSuite = value;
            }
        }

        public IMessageFilterTable<EndpointAddress> EndpointFilterTable
        {
            get => endpointFilterTable;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                endpointFilterTable = value;
            }
        }

        ISecurityContextSecurityTokenCache ISecurityContextSecurityTokenCacheProvider.TokenCache => IssuedTokenCache;

        public virtual XmlDictionaryString RequestSecurityTokenAction => StandardsManager.TrustDriver.RequestSecurityTokenAction;

        public virtual XmlDictionaryString RequestSecurityTokenResponseAction => StandardsManager.TrustDriver.RequestSecurityTokenResponseAction;

        public virtual XmlDictionaryString RequestSecurityTokenResponseFinalAction => StandardsManager.TrustDriver.RequestSecurityTokenResponseFinalAction;

        public SecurityStandardsManager StandardsManager
        {
            get => standardsManager;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                standardsManager = (value != null ? value : SecurityStandardsManager.DefaultInstance);
            }
        }

        public SecurityTokenParameters IssuedSecurityTokenParameters
        {
            get => issuedSecurityTokenParameters;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                issuedSecurityTokenParameters = value;
            }
        }

        public ISecurityContextSecurityTokenCache IssuedTokenCache
        {
            get => issuedTokenCache;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                issuedTokenCache = value;
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
        //        this.CommunicationObject.ThrowIfDisposedOrImmutable();
        //        this.auditLogLocation = value;
        //    }
        //}

        public bool SuppressAuditFailure
        {
            get => suppressAuditFailure;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                suppressAuditFailure = value;
            }
        }

        //public AuditLevel MessageAuthenticationAuditLevel
        //{
        //    get
        //    {
        //        return this.messageAuthenticationAuditLevel;
        //    }
        //    set
        //    {
        //        this.CommunicationObject.ThrowIfDisposedOrImmutable();
        //        this.messageAuthenticationAuditLevel = value;
        //    }
        //}

        public BindingContext IssuerBindingContext
        {
            get => issuerBindingContext;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }
                issuerBindingContext = value.Clone();
            }
        }

        public Uri ListenUri
        {
            get => listenUri;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                listenUri = value;
            }
        }

        public SecurityStateEncoder SecurityStateEncoder
        {
            get => securityStateEncoder;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                securityStateEncoder = value;
            }
        }

        public IList<Type> KnownTypes
        {
            get => knownTypes;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value != null)
                {
                    knownTypes = new Collection<Type>(value);
                }
                else
                {
                    knownTypes = null;
                }
            }
        }

        public int MaxMessageSize
        {
            get => maxMessageSize;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                maxMessageSize = value;
            }
        }

        protected string SecurityContextTokenUri =>
                // this.CommunicationObject.ThrowIfNotOpened();
                sctUri;

        private object ThisLock => CommunicationObject;

        // helpers
        protected SecurityContextSecurityToken IssueSecurityContextToken(UniqueId contextId, string id, byte[] key,
            DateTime tokenEffectiveTime, DateTime tokenExpirationTime,
            ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies, bool isCookieMode) => IssueSecurityContextToken(contextId, id, key, tokenEffectiveTime, tokenExpirationTime, null,
                tokenEffectiveTime, tokenExpirationTime, authorizationPolicies, isCookieMode);

        protected SecurityContextSecurityToken IssueSecurityContextToken(UniqueId contextId, string id, byte[] key,
            DateTime tokenEffectiveTime, DateTime tokenExpirationTime, UniqueId keyGeneration, DateTime keyEffectiveTime,
            DateTime keyExpirationTime, ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies, bool isCookieMode)
        {
            //  this.CommunicationObject.ThrowIfClosedOrNotOpen();
            if (securityStateEncoder == null && isCookieMode)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SctCookieNotSupported)));
            }
            byte[] cookieBlob = (isCookieMode) ? cookieSerializer.CreateCookieFromSecurityContext(contextId, id, key, tokenEffectiveTime, tokenExpirationTime, keyGeneration,
                                keyEffectiveTime, keyExpirationTime, authorizationPolicies) : null;

            SecurityContextSecurityToken issuedToken = new SecurityContextSecurityToken(contextId, id, key, tokenEffectiveTime, tokenExpirationTime,
                authorizationPolicies, isCookieMode, cookieBlob, keyGeneration, keyEffectiveTime, keyExpirationTime);
            return issuedToken;
        }

        private void InitializeDefaults()
        {
            encryptStateInServiceToken = !defaultServerMaintainState;
            serviceTokenLifetime = defaultServerIssuedTokenLifetime;
            maximumCachedNegotiationState = defaultServerMaxActiveNegotiations;
            negotiationTimeout = defaultServerMaxNegotiationLifetime;
            isClientAnonymous = false;
            standardsManager = defaultStandardsManager;
            securityStateEncoder = defaultSecurityStateEncoder;
            maximumConcurrentNegotiations = defaultServerMaxActiveNegotiations;
            // we rely on the transport encoders to enforce the message size except in the 
            // mixed mode nego case, where the client is unauthenticated and the maxMessageSize is too
            // large to be a mitigation
            maxMessageSize = int.MaxValue;
        }

        public override Task CloseAsync(CancellationToken token)
        {
            if (negotiationHost != null)
            {
                negotiationHost = null;
            }

            lock (ThisLock)
            {
                if (idlingNegotiationSessionTimer != null && !isTimerCancelled)
                {
                    isTimerCancelled = true;
                    idlingNegotiationSessionTimer.Cancel();
                }
            }
            return base.CloseAsync(token); ;
        }

        public override void OnAbort()
        {
            if (negotiationHost != null)
            {
                // this.negotiationHost.Abort();
                negotiationHost = null;
            }

            lock (ThisLock)
            {
                if (idlingNegotiationSessionTimer != null && !isTimerCancelled)
                {
                    isTimerCancelled = true;
                    idlingNegotiationSessionTimer.Cancel();
                }
            }
            base.OnAbort();
        }

        public override Task OpenAsync(CancellationToken token)
        {
            if (IssuerBindingContext == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.IssuerBuildContextNotSet, GetType())));
            }
            if (IssuedSecurityTokenParameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.IssuedSecurityTokenParametersNotSet, GetType())));
            }
            if (SecurityAlgorithmSuite == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecurityAlgorithmSuiteNotSet, GetType())));
            }
            if (IssuedTokenCache == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.IssuedTokenCacheNotSet, GetType())));
            }
            SetupServiceHost();
            if (negotiationHost != null)
            {
                negotiationHost.InitializeRuntime();
            }

            stateCache = new NegotiationTokenAuthenticatorStateCache<T>(NegotiationTimeout, MaximumCachedNegotiationState);
            sctUri = StandardsManager.SecureConversationDriver.TokenTypeUri;
            if (SecurityStateEncoder != null)
            {
                cookieSerializer = new SecurityContextCookieSerializer(SecurityStateEncoder, KnownTypes);
            }
            if (negotiationTimeout < TimeSpan.MaxValue)
            {
                lock (ThisLock)
                {
                    activeNegotiationChannels1 = new List<IChannel>();
                    activeNegotiationChannels2 = new List<IChannel>();
                    idlingNegotiationSessionTimer = new IOThreadTimer(new Action<object>(OnIdlingNegotiationSessionTimer), this, false);
                    isTimerCancelled = false;
                    idlingNegotiationSessionTimer.Set(negotiationTimeout);
                }
            }
            return base.OpenAsync();
        }

        protected override bool CanValidateTokenCore(SecurityToken token) => (token is SecurityContextSecurityToken);

        protected override ReadOnlyCollection<IAuthorizationPolicy> ValidateTokenCore(SecurityToken token)
        {
            SecurityContextSecurityToken sct = (SecurityContextSecurityToken)token;
            return sct.AuthorizationPolicies;
        }

        protected abstract Binding GetNegotiationBinding(Binding binding);
        protected abstract bool IsMultiLegNegotiation { get; }
        protected abstract MessageFilter GetListenerFilter();

        private void SetupServiceHost()
        {
            //   ChannelBuilder channelBuilder = new ChannelBuilder(this.IssuerBindingContext.Clone(), true);
            //   channelBuilder.Binding.Elements.Insert(0, new ReplyAdapterBindingElement());
            // channelBuilder.Binding = new CustomBinding(this.GetNegotiationBinding(channelBuilder.Binding));
            ChannelBuilder channelBuilder = IssuerBindingContext.BindingParameters.Find<ChannelBuilder>();
            negotiationHost = new NegotiationHost(this, ListenUri, channelBuilder, GetListenerFilter());
        }


        // message processing abstract method
        protected abstract BodyWriter ProcessRequestSecurityToken(Message request, RequestSecurityToken requestSecurityToken, out T negotiationState);
        protected abstract BodyWriter ProcessRequestSecurityTokenResponse(T negotiationState, Message request, RequestSecurityTokenResponse requestSecurityTokenResponse);

        // message handlers
        protected virtual void ParseMessageBody(Message message, out string context, out RequestSecurityToken requestSecurityToken, out RequestSecurityTokenResponse requestSecurityTokenResponse)
        {
            requestSecurityToken = null;
            requestSecurityTokenResponse = null;
            if (message.Headers.Action == RequestSecurityTokenAction.Value)
            {
                XmlDictionaryReader reader = message.GetReaderAtBodyContents();
                using (reader)
                {
                    requestSecurityToken = RequestSecurityToken.CreateFrom(StandardsManager, reader);
                    message.ReadFromBodyContentsToEnd(reader);
                }
                context = requestSecurityToken.Context;
            }
            else if (message.Headers.Action == RequestSecurityTokenResponseAction.Value)
            {
                XmlDictionaryReader reader = message.GetReaderAtBodyContents();
                using (reader)
                {
                    requestSecurityTokenResponse = RequestSecurityTokenResponse.CreateFrom(StandardsManager, reader);
                    message.ReadFromBodyContentsToEnd(reader);
                }
                context = requestSecurityTokenResponse.Context;
            }
            else
            {
                throw TraceUtility.ThrowHelperError(new SecurityNegotiationException(SR.Format(SR.InvalidActionForNegotiationMessage, message.Headers.Action)), message);
            }
        }

        private static Message CreateReply(Message request, XmlDictionaryString action, BodyWriter body)
        {
            if (request.Headers.MessageId != null)
            {
                Message reply = Message.CreateMessage(request.Version, ActionHeader.Create(action, request.Version.Addressing), body);
                reply.InitializeReply(request);
                return reply;
            }
            else
            {
                // the message id may not be present if MapToHttp is true
                return Message.CreateMessage(request.Version, ActionHeader.Create(action, request.Version.Addressing), body);
            }
        }

        private void OnTokenIssued(SecurityToken token)
        {
            if (issuedSecurityTokenHandler != null)
            {
                issuedSecurityTokenHandler(token, null);
            }
        }

        private void AddNegotiationChannelForIdleTracking()
        {
            if (OperationContext.Current.SessionId == null)
            {
                return;
            }
            lock (ThisLock)
            {
                if (idlingNegotiationSessionTimer == null)
                {
                    return;
                }
                IChannel channel = OperationContext.Current.Channel;
                if (!activeNegotiationChannels1.Contains(channel) && !activeNegotiationChannels2.Contains(channel))
                {
                    activeNegotiationChannels1.Add(channel);
                }
                if (isTimerCancelled)
                {
                    isTimerCancelled = false;
                    idlingNegotiationSessionTimer.Set(negotiationTimeout);
                }
            }
        }

        private void RemoveNegotiationChannelFromIdleTracking()
        {
            if (OperationContext.Current.SessionId == null)
            {
                return;
            }
            lock (ThisLock)
            {
                if (idlingNegotiationSessionTimer == null)
                {
                    return;
                }
                IChannel channel = OperationContext.Current.Channel;
                activeNegotiationChannels1.Remove(channel);
                activeNegotiationChannels2.Remove(channel);
                if (activeNegotiationChannels1.Count == 0 && activeNegotiationChannels2.Count == 0)
                {
                    isTimerCancelled = true;
                    idlingNegotiationSessionTimer.Cancel();
                }
            }
        }

        private void OnIdlingNegotiationSessionTimer(object state)
        {
            lock (ThisLock)
            {
                if (isTimerCancelled || (CommunicationObject.State != CommunicationState.Opened && CommunicationObject.State != CommunicationState.Opening))
                {
                    return;
                }

                try
                {
                    for (int i = 0; i < activeNegotiationChannels2.Count; ++i)
                    {
                        activeNegotiationChannels2[i].Abort();
                    }
                    List<IChannel> temp = activeNegotiationChannels2;
                    temp.Clear();
                    activeNegotiationChannels2 = activeNegotiationChannels1;
                    activeNegotiationChannels1 = temp;
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }
                }
                finally
                {
                    if (CommunicationObject.State == CommunicationState.Opened || CommunicationObject.State == CommunicationState.Opening)
                    {
                        if (activeNegotiationChannels1.Count == 0 && activeNegotiationChannels2.Count == 0)
                        {
                            isTimerCancelled = true;
                            idlingNegotiationSessionTimer.Cancel();
                        }
                        else
                        {
                            idlingNegotiationSessionTimer.Set(negotiationTimeout);
                        }
                    }
                }
            }
        }

        private Message ProcessRequestCore(Message request)
        {
            if (request == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(request));
            }
            Uri to = null;
            RequestSecurityToken rst = null;
            RequestSecurityTokenResponse rstr = null;
            string context = null;
            bool disposeRequest = false;
            bool isNegotiationFailure = true;
            T negotiationState = null;

            try
            {
                // validate the message size if needed
                if (maxMessageSize < int.MaxValue)
                {
                    string action = request.Headers.Action;
                    try
                    {
                        using (MessageBuffer buffer = request.CreateBufferedCopy(maxMessageSize))
                        {
                            request = buffer.CreateMessage();
                            disposeRequest = true;
                        }
                    }
                    catch (QuotaExceededException e)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityNegotiationException(SR.Format(SR.SecurityNegotiationMessageTooLarge, action, maxMessageSize), e));
                    }
                }
                try
                {
                    to = request.Headers.To;
                    ParseMessageBody(request, out context, out rst, out rstr);
                    // check if there is existing state
                    if (context != null)
                    {
                        negotiationState = stateCache.GetState(context);
                    }
                    else
                    {
                        negotiationState = null;
                    }
                    bool disposeState = false;
                    BodyWriter replyBody;
                    try
                    {
                        if (rst != null)
                        {
                            if (negotiationState != null)
                            {
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.Format(SR.NegotiationStateAlreadyPresent, context)));
                            }
                            replyBody = ProcessRequestSecurityToken(request, rst, out negotiationState);
                            lock (negotiationState.ThisLock)
                            {
                                if (negotiationState.IsNegotiationCompleted)
                                {
                                    // if session-sct add it to cache and add a redirect header
                                    if (!negotiationState.ServiceToken.IsCookieMode)
                                    {
                                        IssuedTokenCache.AddContext(negotiationState.ServiceToken);
                                    }
                                    OnTokenIssued(negotiationState.ServiceToken);
                                    // SecurityTraceRecordHelper.TraceServiceSecurityNegotiationCompleted(request, this, negotiationState.ServiceToken);
                                    disposeState = true;
                                }
                                else
                                {
                                    stateCache.AddState(context, negotiationState);
                                    disposeState = false;
                                }
                                AddNegotiationChannelForIdleTracking();
                            }
                        }
                        else
                        {
                            if (negotiationState == null)
                            {
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.Format(SR.CannotFindNegotiationState, context)));
                            }
                            lock (negotiationState.ThisLock)
                            {
                                replyBody = ProcessRequestSecurityTokenResponse(negotiationState, request, rstr);
                                if (negotiationState.IsNegotiationCompleted)
                                {
                                    // if session-sct add it to cache and add a redirect header
                                    if (!negotiationState.ServiceToken.IsCookieMode)
                                    {
                                        IssuedTokenCache.AddContext(negotiationState.ServiceToken);
                                    }
                                    OnTokenIssued(negotiationState.ServiceToken);
                                    // SecurityTraceRecordHelper.TraceServiceSecurityNegotiationCompleted(request, this, negotiationState.ServiceToken);
                                    disposeState = true;
                                }
                                else
                                {
                                    disposeState = false;
                                }
                            }
                        }

                        if (negotiationState.IsNegotiationCompleted && null != ListenUri)
                        {
                            //if (AuditLevel.Success == (this.messageAuthenticationAuditLevel & AuditLevel.Success))
                            //{
                            //    string primaryIdentity = negotiationState.GetRemoteIdentityName();
                            //    SecurityAuditHelper.WriteSecurityNegotiationSuccessEvent(this.auditLogLocation,
                            //        this.suppressAuditFailure, request, request.Headers.To, request.Headers.Action,
                            //        primaryIdentity, this.GetType().Name);
                            //}
                        }
                        isNegotiationFailure = false;
                    }
                    catch (Exception exception)
                    {
                        if (Fx.IsFatal(exception))
                        {
                            throw;
                        }

                        //                        if (PerformanceCounters.PerformanceCountersEnabled && null != this.ListenUri)
                        //                        {
                        //                            PerformanceCounters.AuthenticationFailed(request, this.ListenUri);
                        //                        }
                        //                        if (AuditLevel.Failure == (this.messageAuthenticationAuditLevel & AuditLevel.Failure))
                        //                        {
                        //                            try
                        //                            {
                        //                                string primaryIdentity = (negotiationState != null) ? negotiationState.GetRemoteIdentityName() : String.Empty;
                        //                                SecurityAuditHelper.WriteSecurityNegotiationFailureEvent(this.auditLogLocation,
                        //                                    this.suppressAuditFailure, request, request.Headers.To, request.Headers.Action,
                        //                                    primaryIdentity, this.GetType().Name, exception);
                        //                            }
                        //#pragma warning suppress 56500
                        //                            catch (Exception auditException)
                        //                            {
                        //                                if (Fx.IsFatal(auditException))
                        //                                    throw;

                        //                                DiagnosticUtility.TraceHandledException(auditException, TraceEventType.Error);
                        //                            }
                        //                        }

                        disposeState = true;
                        throw;
                    }
                    finally
                    {
                        if (disposeState)
                        {
                            if (negotiationState != null)
                            {
                                if (context != null)
                                {
                                    stateCache.RemoveState(context);
                                }
                                negotiationState.Dispose();
                            }
                        }
                    }

                    return CreateReply(request, (replyBody is RequestSecurityTokenResponseCollection) ? RequestSecurityTokenResponseFinalAction : RequestSecurityTokenResponseAction, replyBody);
                }
                finally
                {
                    if (disposeRequest)
                    {
                        request.Close();
                    }
                }
            }
            finally
            {
                if (isNegotiationFailure)
                {
                    AddNegotiationChannelForIdleTracking();
                }
                else if (negotiationState != null && negotiationState.IsNegotiationCompleted)
                {
                    RemoveNegotiationChannelFromIdleTracking();
                }
            }
        }

        // negotiation failure methods
        private Message HandleNegotiationException(Message request, Exception e) =>

            //SecurityTraceRecordHelper.TraceServiceSecurityNegotiationFailure<T>(
            //                        EventTraceActivityHelper.TryExtractActivity(request),
            //                        this,
            //                        e);
            CreateFault(request, e);

        private Message CreateFault(Message request, Exception e)
        {
            MessageVersion version = request.Version;
            FaultCode subCode;
            FaultReason reason;
            bool isSenderFault;
            if (e is SecurityTokenValidationException || e is System.ComponentModel.Win32Exception)
            {
                subCode = new FaultCode(TrustApr2004Strings.FailedAuthenticationFaultCode, TrustFeb2005Strings.Namespace);
                reason = new FaultReason(SR.Format(SR.FailedAuthenticationTrustFaultCode), CultureInfo.CurrentCulture);
                isSenderFault = true;
            }
            else if (e is QuotaExceededException)
            {
                // send a receiver fault so that the sender can retry
                subCode = new FaultCode(DotNetSecurityStrings.SecurityServerTooBusyFault, DotNetSecurityStrings.Namespace);
                reason = new FaultReason(SR.Format(SR.NegotiationQuotasExceededFaultReason), CultureInfo.CurrentCulture);
                isSenderFault = false;
            }
            else
            {
                subCode = new FaultCode(TrustApr2004Strings.InvalidRequestFaultCode, TrustFeb2005Strings.Namespace);
                reason = new FaultReason(SR.Format(SR.InvalidRequestTrustFaultCode), CultureInfo.CurrentCulture);
                isSenderFault = true;
            }
            FaultCode faultCode;
            if (isSenderFault)
            {
                faultCode = FaultCode.CreateSenderFaultCode(subCode);
            }
            else
            {
                faultCode = FaultCode.CreateReceiverFaultCode(subCode);
            }
            MessageFault fault = MessageFault.CreateFault(faultCode, reason);
            Message faultReply = Message.CreateMessage(version, fault, version.Addressing.DefaultFaultAction);
            faultReply.Headers.RelatesTo = request.Headers.MessageId;

            return faultReply;
        }

        private class NegotiationHost //: ServiceHostBase
        {
            private readonly NegotiationTokenAuthenticator<T> authenticator;
            private readonly Uri listenUri;
            private readonly ChannelBuilder channelBuilder;
            private readonly MessageFilter listenerFilter;

            public NegotiationHost(NegotiationTokenAuthenticator<T> authenticator, Uri listenUri, ChannelBuilder channelBuilder, MessageFilter listenerFilter)
            {
                this.authenticator = authenticator;
                this.listenUri = listenUri;
                this.channelBuilder = channelBuilder;
                this.listenerFilter = listenerFilter;
            }

            //protected override ServiceDescription CreateDescription(out IDictionary<string, ContractDescription> implementedContracts)
            //{
            //    implementedContracts = null;
            //    return null;
            //}

            internal void InitializeRuntime()
            {

                MessageFilter contractFilter = listenerFilter;
                int filterPriority = int.MaxValue - 20;
                List<Type> endpointChannelTypes = new List<Type> {  typeof(IReplyChannel),
                                                           typeof(IDuplexChannel),
                                                           typeof(IReplySessionChannel),
                                                           typeof(IDuplexSessionChannel) };
                Binding binding = authenticator.IssuerBindingContext.Binding;
                var bindingQname = new XmlQualifiedName(binding.Name, binding.Namespace);
                var channelDispatcher = new ChannelDispatcher(listenUri, binding, bindingQname.ToString(), binding, endpointChannelTypes)
                {
                    MessageVersion = binding.MessageVersion,
                    ManualAddressing = true
                };
                //TODO : Throttle
                // channelDispatcher.ServiceThrottle = new ServiceThrottle(this);
                // channelDispatcher.ServiceThrottle.MaxConcurrentCalls = this.authenticator.MaximumConcurrentNegotiations;
                // channelDispatcher.ServiceThrottle.MaxConcurrentSessions = this.authenticator.MaximumConcurrentNegotiations;
                EndpointDispatcher endpointDispatcher = new EndpointDispatcher(new EndpointAddress(listenUri, new AddressHeader[0]), "SecurityNegotiationContract", "http://tempuri.org/", true)
                {
                    DispatchRuntime = {
                    SingletonInstanceContext = new InstanceContext( null,  authenticator, false),
                    ConcurrencyMode = ConcurrencyMode.Multiple
                    },
                    AddressFilter = new MatchAllMessageFilter(),
                    ContractFilter = listenerFilter,
                    FilterPriority = filterPriority
                };
                endpointDispatcher.DispatchRuntime.PrincipalPermissionMode = PrincipalPermissionMode.None;
                endpointDispatcher.DispatchRuntime.InstanceContextProvider = new SingletonInstanceContextProvider(endpointDispatcher.DispatchRuntime);
                endpointDispatcher.DispatchRuntime.SynchronizationContext = null;
                endpointDispatcher.DispatchRuntime.UnhandledDispatchOperation = new DispatchOperation(endpointDispatcher.DispatchRuntime, "*", "*", "*")
                {
                    Formatter = new MessageOperationFormatter(),
                    Invoker = new NegotiationTokenAuthenticator<T>.NegotiationHost.NegotiationSyncInvoker(authenticator)
                };
                channelDispatcher.Endpoints.Add(endpointDispatcher);
                channelDispatcher.Init();
                Task openTask = channelDispatcher.OpenAsync();
                Fx.Assert(openTask.IsCompleted, "ChannelDispatcher should open synchronously");
                openTask.GetAwaiter().GetResult();
                ServiceDispatcher service = new ServiceDispatcher(channelDispatcher);
                channelBuilder.AddServiceDispatcher<IReplyChannel>(service, new ChannelDemuxerFilter(contractFilter, filterPriority));
            }

            private class NegotiationSyncInvoker : IOperationInvoker
            {
                private readonly NegotiationTokenAuthenticator<T> parent;

                internal NegotiationSyncInvoker(NegotiationTokenAuthenticator<T> parent) => this.parent = parent;

                public bool IsSynchronous => true;

                public object[] AllocateInputs() => EmptyArray<object>.Allocate(1);

                public ValueTask<(object returnValue, object[] outputs)> InvokeAsync(object instance, object[] inputs)
                {
                    object[] outputs = EmptyArray<object>.Allocate(0);
                    Message message = inputs[0] as Message;
                    if (message == null)
                    {
                        return new ValueTask<(object returnValue, object[] outputs)>(((object)null, outputs));
                    }
                    object returnVal = parent.ProcessRequestCore(message);
                    return new ValueTask<(object returnValue, object[] outputs)>((returnVal, outputs));
                }
            }
        }
    }
}
