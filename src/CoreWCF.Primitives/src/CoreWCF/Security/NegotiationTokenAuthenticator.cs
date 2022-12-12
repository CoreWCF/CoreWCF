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
        internal const string DefaultServerMaxNegotiationLifetimeString = "00:01:00";
        internal const string DefaultServerIssuedTokenLifetimeString = "10:00:00";
        internal const string DefaultServerIssuedTransitionTokenLifetimeString = "00:15:00";
        internal const int DefaultServerMaxActiveNegotiations = 128;
        internal static readonly TimeSpan s_defaultServerMaxNegotiationLifetime = TimeSpan.Parse(DefaultServerMaxNegotiationLifetimeString, CultureInfo.InvariantCulture);
        internal static readonly TimeSpan s_defaultServerIssuedTransitionTokenLifetime = TimeSpan.Parse(DefaultServerIssuedTransitionTokenLifetimeString, CultureInfo.InvariantCulture);
        internal const int DefaultServerMaxCachedTokens = 1000;
        internal const bool DefaultServerMaintainState = true;
        internal static readonly SecurityStandardsManager s_defaultStandardsManager = SecurityStandardsManager.DefaultInstance;
        internal static readonly SecurityStateEncoder s_defaultSecurityStateEncoder = new DataProtectionSecurityStateEncoder();
        private NegotiationTokenAuthenticatorStateCache<T> _stateCache;
        private NegotiationHost _negotiationHost;
        private bool _encryptStateInServiceToken;
        private TimeSpan _serviceTokenLifetime;
        private int _maximumCachedNegotiationState;
        private TimeSpan _negotiationTimeout;
        private bool _isClientAnonymous;
        private SecurityStandardsManager _standardsManager;
        private SecurityAlgorithmSuite _securityAlgorithmSuite;
        private SecurityTokenParameters _issuedSecurityTokenParameters;
        private ISecurityContextSecurityTokenCache _issuedTokenCache;
        private BindingContext _issuerBindingContext;
        private Uri _listenUri;

        // AuditLogLocation auditLogLocation;
        private bool _suppressAuditFailure;

        // AuditLevel messageAuthenticationAuditLevel;
        private SecurityStateEncoder _securityStateEncoder;
        private SecurityContextCookieSerializer _cookieSerializer;
        private IMessageFilterTable<EndpointAddress> _endpointFilterTable;
        private int _maxMessageSize;
        private IList<Type> _knownTypes;
        private int _maximumConcurrentNegotiations;
        private List<IChannel> _activeNegotiationChannels1;
        private List<IChannel> _activeNegotiationChannels2;
        private IOThreadTimer _idlingNegotiationSessionTimer;
        private bool _isTimerCancelled;

        protected NegotiationTokenAuthenticator() : base() => InitializeDefaults();

        public IssuedSecurityTokenHandler IssuedSecurityTokenHandler { get; set; }

        public RenewedSecurityTokenHandler RenewedSecurityTokenHandler { get; set; }

        // settings
        public bool EncryptStateInServiceToken
        {
            get => _encryptStateInServiceToken;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _encryptStateInServiceToken = value;
            }
        }

        public TimeSpan ServiceTokenLifetime
        {
            get => _serviceTokenLifetime;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SRCommon.TimeSpanMustBeGreaterThanTimeSpanZero));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.Format(SRCommon.SFxTimeoutOutOfRangeTooBig)));
                }
                _serviceTokenLifetime = value;
            }
        }

        public int MaximumCachedNegotiationState
        {
            get => _maximumCachedNegotiationState;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.Format(SRCommon.ValueMustBeNonNegative)));
                }
                _maximumCachedNegotiationState = value;
            }
        }

        public int MaximumConcurrentNegotiations
        {
            get => _maximumConcurrentNegotiations;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.Format(SRCommon.ValueMustBeNonNegative)));
                }
                _maximumConcurrentNegotiations = value;
            }
        }

        public TimeSpan NegotiationTimeout
        {
            get => _negotiationTimeout;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SRCommon.TimeSpanMustBeGreaterThanTimeSpanZero));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.Format(SRCommon.SFxTimeoutOutOfRangeTooBig)));
                }
                _negotiationTimeout = value;
            }
        }

        public bool IsClientAnonymous
        {
            get => _isClientAnonymous;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _isClientAnonymous = value;
            }
        }

        public SecurityAlgorithmSuite SecurityAlgorithmSuite
        {
            get => _securityAlgorithmSuite;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _securityAlgorithmSuite = value;
            }
        }

        public IMessageFilterTable<EndpointAddress> EndpointFilterTable
        {
            get => _endpointFilterTable;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _endpointFilterTable = value;
            }
        }

        ISecurityContextSecurityTokenCache ISecurityContextSecurityTokenCacheProvider.TokenCache => IssuedTokenCache;

        public virtual XmlDictionaryString RequestSecurityTokenAction => StandardsManager.TrustDriver.RequestSecurityTokenAction;

        public virtual XmlDictionaryString RequestSecurityTokenResponseAction => StandardsManager.TrustDriver.RequestSecurityTokenResponseAction;

        public virtual XmlDictionaryString RequestSecurityTokenResponseFinalAction => StandardsManager.TrustDriver.RequestSecurityTokenResponseFinalAction;

        public SecurityStandardsManager StandardsManager
        {
            get => _standardsManager;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _standardsManager = (value ?? SecurityStandardsManager.DefaultInstance);
            }
        }

        public SecurityTokenParameters IssuedSecurityTokenParameters
        {
            get => _issuedSecurityTokenParameters;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _issuedSecurityTokenParameters = value;
            }
        }

        public ISecurityContextSecurityTokenCache IssuedTokenCache
        {
            get => _issuedTokenCache;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _issuedTokenCache = value;
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
            get => _suppressAuditFailure;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _suppressAuditFailure = value;
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
            get => _issuerBindingContext;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }
                _issuerBindingContext = value.Clone();
            }
        }

        public Uri ListenUri
        {
            get => _listenUri;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _listenUri = value;
            }
        }

        public SecurityStateEncoder SecurityStateEncoder
        {
            get => _securityStateEncoder;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _securityStateEncoder = value;
            }
        }

        public IList<Type> KnownTypes
        {
            get => _knownTypes;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value != null)
                {
                    _knownTypes = new Collection<Type>(value);
                }
                else
                {
                    _knownTypes = null;
                }
            }
        }

        public int MaxMessageSize
        {
            get => _maxMessageSize;
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _maxMessageSize = value;
            }
        }

        protected string SecurityContextTokenUri { get; private set; }

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
            if (_securityStateEncoder == null && isCookieMode)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SctCookieNotSupported)));
            }
            byte[] cookieBlob = (isCookieMode) ? _cookieSerializer.CreateCookieFromSecurityContext(contextId, id, key, tokenEffectiveTime, tokenExpirationTime, keyGeneration,
                                keyEffectiveTime, keyExpirationTime, authorizationPolicies) : null;

            SecurityContextSecurityToken issuedToken = new SecurityContextSecurityToken(contextId, id, key, tokenEffectiveTime, tokenExpirationTime,
                authorizationPolicies, isCookieMode, cookieBlob, keyGeneration, keyEffectiveTime, keyExpirationTime);
            return issuedToken;
        }

        private void InitializeDefaults()
        {
            _encryptStateInServiceToken = !DefaultServerMaintainState;
            _serviceTokenLifetime = DefaultServerIssuedTokenLifetime;
            _maximumCachedNegotiationState = DefaultServerMaxActiveNegotiations;
            _negotiationTimeout = s_defaultServerMaxNegotiationLifetime;
            _isClientAnonymous = false;
            _standardsManager = s_defaultStandardsManager;
            _securityStateEncoder = s_defaultSecurityStateEncoder;
            _maximumConcurrentNegotiations = DefaultServerMaxActiveNegotiations;
            // we rely on the transport encoders to enforce the message size except in the
            // mixed mode nego case, where the client is unauthenticated and the maxMessageSize is too
            // large to be a mitigation
            _maxMessageSize = int.MaxValue;
        }

        public override Task CloseAsync(CancellationToken token)
        {
            if (_negotiationHost != null)
            {
                _negotiationHost = null;
            }

            lock (ThisLock)
            {
                if (_idlingNegotiationSessionTimer != null && !_isTimerCancelled)
                {
                    _isTimerCancelled = true;
                    _idlingNegotiationSessionTimer.Cancel();
                }
            }
            return base.CloseAsync(token); ;
        }

        public override void OnAbort()
        {
            if (_negotiationHost != null)
            {
                // this.negotiationHost.Abort();
                _negotiationHost = null;
            }

            lock (ThisLock)
            {
                if (_idlingNegotiationSessionTimer != null && !_isTimerCancelled)
                {
                    _isTimerCancelled = true;
                    _idlingNegotiationSessionTimer.Cancel();
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
            if (_negotiationHost != null)
            {
                _negotiationHost.InitializeRuntime();
            }

            _stateCache = new NegotiationTokenAuthenticatorStateCache<T>(NegotiationTimeout, MaximumCachedNegotiationState);
            SecurityContextTokenUri = StandardsManager.SecureConversationDriver.TokenTypeUri;
            if (SecurityStateEncoder != null)
            {
                _cookieSerializer = new SecurityContextCookieSerializer(SecurityStateEncoder, KnownTypes);
            }
            if (_negotiationTimeout < TimeSpan.MaxValue)
            {
                lock (ThisLock)
                {
                    _activeNegotiationChannels1 = new List<IChannel>();
                    _activeNegotiationChannels2 = new List<IChannel>();
                    _idlingNegotiationSessionTimer = new IOThreadTimer(new Action<object>(OnIdlingNegotiationSessionTimer), this, false);
                    _isTimerCancelled = false;
                    _idlingNegotiationSessionTimer.Set(_negotiationTimeout);
                }
            }
            return base.OpenAsync();
        }

        protected override bool CanValidateTokenCore(SecurityToken token) => (token is SecurityContextSecurityToken);

        protected override ValueTask<ReadOnlyCollection<IAuthorizationPolicy>> ValidateTokenCoreAsync(SecurityToken token)
        {
            SecurityContextSecurityToken sct = (SecurityContextSecurityToken)token;
            return new ValueTask<ReadOnlyCollection<IAuthorizationPolicy>>(sct.AuthorizationPolicies);
        }

        protected abstract Binding GetNegotiationBinding(Binding binding);
        protected abstract bool IsMultiLegNegotiation { get; }

        internal static TimeSpan DefaultServerIssuedTokenLifetime { get; } = TimeSpan.Parse(DefaultServerIssuedTokenLifetimeString, CultureInfo.InvariantCulture);

        protected abstract MessageFilter GetListenerFilter();

        private void SetupServiceHost()
        {
            //   ChannelBuilder channelBuilder = new ChannelBuilder(this.IssuerBindingContext.Clone(), true);
            //   channelBuilder.Binding.Elements.Insert(0, new ReplyAdapterBindingElement());
            // channelBuilder.Binding = new CustomBinding(this.GetNegotiationBinding(channelBuilder.Binding));
            ChannelBuilder channelBuilder = IssuerBindingContext.BindingParameters.Find<ChannelBuilder>();
            _negotiationHost = new NegotiationHost(this, ListenUri, channelBuilder, GetListenerFilter());
        }


        // message processing abstract method
        protected abstract ValueTask<(BodyWriter, T)> ProcessRequestSecurityTokenAsync(Message request, RequestSecurityToken requestSecurityToken);
        protected abstract ValueTask<BodyWriter> ProcessRequestSecurityTokenResponseAsync(T negotiationState, Message request, RequestSecurityTokenResponse requestSecurityTokenResponse);

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
            IssuedSecurityTokenHandler?.Invoke(token, null);
        }

        private void AddNegotiationChannelForIdleTracking()
        {
            if (OperationContext.Current.SessionId == null)
            {
                return;
            }
            lock (ThisLock)
            {
                if (_idlingNegotiationSessionTimer == null)
                {
                    return;
                }
                IChannel channel = OperationContext.Current.Channel;
                if (!_activeNegotiationChannels1.Contains(channel) && !_activeNegotiationChannels2.Contains(channel))
                {
                    _activeNegotiationChannels1.Add(channel);
                }
                if (_isTimerCancelled)
                {
                    _isTimerCancelled = false;
                    _idlingNegotiationSessionTimer.Set(_negotiationTimeout);
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
                if (_idlingNegotiationSessionTimer == null)
                {
                    return;
                }
                IChannel channel = OperationContext.Current.Channel;
                _activeNegotiationChannels1.Remove(channel);
                _activeNegotiationChannels2.Remove(channel);
                if (_activeNegotiationChannels1.Count == 0 && _activeNegotiationChannels2.Count == 0)
                {
                    _isTimerCancelled = true;
                    _idlingNegotiationSessionTimer.Cancel();
                }
            }
        }

        private void OnIdlingNegotiationSessionTimer(object state)
        {
            lock (ThisLock)
            {
                if (_isTimerCancelled || (CommunicationObject.State != CommunicationState.Opened && CommunicationObject.State != CommunicationState.Opening))
                {
                    return;
                }

                try
                {
                    for (int i = 0; i < _activeNegotiationChannels2.Count; ++i)
                    {
                        _activeNegotiationChannels2[i].Abort();
                    }
                    List<IChannel> temp = _activeNegotiationChannels2;
                    temp.Clear();
                    _activeNegotiationChannels2 = _activeNegotiationChannels1;
                    _activeNegotiationChannels1 = temp;
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
                        if (_activeNegotiationChannels1.Count == 0 && _activeNegotiationChannels2.Count == 0)
                        {
                            _isTimerCancelled = true;
                            _idlingNegotiationSessionTimer.Cancel();
                        }
                        else
                        {
                            _idlingNegotiationSessionTimer.Set(_negotiationTimeout);
                        }
                    }
                }
            }
        }

        private async ValueTask<Message> ProcessRequestCoreAsync(Message request)
        {
            if (request == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(request));
            }

            bool disposeRequest = false;
            bool isNegotiationFailure = true;
            T negotiationState = null;

            try
            {
                // validate the message size if needed
                if (_maxMessageSize < int.MaxValue)
                {
                    string action = request.Headers.Action;
                    try
                    {
                        using (MessageBuffer buffer = request.CreateBufferedCopy(_maxMessageSize))
                        {
                            request = buffer.CreateMessage();
                            disposeRequest = true;
                        }
                    }
                    catch (QuotaExceededException e)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityNegotiationException(SR.Format(SR.SecurityNegotiationMessageTooLarge, action, _maxMessageSize), e));
                    }
                }
                try
                {
                    Uri to = request.Headers.To;
                    ParseMessageBody(request, out string context, out RequestSecurityToken rst, out RequestSecurityTokenResponse rstr);
                    // check if there is existing state
                    if (context != null)
                    {
                        negotiationState = _stateCache.GetState(context);
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
                            (BodyWriter replyBody, T negotiatonState) processedRequestSecurityToken = await ProcessRequestSecurityTokenAsync(request, rst);//.AsTask().GetAwaiter().GetResult();
                            negotiationState = processedRequestSecurityToken.negotiatonState;
                            replyBody = processedRequestSecurityToken.replyBody;
                            await using (await negotiationState.AsyncLock.TakeLockAsync())
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
                                    _stateCache.AddState(context, negotiationState);
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

                            await using (await negotiationState.AsyncLock.TakeLockAsync())
                            {
                                replyBody = await ProcessRequestSecurityTokenResponseAsync(negotiationState, request,
                                    rstr); //.AsTask().GetAwaiter().GetResult();
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
                                    _stateCache.RemoveState(context);
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
            private readonly NegotiationTokenAuthenticator<T> _authenticator;
            private readonly Uri _listenUri;
            private readonly ChannelBuilder _channelBuilder;
            private readonly MessageFilter _listenerFilter;

            public NegotiationHost(NegotiationTokenAuthenticator<T> authenticator, Uri listenUri, ChannelBuilder channelBuilder, MessageFilter listenerFilter)
            {
                _authenticator = authenticator;
                _listenUri = listenUri;
                _channelBuilder = channelBuilder;
                _listenerFilter = listenerFilter;
            }

            //protected override ServiceDescription CreateDescription(out IDictionary<string, ContractDescription> implementedContracts)
            //{
            //    implementedContracts = null;
            //    return null;
            //}

            internal void InitializeRuntime()
            {

                MessageFilter contractFilter = _listenerFilter;
                int filterPriority = int.MaxValue - 20;
                List<Type> endpointChannelTypes = new List<Type> {  typeof(IReplyChannel),
                                                           typeof(IDuplexChannel),
                                                           typeof(IReplySessionChannel),
                                                           typeof(IDuplexSessionChannel) };
                Binding binding = _authenticator.IssuerBindingContext.Binding;
                var bindingQname = new XmlQualifiedName(binding.Name, binding.Namespace);
                var channelDispatcher = new ChannelDispatcher(_listenUri, binding, bindingQname.ToString(), binding, endpointChannelTypes)
                {
                    MessageVersion = binding.MessageVersion,
                    ManualAddressing = true
                };
                //TODO : Throttle
                // channelDispatcher.ServiceThrottle = new ServiceThrottle(this);
                // channelDispatcher.ServiceThrottle.MaxConcurrentCalls = this.authenticator.MaximumConcurrentNegotiations;
                // channelDispatcher.ServiceThrottle.MaxConcurrentSessions = this.authenticator.MaximumConcurrentNegotiations;
                EndpointDispatcher endpointDispatcher = new EndpointDispatcher(new EndpointAddress(_listenUri, new AddressHeader[0]), "SecurityNegotiationContract", "http://tempuri.org/", true)
                {
                    DispatchRuntime = {
                    SingletonInstanceContext = new InstanceContext( null,  _authenticator, false),
                    ConcurrencyMode = ConcurrencyMode.Multiple
                    },
                    AddressFilter = new MatchAllMessageFilter(),
                    ContractFilter = _listenerFilter,
                    FilterPriority = filterPriority
                };
                endpointDispatcher.DispatchRuntime.PrincipalPermissionMode = PrincipalPermissionMode.None;
                endpointDispatcher.DispatchRuntime.InstanceContextProvider = new SingletonInstanceContextProvider(endpointDispatcher.DispatchRuntime);
                endpointDispatcher.DispatchRuntime.SynchronizationContext = null;
                endpointDispatcher.DispatchRuntime.UnhandledDispatchOperation = new DispatchOperation(endpointDispatcher.DispatchRuntime, "*", "*", "*")
                {
                    Formatter = new MessageOperationFormatter(),
                    Invoker = new NegotiationTokenAuthenticator<T>.NegotiationHost.NegotiationSyncInvoker(_authenticator)
                };
                channelDispatcher.Endpoints.Add(endpointDispatcher);
                channelDispatcher.Init();
                Task openTask = channelDispatcher.OpenAsync();
                Fx.Assert(openTask.IsCompleted, "ChannelDispatcher should open synchronously");
                openTask.GetAwaiter().GetResult();
                ServiceDispatcher service = new ServiceDispatcher(channelDispatcher);
                _channelBuilder.AddServiceDispatcher<IReplyChannel>(service, new ChannelDemuxerFilter(contractFilter, filterPriority));
            }

            private class NegotiationSyncInvoker : IOperationInvoker
            {
                private readonly NegotiationTokenAuthenticator<T> _parent;

                internal NegotiationSyncInvoker(NegotiationTokenAuthenticator<T> parent) => _parent = parent;

                public bool IsSynchronous => true;

                public object[] AllocateInputs() => EmptyArray<object>.Allocate(1);

                public async ValueTask<(object returnValue, object[] outputs)> InvokeAsync(object instance, object[] inputs)
                {
                    object[] outputs = EmptyArray<object>.Allocate(0);
                    if (!(inputs[0] is Message message))
                    {
                        return ((object)null, outputs);
                    }
                    object returnVal = await _parent.ProcessRequestCoreAsync(message);
                    return (returnVal, outputs);
                }
            }
        }
    }
}
