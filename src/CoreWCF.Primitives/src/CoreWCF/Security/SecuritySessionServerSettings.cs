// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Security
{
    internal sealed class SecuritySessionServerSettings : IServiceDispatcherSecureConversationSessionSettings, ISecurityCommunicationObject
    {
        internal static readonly TimeSpan defaultKeyRenewalInterval = TimeSpan.FromHours(15);
        internal static readonly TimeSpan defaultKeyRolloverInterval = TimeSpan.FromMinutes(5);
        internal const bool defaultTolerateTransportFailures = true;
        internal const int defaultMaximumPendingSessions = 128;
        internal static readonly TimeSpan defaultInactivityTimeout = TimeSpan.FromMinutes(2);
        private int _maximumPendingSessions;
        private Dictionary<UniqueId, SecurityContextSecurityToken> _pendingSessions1;
        private Dictionary<UniqueId, SecurityContextSecurityToken> _pendingSessions2;
        private Dictionary<UniqueId, MessageFilter> _sessionFilters;
        private IOThreadTimer _inactivityTimer;
        private TimeSpan _inactivityTimeout;
        private bool _tolerateTransportFailures;
        private TimeSpan _maximumKeyRenewalInterval;
        private TimeSpan _keyRolloverInterval;
        private int _maximumPendingKeysPerSession;
        private SecurityProtocolFactory _sessionProtocolFactory;
        private readonly Dictionary<UniqueId, IServerSecuritySessionChannel> _activeSessions;
        private SecurityServiceDispatcher _securityServiceDispatcher;
        private ChannelBuilder _channelBuilder;
        private SecurityStandardsManager _standardsManager;
        private SecurityTokenParameters _issuedTokenParameters;
        private SecurityTokenResolver _sessionTokenResolver;
        private bool _acceptNewWork;
        private Uri _listenUri;
        private SecurityListenerSettingsLifetimeManager _settingsLifetimeManager;

        public SecuritySessionServerSettings()
        {
            _activeSessions = new Dictionary<UniqueId, IServerSecuritySessionChannel>();
            _maximumKeyRenewalInterval = defaultKeyRenewalInterval;
            _maximumPendingKeysPerSession = 5;
            _keyRolloverInterval = defaultKeyRolloverInterval;
            _inactivityTimeout = defaultInactivityTimeout;
            _tolerateTransportFailures = defaultTolerateTransportFailures;
            _maximumPendingSessions = defaultMaximumPendingSessions;
            WrapperCommunicationObj = new WrapperSecurityCommunicationObject(this);
        }

        internal ChannelBuilder ChannelBuilder
        {
            get
            {
                return _channelBuilder;
            }
            set
            {
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                _channelBuilder = value;
            }
        }

        internal WrapperSecurityCommunicationObject WrapperCommunicationObj { get; }

        internal SecurityListenerSettingsLifetimeManager SettingsLifetimeManager
        {
            get
            {
                return _settingsLifetimeManager;
            }
            set
            {
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                _settingsLifetimeManager = value;
            }
        }

        internal SecurityServiceDispatcher SecurityServiceDispatcher
        {
            get
            {
                return _securityServiceDispatcher;
            }
            set
            {
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                _securityServiceDispatcher = value;
            }
        }

        /// <summary>
        /// AcceptorChannelType will help determine if it's a duplex or simple reply channel
        /// </summary>
        internal Type AcceptorChannelType { get; set; }

        // TODO: Used by security tracing
        //private Uri Uri
        //{
        //    get
        //    {
        //        WrapperCommunicationObj.ThrowIfNotOpened();
        //        return _listenUri;
        //    }
        //}

        internal object ThisGlobalLock { get; } = new object();

        public SecurityTokenAuthenticator SessionTokenAuthenticator { get; private set; }

        public ISecurityContextSecurityTokenCache SessionTokenCache { get; private set; }

        public SecurityTokenResolver SessionTokenResolver => _sessionTokenResolver;

        public SecurityTokenParameters IssuedSecurityTokenParameters
        {
            get
            {
                return _issuedTokenParameters;
            }
            set
            {
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                _issuedTokenParameters = value;
            }
        }

        internal SecurityStandardsManager SecurityStandardsManager
        {
            get
            {
                return _standardsManager;
            }
            set
            {
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                _standardsManager = value;
            }
        }

        public bool TolerateTransportFailures
        {
            get
            {
                return _tolerateTransportFailures;
            }
            set
            {
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                _tolerateTransportFailures = value;
            }
        }

        public bool CanRenewSession { get; set; } = true;

        public int MaximumPendingSessions
        {
            get
            {
                return _maximumPendingSessions;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                _maximumPendingSessions = value;
            }
        }

        public TimeSpan InactivityTimeout
        {
            get
            {
                return _inactivityTimeout;
            }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }

                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                _inactivityTimeout = value;
            }
        }

        public TimeSpan MaximumKeyRenewalInterval
        {
            get
            {
                return _maximumKeyRenewalInterval;
            }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                _maximumKeyRenewalInterval = value;
            }
        }

        public TimeSpan KeyRolloverInterval
        {
            get
            {
                return _keyRolloverInterval;
            }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                _keyRolloverInterval = value;
            }
        }

        public int MaximumPendingKeysPerSession
        {
            get
            {
                return _maximumPendingKeysPerSession;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.ValueMustBeGreaterThanZero));
                }
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                _maximumPendingKeysPerSession = value;
            }
        }

        public SecurityProtocolFactory SessionProtocolFactory
        {
            get
            {
                return _sessionProtocolFactory;
            }
            set
            {
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                _sessionProtocolFactory = value;
            }
        }

        public MessageVersion MessageVersion { get; private set; }

        // ISecurityCommunicationObject members
        public TimeSpan DefaultOpenTimeout => ServiceDefaults.OpenTimeout;

        public TimeSpan DefaultCloseTimeout => ServiceDefaults.CloseTimeout;

        public void OnFaulted()
        {
        }

        public void OnOpened()
        {
        }

        public void OnOpening()
        {
        }

        public void OnAbort()
        {
            TimeoutHelper timeoutHelper = new TimeoutHelper(ServiceDefaults.ServiceHostCloseTimeout);
            AbortPendingChannels(timeoutHelper.GetCancellationToken());
            OnAbortCore();
        }

        internal void Abort()
        {
            WrapperCommunicationObj.Abort();
        }

        private void OnCloseCore(TimeSpan timeout)
        {
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            ClearPendingSessions();
            ClosePendingChannels(timeoutHelper.GetCancellationToken());
            if (_inactivityTimer != null)
            {
                _inactivityTimer.Cancel();
            }
            if (_sessionProtocolFactory != null)
            {
                _sessionProtocolFactory.OnCloseAsync(timeoutHelper.RemainingTime());
            }
            if (SessionTokenAuthenticator != null)
            {
                SecurityUtils.CloseTokenAuthenticatorIfRequiredAsync(SessionTokenAuthenticator, timeoutHelper.GetCancellationToken());
            }
        }

        private void OnAbortCore()
        {
            if (_inactivityTimer != null)
            {
                _inactivityTimer.Cancel();
            }
            if (_sessionProtocolFactory != null)
            {
                _sessionProtocolFactory.OnCloseAsync(TimeSpan.Zero);
            }
            if (SessionTokenAuthenticator != null)
            {
                SecurityUtils.AbortTokenAuthenticatorIfRequired(SessionTokenAuthenticator);
            }
        }

        private Task SetupSessionTokenAuthenticatorAsync()
        {
            RecipientServiceModelSecurityTokenRequirement requirement = new RecipientServiceModelSecurityTokenRequirement();
            _issuedTokenParameters.InitializeSecurityTokenRequirement(requirement);
            requirement.KeyUsage = SecurityKeyUsage.Signature;
            requirement.ListenUri = _listenUri;
            requirement.SecurityBindingElement = _sessionProtocolFactory.SecurityBindingElement;
            requirement.SecurityAlgorithmSuite = _sessionProtocolFactory.IncomingAlgorithmSuite;
            requirement.SupportSecurityContextCancellation = true;
            requirement.MessageSecurityVersion = _sessionProtocolFactory.MessageSecurityVersion.SecurityTokenVersion;
            // requirement.AuditLogLocation = sessionProtocolFactory.AuditLogLocation;
            // requirement.SuppressAuditFailure = sessionProtocolFactory.SuppressAuditFailure;
            // requirement.MessageAuthenticationAuditLevel = sessionProtocolFactory.MessageAuthenticationAuditLevel;
            requirement.Properties[ServiceModelSecurityTokenRequirement.MessageDirectionProperty] = MessageDirection.Input;
            if (_sessionProtocolFactory.EndpointFilterTable != null)
            {
                requirement.Properties[ServiceModelSecurityTokenRequirement.EndpointFilterTableProperty] = _sessionProtocolFactory.EndpointFilterTable;
            }
            SessionTokenAuthenticator = _sessionProtocolFactory.SecurityTokenManager.CreateSecurityTokenAuthenticator(requirement, out _sessionTokenResolver);
            if (!(SessionTokenAuthenticator is IIssuanceSecurityTokenAuthenticator))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecuritySessionRequiresIssuanceAuthenticator, typeof(IIssuanceSecurityTokenAuthenticator), SessionTokenAuthenticator.GetType())));
            }
            if (_sessionTokenResolver == null || (!(_sessionTokenResolver is ISecurityContextSecurityTokenCache)))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecuritySessionRequiresSecurityContextTokenCache, _sessionTokenResolver.GetType(), typeof(ISecurityContextSecurityTokenCache))));
            }
            SessionTokenCache = (ISecurityContextSecurityTokenCache)_sessionTokenResolver;
            return Task.CompletedTask;
        }

        public void StopAcceptingNewWork()
        {
            _acceptNewWork = false;
        }

        private int GetPendingSessionCount()
        {
            return _pendingSessions1.Count + _pendingSessions2.Count;
        }

        private void AbortPendingChannels(CancellationToken token)
        {
            ClosePendingChannels(token);
        }

        private async void ClosePendingChannels(CancellationToken token)
        {
            var tasks = new Task[_activeSessions.Count];
            lock (ThisGlobalLock)
            {
                int index = 0;
                if (typeof(IReplyChannel).Equals(AcceptorChannelType))
                {
                    foreach (ServerSecuritySimplexSessionChannel securitySessionSimplexChannel in _activeSessions.Values)
                    {
                        tasks[index] = securitySessionSimplexChannel.CloseAsync(token);
                        index++;
                    }
                }
            }
            await Task.WhenAll(tasks);
        }

        private void ConfigureSessionSecurityProtocolFactory()
        {
            //TODO while implementing message security

            //if (this.sessionProtocolFactory is SessionSymmetricMessageSecurityProtocolFactory)
            //{
            //    AddressingVersion addressing = MessageVersion.Default.Addressing;
            //    if (this.channelBuilder != null)
            //    {
            //        MessageEncodingBindingElement encoding = this.channelBuilder.Binding.Elements.Find<MessageEncodingBindingElement>();
            //        if (encoding != null)
            //        {
            //            addressing = encoding.MessageVersion.Addressing;
            //        }
            //    }

            //    if (addressing != AddressingVersion.WSAddressing10 && addressing != AddressingVersion.WSAddressingAugust2004)
            //    {
            //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
            //            new ProtocolException(SR.Format(SR.AddressingVersionNotSupported, addressing)));
            //    }

            //    SessionSymmetricMessageSecurityProtocolFactory messagePf = (SessionSymmetricMessageSecurityProtocolFactory)this.sessionProtocolFactory;
            //    if (!messagePf.ApplyIntegrity || !messagePf.RequireIntegrity)
            //    {
            //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecuritySessionRequiresMessageIntegrity)));
            //    }
            //    MessagePartSpecification bodyPart = new MessagePartSpecification(true);
            //    messagePf.ProtectionRequirements.IncomingSignatureParts.AddParts(bodyPart, this.SecurityStandardsManager.SecureConversationDriver.CloseAction);
            //    messagePf.ProtectionRequirements.IncomingSignatureParts.AddParts(bodyPart, this.SecurityStandardsManager.SecureConversationDriver.CloseResponseAction);
            //    messagePf.ProtectionRequirements.OutgoingSignatureParts.AddParts(bodyPart, this.SecurityStandardsManager.SecureConversationDriver.CloseResponseAction);
            //    messagePf.ProtectionRequirements.OutgoingSignatureParts.AddParts(bodyPart, this.SecurityStandardsManager.SecureConversationDriver.CloseAction);
            //    messagePf.ProtectionRequirements.OutgoingSignatureParts.AddParts(bodyPart, addressing.FaultAction);
            //    messagePf.ProtectionRequirements.OutgoingSignatureParts.AddParts(bodyPart, addressing.DefaultFaultAction);
            //    messagePf.ProtectionRequirements.OutgoingSignatureParts.AddParts(bodyPart, DotNetSecurityStrings.SecuritySessionFaultAction);
            //    if (messagePf.ApplyConfidentiality)
            //    {
            //        messagePf.ProtectionRequirements.OutgoingEncryptionParts.AddParts(MessagePartSpecification.NoParts, this.SecurityStandardsManager.SecureConversationDriver.CloseResponseAction);
            //        messagePf.ProtectionRequirements.OutgoingEncryptionParts.AddParts(MessagePartSpecification.NoParts, this.SecurityStandardsManager.SecureConversationDriver.CloseAction);
            //        messagePf.ProtectionRequirements.OutgoingEncryptionParts.AddParts(bodyPart, addressing.FaultAction);
            //        messagePf.ProtectionRequirements.OutgoingEncryptionParts.AddParts(bodyPart, addressing.DefaultFaultAction);
            //        messagePf.ProtectionRequirements.OutgoingEncryptionParts.AddParts(bodyPart, DotNetSecurityStrings.SecuritySessionFaultAction);
            //    }
            //    if (messagePf.RequireConfidentiality)
            //    {
            //        messagePf.ProtectionRequirements.IncomingEncryptionParts.AddParts(MessagePartSpecification.NoParts, this.SecurityStandardsManager.SecureConversationDriver.CloseAction);
            //        messagePf.ProtectionRequirements.IncomingEncryptionParts.AddParts(MessagePartSpecification.NoParts, this.SecurityStandardsManager.SecureConversationDriver.CloseResponseAction);
            //    }
            //    messagePf.SecurityTokenParameters = this.IssuedSecurityTokenParameters;
            //}
            //else
            if (_sessionProtocolFactory is SessionSymmetricTransportSecurityProtocolFactory sessionSymmetricProtocolFactory)
            {
                sessionSymmetricProtocolFactory.AddTimestamp = true;
                sessionSymmetricProtocolFactory.SecurityTokenParameters = IssuedSecurityTokenParameters;
                sessionSymmetricProtocolFactory.SecurityTokenParameters.RequireDerivedKeys = false;
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
            }
        }

        private void OnTokenRenewed(SecurityToken newToken, SecurityToken oldToken)
        {
            WrapperCommunicationObj.ThrowIfClosed();
            if (!_acceptNewWork)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new EndpointNotFoundException(SR.SecurityListenerClosing));
            }
            if (!(newToken is SecurityContextSecurityToken newSecurityContextToken))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.SessionTokenIsNotSecurityContextToken, newToken.GetType(), typeof(SecurityContextSecurityToken))));
            }
            if (!(oldToken is SecurityContextSecurityToken oldSecurityContextToken))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.SessionTokenIsNotSecurityContextToken, oldToken.GetType(), typeof(SecurityContextSecurityToken))));
            }
            IServerSecuritySessionChannel sessionChannel = FindSessionChannel(newSecurityContextToken.ContextId);
            if (sessionChannel == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.CannotFindSecuritySession, newSecurityContextToken.ContextId)));
            }
            sessionChannel.RenewSessionToken(newSecurityContextToken, oldSecurityContextToken);
        }

        /// <summary>
        /// This method creates SessionInitiationMessageServiceDispatcher which would act as 
        /// holder for SecurityReplySessionServiceChannelDispatcher (for Duplex we can implement simillar to ServerSecurityDuplexSessionChannel).
        /// Even though the Dispatcher is being added to demuxer, the ServiceChannelDispatcher is lazily initialized(based on first call) and that instance being hold to serve subsequent calls from the same client.
        /// When close received, the ServiceChannelDispatcher is cleared as well as the Dispatcher from Demuxer.
        /// </summary>
        /// <param name="sessionToken"></param>
        private void CreateSessionMessageServiceDispatcher(SecurityContextSecurityToken sessionToken, EndpointAddress remoteAddress)
        {
            lock (ThisGlobalLock)
            {
                MessageFilter sctFilter = new SecuritySessionFilter(sessionToken.ContextId, _sessionProtocolFactory.StandardsManager, (_sessionProtocolFactory.SecurityHeaderLayout == SecurityHeaderLayout.Strict), SecurityStandardsManager.SecureConversationDriver.RenewAction.Value, SecurityStandardsManager.SecureConversationDriver.RenewResponseAction.Value);
                SessionInitiationMessageServiceDispatcher sessionServiceDispatcher
                 = new SessionInitiationMessageServiceDispatcher(this, sessionToken, sctFilter, remoteAddress);
                //logic to separate for Duplex
                if (typeof(IReplyChannel).Equals(AcceptorChannelType))
                {
                    ChannelBuilder.AddServiceDispatcher<IReplyChannel>(sessionServiceDispatcher, new ChannelDemuxerFilter(sctFilter, int.MaxValue));
                }

                AddPendingSession(sessionToken.ContextId, sessionToken, sctFilter);
            }
        }

        private void OnTokenIssued(SecurityToken issuedToken, EndpointAddress tokenRequestor)
        {
            WrapperCommunicationObj.ThrowIfClosed(); //TODO mark open
            if (!_acceptNewWork)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new EndpointNotFoundException(SR.SecurityListenerClosing));
            }
            if (!(issuedToken is SecurityContextSecurityToken issuedSecurityContextToken))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.SessionTokenIsNotSecurityContextToken, issuedToken.GetType(), typeof(SecurityContextSecurityToken))));
            }
            // IServerReliableChannelBinder channelBinder = CreateChannelBinder(issuedSecurityContextToken, tokenRequestor ?? EndpointAddress.AnonymousAddress);
            CreateSessionMessageServiceDispatcher(issuedSecurityContextToken, tokenRequestor ?? EndpointAddress.AnonymousAddress);
        }

        internal SecurityContextSecurityToken GetSecurityContextSecurityToken(UniqueId sessionId)
        {
            if (_pendingSessions1 != null && _pendingSessions1.ContainsKey(sessionId))
            {
                return _pendingSessions1[sessionId];
            }

            if (_pendingSessions2 != null && _pendingSessions2.ContainsKey(sessionId))
            {
                return _pendingSessions2[sessionId];
            }

            return null;
        }

        private void OnTimer(object state)
        {
            if (WrapperCommunicationObj.State == CommunicationState.Closed
                || WrapperCommunicationObj.State == CommunicationState.Faulted)
            {
                return;
            }
            try
            {
                ClearPendingSessions();
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
                if (WrapperCommunicationObj.State != CommunicationState.Closed
                    && WrapperCommunicationObj.State != CommunicationState.Closing
                    && WrapperCommunicationObj.State != CommunicationState.Faulted)
                {
                    _inactivityTimer.Set(_inactivityTimeout);
                }
            }
        }

        private void AddPendingSession(UniqueId sessionId, SecurityContextSecurityToken securityToken, MessageFilter filter)
        {
            lock (ThisGlobalLock)
            {
                if ((GetPendingSessionCount() + 1) > MaximumPendingSessions)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new QuotaExceededException(SR.SecuritySessionLimitReached));
                }
                if (_pendingSessions1.ContainsKey(sessionId) || _pendingSessions2.ContainsKey(sessionId))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.SecuritySessionAlreadyPending, sessionId)));
                }
                _pendingSessions1.Add(sessionId, securityToken);
                _sessionFilters.Add(sessionId, filter);
            }
            //SecurityTraceRecordHelper.TracePendingSessionAdded(sessionId, this.Uri);
            //if (TD.SecuritySessionRatioIsEnabled())
            //{
            //    TD.SecuritySessionRatio(GetPendingSessionCount(), this.MaximumPendingSessions);
            //}
        }

        private void ClearPendingSessions()
        {
            lock (ThisGlobalLock)
            {
                if (_pendingSessions1.Count == 0 && _pendingSessions2.Count == 0)
                {
                    return;
                }
                foreach (UniqueId sessionId in _pendingSessions2.Keys)
                {
                    SecurityContextSecurityToken token = _pendingSessions2[sessionId];
                    try
                    {
                        //TryCloseBinder(channelBinder, this.CloseTimeout); // Replacing this line with below (being proactive rather reactive(in WCF))
                        ChannelBuilder.RemoveServiceDispatcher<IReplyChannel>(_sessionFilters[sessionId]);
                        SessionTokenCache.RemoveAllContexts(sessionId);
                    }
                    catch (CommunicationException e)
                    {
                        DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                    }
                    catch (TimeoutException e)
                    {
                        //if (TD.CloseTimeoutIsEnabled())
                        //{
                        //    TD.CloseTimeout(e.Message);
                        //}
                        DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                    }
                    catch (ObjectDisposedException e)
                    {
                        DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                    }
                    // SecurityTraceRecordHelper.TracePendingSessionClosed(sessionId, this.Uri);
                }
                _pendingSessions2.Clear();
                Dictionary<UniqueId, SecurityContextSecurityToken> temp = _pendingSessions2;
                _pendingSessions2 = _pendingSessions1;
                _pendingSessions1 = temp;
            }
        }

        internal bool RemovePendingSession(UniqueId sessionId)
        {
            bool result;
            lock (ThisGlobalLock)
            {
                if (_pendingSessions1.ContainsKey(sessionId))
                {
                    _pendingSessions1.Remove(sessionId);
                    result = true;
                }
                else if (_pendingSessions2.ContainsKey(sessionId))
                {
                    _pendingSessions2.Remove(sessionId);
                    result = true;
                }
                else
                {
                    result = false;
                }
            }
            /* if (result)
             {
                 SecurityTraceRecordHelper.TracePendingSessionActivated(sessionId, this.Uri);
                 if (TD.SecuritySessionRatioIsEnabled())
                 {
                     TD.SecuritySessionRatio(GetPendingSessionCount(), this.MaximumPendingSessions);
                 }
             }*/
            return result;
        }

        private IServerSecuritySessionChannel FindSessionChannel(UniqueId sessionId)
        {
            IServerSecuritySessionChannel result;
            lock (ThisGlobalLock)
            {
                _activeSessions.TryGetValue(sessionId, out result);
            }
            return result;
        }

        private void AddSessionChannel(UniqueId sessionId, IServerSecuritySessionChannel channel, MessageFilter filter)
        {
            lock (ThisGlobalLock)
            {
                _activeSessions.Add(sessionId, channel);
            }
        }

        internal void RemoveSessionChannel(string sessionId)
        {
            RemoveSessionChannel(new UniqueId(sessionId));
        }

        private void RemoveSessionChannel(UniqueId sessionId)
        {
            lock (ThisGlobalLock)
            {
                ChannelBuilder.RemoveServiceDispatcher<IReplyChannel>(_sessionFilters[sessionId]);
                _activeSessions.Remove(sessionId);
                _sessionFilters.Remove(sessionId);
            }
            //SecurityTraceRecordHelper.TraceActiveSessionRemoved(sessionId, this.Uri);
        }

        public Task CloseAsync(TimeSpan timeout)
        {
            return WrapperCommunicationObj.CloseAsync();
        }
        public Task OnCloseAsync(TimeSpan timeout)
        {
            OnCloseCore(timeout);
            return Task.CompletedTask;
        }

        public Task OpenAsync(TimeSpan timeout)
        {
            return WrapperCommunicationObj.OpenAsync();
        }
        public Task OnOpenAsync(TimeSpan timeout)
        {
            if (_sessionProtocolFactory == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SecuritySessionProtocolFactoryShouldBeSetBeforeThisOperation));
            }
            if (_standardsManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecurityStandardsManagerNotSet, GetType())));
            }
            if (_issuedTokenParameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.IssuedSecurityTokenParametersNotSet, GetType())));
            }
            if (_maximumKeyRenewalInterval < _keyRolloverInterval)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.KeyRolloverGreaterThanKeyRenewal));
            }
            if (_securityServiceDispatcher == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecurityChannelListenerNotSet, GetType())));
            }
            if (_settingsLifetimeManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecuritySettingsLifetimeManagerNotSet, GetType())));
            }
            MessageVersion = _channelBuilder.Binding.MessageVersion;
            _listenUri = _securityServiceDispatcher.BaseAddress;
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            _pendingSessions1 = new Dictionary<UniqueId, SecurityContextSecurityToken>();
            _pendingSessions2 = new Dictionary<UniqueId, SecurityContextSecurityToken>();
            _sessionFilters = new Dictionary<UniqueId, MessageFilter>();
            if (_inactivityTimeout < TimeSpan.MaxValue)
            {
                _inactivityTimer = new IOThreadTimer(new Action<object>(OnTimer), this, false);
                _inactivityTimer.Set(_inactivityTimeout);
            }
            ConfigureSessionSecurityProtocolFactory();
            _sessionProtocolFactory.OpenAsync(timeoutHelper.RemainingTime());
            SetupSessionTokenAuthenticatorAsync();
            ((IIssuanceSecurityTokenAuthenticator)SessionTokenAuthenticator).IssuedSecurityTokenHandler = OnTokenIssued;
            ((IIssuanceSecurityTokenAuthenticator)SessionTokenAuthenticator).RenewedSecurityTokenHandler = OnTokenRenewed;
            if (SessionTokenAuthenticator is SecuritySessionSecurityTokenAuthenticator securitySessionTokenAuthenticator)
            {
                securitySessionTokenAuthenticator.SecurityServiceDispatcher = SecurityServiceDispatcher;
            }
            _acceptNewWork = true;
            SecurityUtils.OpenTokenAuthenticatorIfRequiredAsync(SessionTokenAuthenticator, timeoutHelper.GetCancellationToken());
            SecuritySessionSecurityTokenAuthenticator securityAuth = (SecuritySessionSecurityTokenAuthenticator)SessionTokenAuthenticator;
            return Task.CompletedTask;
        }

        public void OnClosed()
        {
            throw new NotImplementedException();
        }

        public void OnClosing()
        {
            throw new NotImplementedException();
        }

        //Renaming SessionInitiationMessageHandler to SessionInitiationMessageServiceDispatcher
        //
        internal class SessionInitiationMessageServiceDispatcher : IServiceDispatcher
        {
            private readonly SecuritySessionServerSettings _settings;
            private readonly SecurityContextSecurityToken _sessionToken;
            private volatile IServiceChannelDispatcher _sessionChannelDispatcher;
            private readonly MessageFilter _messageFilter;
            private readonly EndpointAddress _remoteAddress;

            public SessionInitiationMessageServiceDispatcher(/*IServerReliableChannelBinder channelBinder,*/ SecuritySessionServerSettings settings, SecurityContextSecurityToken sessionToken, MessageFilter filter, EndpointAddress address)
            {
                _settings = settings;
                _sessionToken = sessionToken;
                _messageFilter = filter;
                _remoteAddress = address;
            }

            public Uri BaseAddress => throw new NotImplementedException();

            public Binding Binding => throw new NotImplementedException();

            public ServiceHostBase Host => throw new NotImplementedException();

            public AsyncLock AsyncLock { get; } = new AsyncLock();

            public IList<Type> SupportedChannelTypes => throw new NotImplementedException();

            /// <summary>
            /// ProcessMessage equivalent in WCF 
            /// </summary>
            /// <returns></returns>
            public async Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel channel)
            {
                if (_sessionChannelDispatcher == null)
                {
                    using (await AsyncLock.TakeLockAsync())
                    {
                        if (_sessionChannelDispatcher == null)
                        {
                            if (!_settings.RemovePendingSession(_sessionToken.ContextId))
                            {
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new CommunicationException(SR.Format(SR.SecuritySessionNotPending, _sessionToken.ContextId)));
                            }
                            if (_settings.AcceptorChannelType is IDuplexChannel)
                            {
                                throw new PlatformNotSupportedException();
                            }
                            else
                            {
                                ServerSecuritySimplexSessionChannel.SecurityReplySessionServiceChannelDispatcher
                                    replySessionChannelDispatcher = new ServerSecuritySimplexSessionChannel.
                                    SecurityReplySessionServiceChannelDispatcher(_settings, _sessionToken,
                                    null, _settings.SettingsLifetimeManager, channel, _remoteAddress);
                                await replySessionChannelDispatcher.OpenAsync(ServiceDefaults.OpenTimeout);
                                _sessionChannelDispatcher = replySessionChannelDispatcher;
                                _settings.AddSessionChannel(_sessionToken.ContextId, replySessionChannelDispatcher, _messageFilter);
                            }
                        }
                    }
                }
                return _sessionChannelDispatcher;
            }
        }

        private interface IServerSecuritySessionChannel
        {
            void RenewSessionToken(SecurityContextSecurityToken newToken, SecurityContextSecurityToken supportingToken);
        }

        private abstract class ServerSecuritySessionChannel : /*ChannelBase,*/ IServerSecuritySessionChannel
        {
            private FaultCode _renewFaultCode;
            private FaultReason _renewFaultReason;
            private FaultCode _sessionAbortedFaultCode;
            private FaultReason _sessionAbortedFaultReason;

            // Double-checked locking pattern requires volatile for read/write synchronization
            private bool _areFaultCodesInitialized;
            //private readonly IServerReliableChannelBinder _channelBinder;
            private readonly SecurityProtocol _securityProtocol;

            // This is used to sign outgoing messages
            private SecurityContextSecurityToken _currentSessionToken;
            private readonly UniqueId _sessionId;

            // These are renewed tokens that have not been used as yet
            private readonly List<SecurityContextSecurityToken> _futureSessionTokens;
            private RequestContext _initialRequestContext;
            private bool _isInputClosed;
            private readonly MessageVersion _messageVersion;
            private readonly SecurityListenerSettingsLifetimeManager _settingsLifetimeManager;
            private bool _hasSecurityStateReference;

            protected ServerSecuritySessionChannel(SecuritySessionServerSettings settings,
                SecurityContextSecurityToken sessionToken,
                object listenerSecurityProtocolState,
                SecurityListenerSettingsLifetimeManager settingsLifetimeManager, EndpointAddress address)
            {
                Settings = settings;
                _messageVersion = settings.MessageVersion;
                // See issue #285
                // channelBinder.Faulted += this.OnInnerFaulted;
                _securityProtocol = Settings.SessionProtocolFactory.CreateSecurityProtocol(null, null, true, TimeSpan.Zero);
                if (!(_securityProtocol is IAcceptorSecuritySessionProtocol))
                {
                    Fx.Assert("Security protocol must be IAcceptorSecuritySessionProtocol.");
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ProtocolMisMatch, "IAcceptorSecuritySessionProtocol", GetType().ToString())));
                }
                _currentSessionToken = sessionToken;
                _sessionId = sessionToken.ContextId;
                _futureSessionTokens = new List<SecurityContextSecurityToken>(1);
                ((IAcceptorSecuritySessionProtocol)_securityProtocol).SetOutgoingSessionToken(sessionToken);
                ((IAcceptorSecuritySessionProtocol)_securityProtocol).SetSessionTokenAuthenticator(_sessionId, Settings.SessionTokenAuthenticator, Settings.SessionTokenResolver);
                _settingsLifetimeManager = settingsLifetimeManager;
                LocalAddress = address;
                LocalLock = new object();
            }

            protected SecuritySessionServerSettings Settings { get; }

            protected virtual bool CanDoSecurityCorrelation => false;

            internal TimeSpan InternalSendTimeout => ServiceDefaults.SendTimeout;

            public EndpointAddress LocalAddress { get; }

            public object LocalLock { get; }

            public CommunicationState State => Settings.WrapperCommunicationObj.State;

            public virtual Task OpenAsync(TimeSpan timeout)
            {
                _securityProtocol.OpenAsync(timeout);
                if (CanDoSecurityCorrelation)
                {
                    ((IAcceptorSecuritySessionProtocol)_securityProtocol).ReturnCorrelationState = true;
                } // if an abort happened concurrently with the open, then return
                if (State == CommunicationState.Closed || State == CommunicationState.Closing)
                {
                    return Task.CompletedTask;
                }
                _settingsLifetimeManager.AddReference();
                _hasSecurityStateReference = true;
                return Task.CompletedTask;
            }

            protected virtual void AbortCore()
            {
                if (_securityProtocol != null)
                {
                    TimeoutHelper timeout = new TimeoutHelper(ServiceDefaults.CloseTimeout);
                    _securityProtocol.CloseAsync(true, timeout.RemainingTime());
                }
                Settings.SessionTokenCache.RemoveAllContexts(_currentSessionToken.ContextId);
                bool abortLifetimeManager = false;
                lock (LocalLock)
                {
                    if (_hasSecurityStateReference)
                    {
                        abortLifetimeManager = true;
                        _hasSecurityStateReference = false;
                    }
                }
                if (abortLifetimeManager)
                {
                    _settingsLifetimeManager.Abort();
                }
            }

            protected virtual void CloseCore(CancellationToken token)
            {
                try
                {
                    TimeoutHelper helper = new TimeoutHelper(ServiceDefaults.CloseTimeout);
                    if (_securityProtocol != null)
                    {
                        _securityProtocol.CloseAsync(false, helper.RemainingTime()); ;
                    }
                    bool closeLifetimeManager = false;
                    lock (LocalLock)
                    {
                        if (_hasSecurityStateReference)
                        {
                            closeLifetimeManager = true;
                            _hasSecurityStateReference = false;
                        }
                    }
                    if (closeLifetimeManager)
                    {
                        _settingsLifetimeManager.CloseAsync(helper.RemainingTime());
                    }
                }
                catch (CommunicationObjectAbortedException)
                {
                    if (State != CommunicationState.Closed)
                    {
                        throw;
                    }
                    // a parallel thread aborted the channel. Ignore the exception
                }
                Settings.SessionTokenCache.RemoveAllContexts(_currentSessionToken.ContextId);
            }

            protected abstract void OnCloseMessageReceived(RequestContext requestContext, Message message, SecurityProtocolCorrelationState correlationState, CancellationToken token);

            protected abstract void OnCloseResponseMessageReceived(RequestContext requestContext, Message message, SecurityProtocolCorrelationState correlationState, TimeSpan timeout);

            public void RenewSessionToken(SecurityContextSecurityToken newToken, SecurityContextSecurityToken supportingToken)
            {
                ThrowIfClosedOrNotOpen();
                // enforce that the token being renewed is the current session token
                lock (LocalLock)
                {
                    if (supportingToken.ContextId != _currentSessionToken.ContextId || supportingToken.KeyGeneration != _currentSessionToken.KeyGeneration)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.CurrentSessionTokenNotRenewed, supportingToken.KeyGeneration, _currentSessionToken.KeyGeneration)));
                    }
                    if (_futureSessionTokens.Count == Settings.MaximumPendingKeysPerSession)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.TooManyPendingSessionKeys));
                    }
                    _futureSessionTokens.Add(newToken);
                }
                // SecurityTraceRecordHelper.TraceNewServerSessionKeyIssued(newToken, supportingToken, GetLocalUri());
            }

            //protected Uri GetLocalUri()
            //{
            //    if (_channelBinder.LocalAddress == null)
            //    {
            //        return null;
            //    }
            //    else
            //    {
            //        return _channelBinder.LocalAddress.Uri;
            //    }
            //}

            // TODO: Wire up the channel binder faults to call this.
            //private void OnInnerFaulted(IReliableChannelBinder sender, Exception exception)
            //{
            //    OnFaulted(exception);
            //}

            private SecurityContextSecurityToken GetSessionToken(SecurityMessageProperty securityProperty)
            {
                SecurityContextSecurityToken sct = (securityProperty.ProtectionToken != null) ? securityProperty.ProtectionToken.SecurityToken as SecurityContextSecurityToken : null;
                if (sct != null && sct.ContextId == _sessionId)
                {
                    return sct;
                }
                if (securityProperty.HasIncomingSupportingTokens)
                {
                    for (int i = 0; i < securityProperty.IncomingSupportingTokens.Count; ++i)
                    {
                        if (securityProperty.IncomingSupportingTokens[i].SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.Endorsing)
                        {
                            sct = (securityProperty.IncomingSupportingTokens[i].SecurityToken as SecurityContextSecurityToken);
                            if (sct != null && sct.ContextId == _sessionId)
                            {
                                return sct;
                            }
                        }
                    }
                }
                return null;
            }

            private bool CheckIncomingToken(RequestContext requestContext, Message message, SecurityProtocolCorrelationState correlationState, TimeSpan timeout)
            {
                SecurityMessageProperty securityProperty = message.Properties.Security;
                // this is guaranteed to be non-null and matches the session ID since the binding checked it
                SecurityContextSecurityToken incomingToken = GetSessionToken(securityProperty);
                if (incomingToken == null)
                {
                    throw TraceUtility.ThrowHelperWarning(new MessageSecurityException(SR.NoSessionTokenPresentInMessage), message);
                }
                // the incoming token's key should have been issued within keyRenewalPeriod time in the past
                // if not, send back a renewal fault. However if this is a session close message then its ok to not require the client 
                // to renew the key in order to send the close.
                if (incomingToken.KeyExpirationTime < DateTime.UtcNow &&
                    message.Headers.Action != Settings.SecurityStandardsManager.SecureConversationDriver.CloseAction.Value)
                {
                    if (Settings.CanRenewSession)
                    {
                        SendRenewFault(requestContext, correlationState, timeout);
                        return false;
                    }
                    else
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new Exception(SR.Format(SR.SecurityContextKeyExpired, incomingToken.ContextId, incomingToken.KeyGeneration)));
                    }
                }
                // this is a valid token. If it corresponds to a newly issued session token, make it the current
                // session token.
                lock (LocalLock)
                {
                    if (_futureSessionTokens.Count > 0 && incomingToken.KeyGeneration != _currentSessionToken.KeyGeneration)
                    {
                        bool changedCurrentSessionToken = false;
                        for (int i = 0; i < _futureSessionTokens.Count; ++i)
                        {
                            if (_futureSessionTokens[i].KeyGeneration == incomingToken.KeyGeneration)
                            {
                                // let the current token expire after KeyRollover time interval
                                DateTime keyRolloverTime = TimeoutHelper.Add(DateTime.UtcNow, Settings.KeyRolloverInterval);
                                Settings.SessionTokenCache.UpdateContextCachingTime(_currentSessionToken, keyRolloverTime);
                                _currentSessionToken = _futureSessionTokens[i];
                                _futureSessionTokens.RemoveAt(i);
                                ((IAcceptorSecuritySessionProtocol)_securityProtocol).SetOutgoingSessionToken(_currentSessionToken);
                                changedCurrentSessionToken = true;
                                break;
                            }
                        }
                        if (changedCurrentSessionToken)
                        {
                            // SecurityTraceRecordHelper.TraceServerSessionKeyUpdated(this.currentSessionToken, GetLocalUri());
                            // remove all renewed tokens that will never be used.
                            for (int i = 0; i < _futureSessionTokens.Count; ++i)
                            {
                                Settings.SessionTokenCache.RemoveContext(_futureSessionTokens[i].ContextId, _futureSessionTokens[i].KeyGeneration);
                            }
                            _futureSessionTokens.Clear();
                        }
                    }
                }

                return true;
            }

            public RequestContext ReceiveRequest(RequestContext initialRequestContext)
            {
                return ReceiveRequest(ServiceDefaults.ReceiveTimeout, initialRequestContext);
            }

            public RequestContext ReceiveRequest(TimeSpan timeout, RequestContext initialRequestContext)
            {
                _initialRequestContext = initialRequestContext;
                if (TryReceiveRequest(timeout, out RequestContext requestContext))
                {
                    return requestContext;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TimeoutException());
                }
            }

            public bool TryReceiveRequest(TimeSpan timeout, out RequestContext requestContext)
            {
                ThrowIfFaulted();
                TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
                while (true)
                {
                    if (_isInputClosed || State == CommunicationState.Faulted)
                    {
                        break;
                    }
                    if (timeoutHelper.RemainingTime() == TimeSpan.Zero)
                    {
                        requestContext = null;
                        return false;
                    }

                    RequestContext innerRequestContext;
                    if (_initialRequestContext != null)
                    {
                        innerRequestContext = _initialRequestContext;
                        _initialRequestContext = null;
                    }
                    else
                    {
                        requestContext = null;
                        return false;
                    }
                    if (innerRequestContext == null)
                    {
                        // the channel could have been aborted or closed
                        break;
                    }
                    if (_isInputClosed && innerRequestContext.RequestMessage != null)
                    {
                        Message message = innerRequestContext.RequestMessage;
                        try
                        {
                            ProtocolException error = ProtocolException.ReceiveShutdownReturnedNonNull(message);
                            throw TraceUtility.ThrowHelperWarning(error, message);
                        }
                        finally
                        {
                            message.Close();
                            innerRequestContext.Abort();
                        }
                    }
                    Message requestMessage = ProcessRequestContext(innerRequestContext, timeoutHelper.RemainingTime(), out SecurityProtocolCorrelationState correlationState, out bool isSecurityProcessingFailure);
                    if (requestMessage != null)
                    {
                        requestContext = new SecuritySessionRequestContext(innerRequestContext, requestMessage, correlationState, this);
                        return true;
                    }
                }
                ThrowIfFaulted();
                requestContext = null;
                return true;
            }

            private void ThrowIfFaulted()
            {
                Settings.WrapperCommunicationObj.ThrowIfFaulted();
            }

            //public override T GetProperty<T>()
            //{
            //    if (typeof(T) == typeof(FaultConverter) && (this.channelBinder != null))
            //    {
            //        return new SecurityChannelFaultConverter(this.channelBinder.Channel) as T;
            //    }

            //    T result = base.GetProperty<T>();
            //    if ((result == null) && (channelBinder != null) && (channelBinder.Channel != null))
            //    {
            //        result = channelBinder.Channel.GetProperty<T>();
            //    }

            //    return result;
            //}

            private void SendFaultIfRequired(Exception e, Message unverifiedMessage, RequestContext requestContext, TimeSpan timeout)
            {
                try
                {
                    MessageFault fault = SecurityUtils.CreateSecurityMessageFault(e, _securityProtocol.SecurityProtocolFactory.StandardsManager);
                    if (fault == null)
                    {
                        return;
                    }
                    TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
                    try
                    {
                        using (Message faultMessage = Message.CreateMessage(unverifiedMessage.Version, fault, unverifiedMessage.Version.Addressing.DefaultFaultAction))
                        {
                            if (unverifiedMessage.Headers.MessageId != null)
                            {
                                faultMessage.InitializeReply(unverifiedMessage);
                            }

                            requestContext.ReplyAsync(faultMessage, timeoutHelper.GetCancellationToken());
                            requestContext.CloseAsync(timeoutHelper.GetCancellationToken());
                        }
                    }
                    catch (CommunicationException ex)
                    {
                        DiagnosticUtility.TraceHandledException(ex, TraceEventType.Information);
                    }
                    catch (TimeoutException ex)
                    {
                        //if (TD.CloseTimeoutIsEnabled())
                        //{
                        //    TD.CloseTimeout(e.Message);
                        //}
                        DiagnosticUtility.TraceHandledException(ex, TraceEventType.Information);
                    }
                }
                finally
                {
                    unverifiedMessage.Close();
                    requestContext.Abort();
                }
            }

            private bool ShouldWrapException(Exception e)
            {
                return ((e is FormatException) || (e is XmlException));
            }

            private Message ProcessRequestContext(RequestContext requestContext, TimeSpan timeout, out SecurityProtocolCorrelationState correlationState, out bool isSecurityProcessingFailure)
            {
                correlationState = null;
                isSecurityProcessingFailure = false;
                if (requestContext == null)
                {
                    return null;
                }

                Message result = null;
                Message message = requestContext.RequestMessage;
                bool cleanupContextState = true;
                try
                {
                    TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
                    Message unverifiedMessage = message;
                    Exception securityException = null;
                    try
                    {
                        correlationState = VerifyIncomingMessage(ref message, timeoutHelper.RemainingTime());
                        // message.Properties.Security
                    }
                    catch (MessageSecurityException e)
                    {
                        isSecurityProcessingFailure = true;
                        securityException = e;
                        throw;
                    }
                    if (securityException != null)
                    {
                        // SendFaultIfRequired closes the unverified message and context
                        SendFaultIfRequired(securityException, unverifiedMessage, requestContext, timeoutHelper.RemainingTime());
                        cleanupContextState = false;
                        return null;
                    }
                    else if (CheckIncomingToken(requestContext, message, correlationState, timeoutHelper.RemainingTime()))
                    {
                        if (message.Headers.Action == Settings.SecurityStandardsManager.SecureConversationDriver.CloseAction.Value)
                        {
                            //  SecurityTraceRecordHelper.TraceServerSessionCloseReceived(this.currentSessionToken, GetLocalUri());
                            _isInputClosed = true;
                            // OnCloseMessageReceived is responsible for closing the message and requestContext if required.
                            OnCloseMessageReceived(requestContext, message, correlationState, timeoutHelper.GetCancellationToken());
                            correlationState = null;
                        }
                        else if (message.Headers.Action == Settings.SecurityStandardsManager.SecureConversationDriver.CloseResponseAction.Value)
                        {
                            // SecurityTraceRecordHelper.TraceServerSessionCloseResponseReceived(this.currentSessionToken, GetLocalUri());
                            _isInputClosed = true;
                            // OnCloseResponseMessageReceived is responsible for closing the message and requestContext if required.
                            OnCloseResponseMessageReceived(requestContext, message, correlationState, timeoutHelper.RemainingTime());
                            correlationState = null;
                        }
                        else
                        {
                            result = message;
                        }
                        cleanupContextState = false;
                    }
                }
                catch (Exception e)
                {
                    if ((e is CommunicationException) || (e is TimeoutException) || (Fx.IsFatal(e)) || !ShouldWrapException(e))
                    {
                        throw;
                    }
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.MessageSecurityVerificationFailed, e));
                }
                finally
                {
                    if (cleanupContextState)
                    {
                        if (requestContext.RequestMessage != null)
                        {
                            requestContext.RequestMessage.Close();
                        }
                        requestContext.Abort();
                    }
                }

                return result;
            }

            internal void CheckOutgoingToken()
            {
                lock (LocalLock)
                {
                    if (_currentSessionToken.KeyExpirationTime < DateTime.UtcNow)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new Exception(SR.SecuritySessionKeyIsStale));
                    }
                }
            }

            internal void SecureApplicationMessage(ref Message message, SecurityProtocolCorrelationState correlationState, CancellationToken token)
            {
                ThrowIfFaulted();
                ThrowIfClosedOrNotOpen();
                CheckOutgoingToken();
                message = _securityProtocol.SecureOutgoingMessage(message, token);
            }

            private void ThrowIfClosedOrNotOpen()
            {
                //throw new NotImplementedException();
            }

            internal SecurityProtocolCorrelationState VerifyIncomingMessage(ref Message message, TimeSpan timeout)
            {
                return _securityProtocol.VerifyIncomingMessage(ref message, timeout, null);
            }

            private void PrepareReply(Message request, Message reply)
            {
                if (request.Headers.ReplyTo != null)
                {
                    request.Headers.ReplyTo.ApplyTo(reply);
                }
                else if (request.Headers.From != null)
                {
                    request.Headers.From.ApplyTo(reply);
                }
                if (request.Headers.MessageId != null)
                {
                    reply.Headers.RelatesTo = request.Headers.MessageId;
                }
                //TraceUtility.CopyActivity(request, reply);
                //if (TraceUtility.PropagateUserActivity || TraceUtility.ShouldPropagateActivity)
                //{
                //    TraceUtility.AddActivityHeader(reply);
                //}
            }

            protected void InitializeFaultCodesIfRequired()
            {
                if (!_areFaultCodesInitialized)
                {
                    lock (LocalLock)
                    {
                        if (!_areFaultCodesInitialized)
                        {
                            SecurityStandardsManager standardsManager = _securityProtocol.SecurityProtocolFactory.StandardsManager;
                            SecureConversationDriver scDriver = standardsManager.SecureConversationDriver;
                            _renewFaultCode = FaultCode.CreateSenderFaultCode(scDriver.RenewNeededFaultCode.Value, scDriver.Namespace.Value);
                            _renewFaultReason = new FaultReason(SR.SecurityRenewFaultReason, System.Globalization.CultureInfo.InvariantCulture);
                            _sessionAbortedFaultCode = FaultCode.CreateSenderFaultCode(DotNetSecurityStrings.SecuritySessionAbortedFault, DotNetSecurityStrings.Namespace);
                            _sessionAbortedFaultReason = new FaultReason(SR.SecuritySessionAbortedFaultReason, System.Globalization.CultureInfo.InvariantCulture);
                            _areFaultCodesInitialized = true;
                        }
                    }
                }
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "General exception types are not being caught")]
            private void SendRenewFault(RequestContext requestContext, SecurityProtocolCorrelationState correlationState, TimeSpan timeout)
            {
                Message message = requestContext.RequestMessage;
                try
                {
                    InitializeFaultCodesIfRequired();
                    MessageFault renewFault = MessageFault.CreateFault(_renewFaultCode, _renewFaultReason);
                    Message response;
                    if (message.Headers.MessageId != null)
                    {
                        response = Message.CreateMessage(message.Version, renewFault, DotNetSecurityStrings.SecuritySessionFaultAction);
                        response.InitializeReply(message);
                    }
                    else
                    {
                        response = Message.CreateMessage(message.Version, renewFault, DotNetSecurityStrings.SecuritySessionFaultAction);
                    }
                    try
                    {
                        PrepareReply(message, response);
                        TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
                        response = _securityProtocol.SecureOutgoingMessage(response, timeoutHelper.GetCancellationToken());
                        response.Properties.AllowOutputBatching = false;
                        SendMessage(requestContext, response, timeoutHelper.GetCancellationToken());
                    }
                    finally
                    {
                        response.Close();
                    }
                    //  SecurityTraceRecordHelper.TraceSessionRenewalFaultSent(this.currentSessionToken, GetLocalUri(), message);
                }
                catch (CommunicationException/* e*/)
                {
                    //SecurityTraceRecordHelper.TraceRenewFaultSendFailure(this.currentSessionToken, GetLocalUri(), e);
                }
                catch (TimeoutException/* e*/)
                {
                    // SecurityTraceRecordHelper.TraceRenewFaultSendFailure(this.currentSessionToken, GetLocalUri(), e);
                }
            }

            private Message ProcessCloseRequest(Message request)
            {
                RequestSecurityToken rst;
                XmlDictionaryReader bodyReader = request.GetReaderAtBodyContents();
                using (bodyReader)
                {
                    rst = Settings.SecurityStandardsManager.TrustDriver.CreateRequestSecurityToken(bodyReader);
                    request.ReadFromBodyContentsToEnd(bodyReader);
                }
                if (rst.RequestType != null && rst.RequestType != Settings.SecurityStandardsManager.TrustDriver.RequestTypeClose)
                {
                    throw TraceUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.InvalidRstRequestType, rst.RequestType)), request);
                }
                if (rst.CloseTarget == null)
                {
                    throw TraceUtility.ThrowHelperWarning(new MessageSecurityException(SR.NoCloseTargetSpecified), request);
                }
                if (!(rst.CloseTarget is SecurityContextKeyIdentifierClause sctSkiClause) || !SecuritySessionSecurityTokenAuthenticator.DoesSkiClauseMatchSigningToken(sctSkiClause, request))
                {
                    throw TraceUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.BadCloseTarget, rst.CloseTarget)), request);
                }
                RequestSecurityTokenResponse rstr = new RequestSecurityTokenResponse(Settings.SecurityStandardsManager)
                {
                    Context = rst.Context,
                    IsRequestedTokenClosed = true
                };
                rstr.MakeReadOnly();
                BodyWriter bodyWriter = rstr;
                if (Settings.SecurityStandardsManager.MessageSecurityVersion.TrustVersion == TrustVersion.WSTrust13)
                {
                    List<RequestSecurityTokenResponse> rstrList = new List<RequestSecurityTokenResponse>(1)
                    {
                        rstr
                    };
                    RequestSecurityTokenResponseCollection rstrc = new RequestSecurityTokenResponseCollection(rstrList, Settings.SecurityStandardsManager);
                    bodyWriter = rstrc;
                }
                Message response = Message.CreateMessage(request.Version, ActionHeader.Create(Settings.SecurityStandardsManager.SecureConversationDriver.CloseResponseAction, request.Version.Addressing), bodyWriter);
                PrepareReply(request, response);
                return response;
            }

            internal Message CreateCloseResponse(Message message, SecurityProtocolCorrelationState correlationState, CancellationToken token)
            {
                using (message)
                {
                    Message response = ProcessCloseRequest(message);
                    response = _securityProtocol.SecureOutgoingMessage(response, token);
                    response.Properties.AllowOutputBatching = false;
                    return response;
                }
            }

            internal void TraceSessionClosedResponseSuccess()
            {
                // SecurityTraceRecordHelper.TraceSessionClosedResponseSent(this.currentSessionToken, GetLocalUri());
            }

            internal void TraceSessionClosedResponseFailure(Exception e)
            {
                // SecurityTraceRecordHelper.TraceSessionClosedResponseSendFailure(this.currentSessionToken, GetLocalUri(), e);
            }

            internal void TraceSessionClosedSuccess()
            {
                //  SecurityTraceRecordHelper.TraceSessionClosedSent(this.currentSessionToken, GetLocalUri());
            }

            internal void TraceSessionClosedFailure(Exception e)
            {
                //  SecurityTraceRecordHelper.TraceSessionCloseSendFailure(this.currentSessionToken, GetLocalUri(), e);
            }

            // SendCloseResponse closes the message and underlying context if the operation completes successfully
            protected void SendCloseResponse(RequestContext requestContext, Message closeResponse, CancellationToken token)
            {
                try
                {
                    using (closeResponse)
                    {
                        SendMessage(requestContext, closeResponse, token);
                    }

                    TraceSessionClosedResponseSuccess();
                }
                catch (CommunicationException e)
                {
                    TraceSessionClosedResponseFailure(e);
                }
                catch (TimeoutException e)
                {
                    TraceSessionClosedResponseFailure(e);
                }
            }

            internal Message CreateCloseMessage(CancellationToken token)
            {
                RequestSecurityToken rst = new RequestSecurityToken(Settings.SecurityStandardsManager)
                {
                    RequestType = Settings.SecurityStandardsManager.TrustDriver.RequestTypeClose,
                    CloseTarget = Settings.IssuedSecurityTokenParameters.CreateKeyIdentifierClause(_currentSessionToken, SecurityTokenReferenceStyle.External)
                };
                rst.MakeReadOnly();
                Message closeMessage = Message.CreateMessage(_messageVersion, ActionHeader.Create(Settings.SecurityStandardsManager.SecureConversationDriver.CloseAction, _messageVersion.Addressing), rst);
                RequestReplyCorrelator.PrepareRequest(closeMessage);
                if (LocalAddress != null)
                {
                    closeMessage.Headers.ReplyTo = LocalAddress;
                }
                else
                {
                    if (closeMessage.Version.Addressing == AddressingVersion.WSAddressing10)
                    {
                        closeMessage.Headers.ReplyTo = null;
                    }
                    else if (closeMessage.Version.Addressing == AddressingVersion.WSAddressingAugust2004)
                    {
                        closeMessage.Headers.ReplyTo = EndpointAddress.AnonymousAddress;
                    }
                    else
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            new ProtocolException(SR.Format(SR.AddressingVersionNotSupported, closeMessage.Version.Addressing)));
                    }
                }
                _securityProtocol.SecureOutgoingMessage(closeMessage, token);
                closeMessage.Properties.AllowOutputBatching = false;
                return closeMessage;
            }

            protected void SendClose(CancellationToken token)
            {
                try
                {
                    using (Message closeMessage = CreateCloseMessage(token))
                    {
                        SendMessage(null, closeMessage, token);
                    }
                    TraceSessionClosedSuccess();
                }
                catch (CommunicationException e)
                {
                    TraceSessionClosedFailure(e);
                }
                catch (TimeoutException e)
                {
                    TraceSessionClosedFailure(e);
                }
            }



            protected void SendMessage(RequestContext requestContext, Message message, CancellationToken token)
            {
                if (requestContext != null)
                {
                    requestContext.ReplyAsync(message, token);
                    requestContext.CloseAsync(token);
                }
            }

            internal void OnFaulted(Exception ex)
            {
                Settings.WrapperCommunicationObj.Fault(ex);
            }
        }

        private abstract class ServerSecuritySimplexSessionChannel : ServerSecuritySessionChannel
        {
            private readonly SoapSecurityInputSession _session;
            private bool _receivedClose;
            private bool _canSendCloseResponse;
            private bool _sentCloseResponse;
            private RequestContext _closeRequestContext;
            private Message _closeResponse;

            public ServerSecuritySimplexSessionChannel(
                SecuritySessionServerSettings settings,
                SecurityContextSecurityToken sessionToken,
                object listenerSecurityState, SecurityListenerSettingsLifetimeManager settingsLifetimeManager, EndpointAddress address)
                : base(settings, sessionToken, listenerSecurityState, settingsLifetimeManager, address)
            {
                _session = new SoapSecurityInputSession(sessionToken, settings, this);
            }

            public IInputSession Session => _session;

            private void CleanupPendingCloseState()
            {
                lock (LocalLock)
                {
                    if (_closeResponse != null)
                    {
                        _closeResponse.Close();
                        _closeResponse = null;
                    }
                    if (_closeRequestContext != null)
                    {
                        _closeRequestContext.Abort();
                        _closeRequestContext = null;
                    }
                }
            }

            protected override void AbortCore()
            {
                base.AbortCore();
                Settings.RemoveSessionChannel(_session.Id);
                CleanupPendingCloseState();
            }

            protected override void CloseCore(CancellationToken token)
            {
                base.CloseCore(token);
                Settings.RemoveSessionChannel(_session.Id);
            }

            public virtual Task CloseAsync(CancellationToken token)
            {
                return OnCloseAsync(token);
            }
            protected Task OnCloseAsync(CancellationToken token)
            {
                // send a close response if one was not sent yet
                bool wasAborted = SendCloseResponseOnCloseIfRequired(token);
                if (wasAborted)
                {
                    return Task.CompletedTask;
                }
                CloseCore(token);
                return Task.CompletedTask;
            }

            private bool ShouldSendCloseResponseOnClose(out RequestContext pendingCloseRequestContext, out Message pendingCloseResponse)
            {
                bool sendCloseResponse = false;
                lock (LocalLock)
                {
                    _canSendCloseResponse = true;
                    if (!_sentCloseResponse && _receivedClose && _closeResponse != null)
                    {
                        _sentCloseResponse = true;
                        sendCloseResponse = true;
                        pendingCloseRequestContext = _closeRequestContext;
                        pendingCloseResponse = _closeResponse;
                        _closeResponse = null;
                        _closeRequestContext = null;
                    }
                    else
                    {
                        _canSendCloseResponse = false;
                        pendingCloseRequestContext = null;
                        pendingCloseResponse = null;
                    }
                }
                return sendCloseResponse;
            }

            private bool SendCloseResponseOnCloseIfRequired(CancellationToken token)
            {
                bool aborted = false;
                bool sendCloseResponse = ShouldSendCloseResponseOnClose(out RequestContext pendingCloseRequestContext, out Message pendingCloseResponse);
                bool cleanupCloseState = true;
                if (sendCloseResponse)
                {
                    try
                    {
                        SendCloseResponse(pendingCloseRequestContext, pendingCloseResponse, token);
                        // this.inputSessionClosedHandle.Set();
                        cleanupCloseState = false;
                    }
                    catch (CommunicationObjectAbortedException)
                    {
                        if (State != CommunicationState.Closed)
                        {
                            throw;
                        }
                        aborted = true;
                    }
                    finally
                    {
                        if (cleanupCloseState)
                        {
                            if (pendingCloseResponse != null)
                            {
                                pendingCloseResponse.Close();
                            }
                            if (pendingCloseRequestContext != null)
                            {
                                pendingCloseRequestContext.Abort();
                            }
                        }
                    }
                }

                return aborted;
            }

            protected override void OnCloseResponseMessageReceived(RequestContext requestContext, Message message, SecurityProtocolCorrelationState correlationState, TimeSpan timeout)
            {
                // we dont expect a close-response for non-duplex security session
                message.Close();
                requestContext.Abort();
                Fault(new ProtocolException(SR.UnexpectedSecuritySessionCloseResponse));
            }

            private void Fault(ProtocolException protocolException)
            {
                AbortCore();
                OnFaulted(protocolException);
            }

            protected override void OnCloseMessageReceived(RequestContext requestContext, Message message, SecurityProtocolCorrelationState correlationState, CancellationToken token)
            {
                if (State == CommunicationState.Created)
                {
                    Fx.Assert("ServerSecuritySimplexSessionChannel.OnCloseMessageReceived (this.State == Created)");
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ServerReceivedCloseMessageStateIsCreated, GetType().ToString())));
                }

                if (SendCloseResponseOnCloseReceivedIfRequired(requestContext, message, correlationState, token))
                {
                    //  this.inputSessionClosedHandle.Set();
                }
            }

            private bool SendCloseResponseOnCloseReceivedIfRequired(RequestContext requestContext, Message message, SecurityProtocolCorrelationState correlationState, CancellationToken token)
            {
                bool sendCloseResponse = false;
                //  ServiceModelActivity activity = DiagnosticUtility.ShouldUseActivity ? TraceUtility.ExtractActivity(message) : null;
                bool cleanupContext = true;
                try
                {
                    Message localCloseResponse = null;
                    lock (LocalLock)
                    {
                        if (!_receivedClose)
                        {
                            _receivedClose = true;
                            localCloseResponse = CreateCloseResponse(message, correlationState, token);
                            if (_canSendCloseResponse)
                            {
                                _sentCloseResponse = true;
                                sendCloseResponse = true;
                            }
                            else
                            {
                                // save the close requestContext to reply later
                                _closeRequestContext = requestContext;
                                _closeResponse = localCloseResponse;
                                cleanupContext = false;
                            }
                        }
                    }
                    if (sendCloseResponse)
                    {
                        SendCloseResponse(requestContext, localCloseResponse, token);
                        cleanupContext = false;
                    }
                    else if (cleanupContext)
                    {
                        requestContext.CloseAsync(token);
                        cleanupContext = false;
                    }
                    return sendCloseResponse;
                }
                finally
                {
                    message.Close();
                    if (cleanupContext)
                    {
                        requestContext.Abort();
                    }
                    //if (DiagnosticUtility.ShouldUseActivity && (activity != null))
                    //{
                    //    activity.Stop();
                    //}
                }
            }

            protected class SoapSecurityInputSession : ISecureConversationSession, IInputSession
            {
                private readonly ServerSecuritySessionChannel _channel;
                private readonly UniqueId _securityContextTokenId;
                private readonly SecurityKeyIdentifierClause _sessionTokenIdentifier;
                private readonly SecurityStandardsManager _standardsManager;

                public SoapSecurityInputSession(SecurityContextSecurityToken sessionToken,
                    SecuritySessionServerSettings settings, ServerSecuritySessionChannel channel)
                {
                    _channel = channel;
                    _securityContextTokenId = sessionToken.ContextId;
                    Claim identityClaim = SecurityUtils.GetPrimaryIdentityClaim(sessionToken.AuthorizationPolicies);
                    if (identityClaim != null)
                    {
                        RemoteIdentity = EndpointIdentity.CreateIdentity(identityClaim);
                    }
                    _sessionTokenIdentifier = settings.IssuedSecurityTokenParameters.CreateKeyIdentifierClause(sessionToken, SecurityTokenReferenceStyle.External);
                    _standardsManager = settings.SessionProtocolFactory.StandardsManager;
                }

                public string Id => _securityContextTokenId.ToString();

                public EndpointIdentity RemoteIdentity { get; }

                public void WriteSessionTokenIdentifier(XmlDictionaryWriter writer)
                {
                    _standardsManager.SecurityTokenSerializer.WriteKeyIdentifierClause(writer, _sessionTokenIdentifier);
                }

                public bool TryReadSessionTokenIdentifier(XmlReader reader)
                {
                    if (!_standardsManager.SecurityTokenSerializer.CanReadKeyIdentifierClause(reader))
                    {
                        return false;
                    }
                    return _standardsManager.SecurityTokenSerializer.ReadKeyIdentifierClause(reader) is SecurityContextKeyIdentifierClause incomingTokenIdentifier && incomingTokenIdentifier.Matches(_securityContextTokenId, null);
                }
            }

            //Renamed SecurityReplySessionChannel => SecurityReplySessionServiceChannelDispatcher (implementd IServiceChannelDispatcher)
            public class SecurityReplySessionServiceChannelDispatcher : ServerSecuritySimplexSessionChannel, IServiceChannelDispatcher, IReplySessionChannel
            {
                private readonly IServiceProvider _serviceProvider;
                private volatile IServiceChannelDispatcher _channelDispatcher;
                public SecurityReplySessionServiceChannelDispatcher(
                    SecuritySessionServerSettings settings,
                    SecurityContextSecurityToken sessionToken,
                    object listenerSecurityState, SecurityListenerSettingsLifetimeManager settingsLifetimeManager
                    , IChannel channel, EndpointAddress address)
                    : base(settings,
                          sessionToken, listenerSecurityState, settingsLifetimeManager, address)
                {
                    IncomingChannel = (IReplyChannel)channel;
                    // this.defaultBinder = new ReplyChannelBinder();
                    // this.idleManager = new ServiceChannel.SessionIdleManager();
                    _serviceProvider = IncomingChannel.GetProperty<IServiceScopeFactory>().CreateScope().ServiceProvider;
                }

                public IReplyChannel IncomingChannel { get; set; }

                public IServiceChannelDispatcher ChannelDispatcher { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

                protected override bool CanDoSecurityCorrelation => true;

#pragma warning disable CS0067 // The event is never used - see issue #287
                public event EventHandler Closed;
                public event EventHandler Closing;
                public event EventHandler Faulted;
                public event EventHandler Opened;
                public event EventHandler Opening;
#pragma warning restore CS0067 // The event is never used

                public void Abort()
                {
                    base.AbortCore();
                }

                public Task CloseAsync()
                {
                    TimeoutHelper helper = new TimeoutHelper(ServiceDefaults.CloseTimeout);
                    return CloseAsync(helper.GetCancellationToken());
                }

                public override Task CloseAsync(CancellationToken token)
                {
                    base.CloseAsync(token);
                    return Task.CompletedTask;
                }

                public Task DispatchAsync(RequestContext context)
                {
                    RequestContext securityRequestContext = ReceiveRequest(context);
                    return _channelDispatcher.DispatchAsync(securityRequestContext);
                }

                public Task DispatchAsync(Message message)
                {
                    throw new NotImplementedException();
                }

                public T GetProperty<T>() where T : class
                {
                    T tObj = _serviceProvider.GetService<T>();
                    if (tObj == null)
                    {
                        return IncomingChannel.GetProperty<T>();
                    }
                    else
                    {
                        return tObj;
                    }
                }

                public Task OpenAsync()
                {
                    return Task.CompletedTask;
                }

                public override async Task OpenAsync(TimeSpan timeout)
                {
                    await base.OpenAsync(timeout);
                    _channelDispatcher = await Settings.SecurityServiceDispatcher.
                        GetInnerServiceChannelDispatcher(this);
                }

                public Task OpenAsync(CancellationToken token)
                {
                    return Task.CompletedTask;
                }
            }
        }

        private class SecuritySessionRequestContext : RequestContextBase
        {
            private readonly RequestContext _requestContext;
            private readonly ServerSecuritySessionChannel _channel;
            private readonly SecurityProtocolCorrelationState _correlationState;

            public SecuritySessionRequestContext(RequestContext requestContext, Message requestMessage, SecurityProtocolCorrelationState correlationState, ServerSecuritySessionChannel channel)
                : base(requestMessage, ServiceDefaults.CloseTimeout, ServiceDefaults.SendTimeout)
            {
                _requestContext = requestContext;
                _correlationState = correlationState;
                _channel = channel;
            }

            protected override void OnAbort()
            {
                _requestContext.Abort();
            }

            protected override Task OnCloseAsync(CancellationToken token)
            {
                return _requestContext.CloseAsync(token);
            }

            protected override Task OnReplyAsync(Message message, CancellationToken token)
            {
                if (message != null)
                {
                    _channel.SecureApplicationMessage(ref message, _correlationState, token);
                    //this.securityProtocol.SecureOutgoingMessageAsync(message);
                    return _requestContext.ReplyAsync(message);
                }
                else
                {
                    return Task.CompletedTask;
                }
            }
        }

        //Failure Demuxer handler
        internal class SecuritySessionDemuxFailureHandler : IChannelDemuxFailureHandler
        {
            private readonly SecurityStandardsManager _standardsManager;

            public SecuritySessionDemuxFailureHandler(SecurityStandardsManager standardsManager)
            {
                if (standardsManager == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(standardsManager));
                }
                this._standardsManager = standardsManager;
            }

            public void HandleDemuxFailure(Message message)
            {
                if (message == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
                }
            }

            public Message CreateSessionDemuxFaultMessage(Message message)
            {
                MessageFault fault = SecurityUtils.CreateSecurityContextNotFoundFault(this._standardsManager, message.Headers.Action);
                Message faultMessage = Message.CreateMessage(message.Version, fault, message.Version.Addressing.DefaultFaultAction);
                if (message.Headers.MessageId != null)
                {
                    faultMessage.InitializeReply(message);
                }
                return faultMessage;
            }
           
            public Task HandleDemuxFailureAsync(Message message)
            {
                throw new NotImplementedException();
            }

            public Task HandleDemuxFailureAsync(Message message, RequestContext faultContext)
            {
                this.HandleDemuxFailure(message);
                Message faultMessage = CreateSessionDemuxFaultMessage(message);
                try
                {
                    faultContext.ReplyAsync(faultMessage);
                }
                catch (Exception ex)
                {
                    if (Fx.IsFatal(ex))
                    {
                        throw;
                    }
                }
                finally
                {
                    faultMessage.Close();
                }
                return Task.CompletedTask;
            }
        }
    }
}
