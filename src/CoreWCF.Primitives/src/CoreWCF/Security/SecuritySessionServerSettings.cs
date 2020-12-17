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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace CoreWCF.Security
{
    internal sealed class SecuritySessionServerSettings : IServiceDispatcherSecureConversationSessionSettings, ISecurityCommunicationObject
    {
        internal static readonly TimeSpan defaultKeyRenewalInterval = TimeSpan.FromHours(15);
        internal static readonly TimeSpan defaultKeyRolloverInterval = TimeSpan.FromMinutes(5);
        internal const bool defaultTolerateTransportFailures = true;
        internal const int defaultMaximumPendingSessions = 128;
        internal static readonly TimeSpan defaultInactivityTimeout = TimeSpan.FromMinutes(2);
        private int maximumPendingSessions;
        private Dictionary<UniqueId, SecurityContextSecurityToken> pendingSessions1;
        private Dictionary<UniqueId, SecurityContextSecurityToken> pendingSessions2;
        private Dictionary<UniqueId, MessageFilter> sessionFilters;
        private IOThreadTimer inactivityTimer;
        private TimeSpan inactivityTimeout;
        private bool tolerateTransportFailures;
        private TimeSpan maximumKeyRenewalInterval;
        private TimeSpan keyRolloverInterval;
        private int maximumPendingKeysPerSession;
        private SecurityProtocolFactory sessionProtocolFactory;
        private Dictionary<UniqueId, IServerSecuritySessionChannel> activeSessions;
        private SecurityServiceDispatcher securityServiceDispatcher;
        private ChannelBuilder channelBuilder;
        private SecurityStandardsManager standardsManager;
        private SecurityTokenParameters issuedTokenParameters;
        private SecurityTokenResolver sessionTokenResolver;
        private bool acceptNewWork;
        private Uri listenUri;
        private SecurityListenerSettingsLifetimeManager settingsLifetimeManager;
        private Type acceptorChannelType;

        public SecuritySessionServerSettings()
        {
            activeSessions = new Dictionary<UniqueId, IServerSecuritySessionChannel>();
            this.maximumKeyRenewalInterval = defaultKeyRenewalInterval;
            this.maximumPendingKeysPerSession = 5;
            this.keyRolloverInterval = defaultKeyRolloverInterval;
            this.inactivityTimeout = defaultInactivityTimeout;
            this.tolerateTransportFailures = defaultTolerateTransportFailures;
            this.maximumPendingSessions = defaultMaximumPendingSessions;
            this.WrapperCommunicationObj = new WrapperSecurityCommunicationObject(this);
        }

        internal ChannelBuilder ChannelBuilder
        {
            get
            {
                return this.channelBuilder;
            }
            set
            {
                this.WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                this.channelBuilder = value;
            }
        }

        internal WrapperSecurityCommunicationObject WrapperCommunicationObj { get; }

        internal SecurityListenerSettingsLifetimeManager SettingsLifetimeManager
        {
            get
            {
                return this.settingsLifetimeManager;
            }
            set
            {
                this.WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                this.settingsLifetimeManager = value;
            }
        }

        internal SecurityServiceDispatcher SecurityServiceDispatcher
        {
            get
            {
                return this.securityServiceDispatcher;
            }
            set
            {
                this.WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                this.securityServiceDispatcher = value;
            }
        }

        /// <summary>
        /// AcceptorChannelType will help determine if it's a duplex or simple reply channel
        /// </summary>
        internal Type AcceptorChannelType
        {
            get
            {
                return this.acceptorChannelType;
            }
            set
            {
                this.acceptorChannelType = value;
            }
        }

        private Uri Uri
        {
            get
            {
                this.WrapperCommunicationObj.ThrowIfNotOpened();
                return this.listenUri;
            }
        }

        internal object ThisGlobalLock { get; } = new object();

        public SecurityTokenAuthenticator SessionTokenAuthenticator { get; private set; }

        public ISecurityContextSecurityTokenCache SessionTokenCache { get; private set; }

        public SecurityTokenResolver SessionTokenResolver => this.sessionTokenResolver;

        public SecurityTokenParameters IssuedSecurityTokenParameters
        {
            get
            {
                return this.issuedTokenParameters;
            }
            set
            {
                this.WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                this.issuedTokenParameters = value;
            }
        }

        internal SecurityStandardsManager SecurityStandardsManager
        {
            get
            {
                return this.standardsManager;
            }
            set
            {
                this.WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                this.standardsManager = value;
            }
        }

        public bool TolerateTransportFailures
        {
            get
            {
                return this.tolerateTransportFailures;
            }
            set
            {
                this.WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                this.tolerateTransportFailures = value;
            }
        }

        public bool CanRenewSession { get; set; } = true;

        public int MaximumPendingSessions
        {
            get
            {
                return this.maximumPendingSessions;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                this.WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                this.maximumPendingSessions = value;
            }
        }

        public TimeSpan InactivityTimeout
        {
            get
            {
                return this.inactivityTimeout;
            }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }

                this.WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                this.inactivityTimeout = value;
            }
        }

        public TimeSpan MaximumKeyRenewalInterval
        {
            get
            {
                return this.maximumKeyRenewalInterval;
            }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }
                this.WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                this.maximumKeyRenewalInterval = value;
            }
        }

        public TimeSpan KeyRolloverInterval
        {
            get
            {
                return this.keyRolloverInterval;
            }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }
                this.WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                this.keyRolloverInterval = value;
            }
        }

        public int MaximumPendingKeysPerSession
        {
            get
            {
                return this.maximumPendingKeysPerSession;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.ValueMustBeGreaterThanZero));
                }
                this.WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                this.maximumPendingKeysPerSession = value;
            }
        }

        public SecurityProtocolFactory SessionProtocolFactory
        {
            get
            {
                return this.sessionProtocolFactory;
            }
            set
            {
                this.WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                this.sessionProtocolFactory = value;
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
            this.AbortPendingChannels(timeoutHelper.GetCancellationToken());
            this.OnAbortCore();
        }

        internal void Abort()
        {
            this.WrapperCommunicationObj.Abort();
        }

        private void OnCloseCore(TimeSpan timeout)
        {
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            this.ClearPendingSessions();
            this.ClosePendingChannels(timeoutHelper.GetCancellationToken());
            if (this.inactivityTimer != null)
            {
                this.inactivityTimer.Cancel();
            }
            if (this.sessionProtocolFactory != null)
            {
                this.sessionProtocolFactory.OnCloseAsync(timeoutHelper.RemainingTime());
            }
            if (this.SessionTokenAuthenticator != null)
            {
                SecurityUtils.CloseTokenAuthenticatorIfRequiredAsync(this.SessionTokenAuthenticator, timeoutHelper.GetCancellationToken());
            }
        }

        private void OnAbortCore()
        {
            if (this.inactivityTimer != null)
            {
                this.inactivityTimer.Cancel();
            }
            if (this.sessionProtocolFactory != null)
            {
                this.sessionProtocolFactory.OnCloseAsync(TimeSpan.Zero);
            }
            if (this.SessionTokenAuthenticator != null)
            {
                SecurityUtils.AbortTokenAuthenticatorIfRequired(this.SessionTokenAuthenticator);
            }
        }

        private Task SetupSessionTokenAuthenticatorAsync()
        {
            RecipientServiceModelSecurityTokenRequirement requirement = new RecipientServiceModelSecurityTokenRequirement();
            this.issuedTokenParameters.InitializeSecurityTokenRequirement(requirement);
            requirement.KeyUsage = SecurityKeyUsage.Signature;
            requirement.ListenUri = this.listenUri;
            requirement.SecurityBindingElement = this.sessionProtocolFactory.SecurityBindingElement;
            requirement.SecurityAlgorithmSuite = this.sessionProtocolFactory.IncomingAlgorithmSuite;
            requirement.SupportSecurityContextCancellation = true;
            requirement.MessageSecurityVersion = sessionProtocolFactory.MessageSecurityVersion.SecurityTokenVersion;
            // requirement.AuditLogLocation = sessionProtocolFactory.AuditLogLocation;
            // requirement.SuppressAuditFailure = sessionProtocolFactory.SuppressAuditFailure;
            // requirement.MessageAuthenticationAuditLevel = sessionProtocolFactory.MessageAuthenticationAuditLevel;
            requirement.Properties[ServiceModelSecurityTokenRequirement.MessageDirectionProperty] = MessageDirection.Input;
            if (sessionProtocolFactory.EndpointFilterTable != null)
            {
                requirement.Properties[ServiceModelSecurityTokenRequirement.EndpointFilterTableProperty] = sessionProtocolFactory.EndpointFilterTable;
            }
            this.SessionTokenAuthenticator = this.sessionProtocolFactory.SecurityTokenManager.CreateSecurityTokenAuthenticator(requirement, out this.sessionTokenResolver);
            if (!(this.SessionTokenAuthenticator is IIssuanceSecurityTokenAuthenticator))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecuritySessionRequiresIssuanceAuthenticator, typeof(IIssuanceSecurityTokenAuthenticator), this.SessionTokenAuthenticator.GetType())));
            }
            if (sessionTokenResolver == null || (!(sessionTokenResolver is ISecurityContextSecurityTokenCache)))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecuritySessionRequiresSecurityContextTokenCache, this.sessionTokenResolver.GetType(), typeof(ISecurityContextSecurityTokenCache))));
            }
            this.SessionTokenCache = (ISecurityContextSecurityTokenCache)this.sessionTokenResolver;
            return Task.CompletedTask;
        }

        public void StopAcceptingNewWork()
        {
            this.acceptNewWork = false;
        }

        private int GetPendingSessionCount()
        {
            return this.pendingSessions1.Count + this.pendingSessions2.Count;
        }

        private void AbortPendingChannels(CancellationToken token)
        {
            ClosePendingChannels(token);
        }

        private async void ClosePendingChannels(CancellationToken token)
        {
            var tasks = new Task[this.activeSessions.Count];
            lock (ThisGlobalLock)
            {
                int index = 0;
                if (typeof(IReplyChannel).Equals(this.AcceptorChannelType))
                {
                    foreach (ServerSecuritySimplexSessionChannel securitySessionSimplexChannel in this.activeSessions.Values)
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
            if (this.sessionProtocolFactory is SessionSymmetricTransportSecurityProtocolFactory)
            {
                SessionSymmetricTransportSecurityProtocolFactory transportPf = (SessionSymmetricTransportSecurityProtocolFactory)this.sessionProtocolFactory;
                transportPf.AddTimestamp = true;
                transportPf.SecurityTokenParameters = this.IssuedSecurityTokenParameters;
                transportPf.SecurityTokenParameters.RequireDerivedKeys = false;
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
            }
        }

        private void OnTokenRenewed(SecurityToken newToken, SecurityToken oldToken)
        {
            this.WrapperCommunicationObj.ThrowIfClosed();
            if (!this.acceptNewWork)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new EndpointNotFoundException(SR.SecurityListenerClosing));
            }
            SecurityContextSecurityToken newSecurityContextToken = newToken as SecurityContextSecurityToken;
            if (newSecurityContextToken == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.SessionTokenIsNotSecurityContextToken, newToken.GetType(), typeof(SecurityContextSecurityToken))));
            }
            SecurityContextSecurityToken oldSecurityContextToken = oldToken as SecurityContextSecurityToken;
            if (oldSecurityContextToken == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.SessionTokenIsNotSecurityContextToken, oldToken.GetType(), typeof(SecurityContextSecurityToken))));
            }
            IServerSecuritySessionChannel sessionChannel = this.FindSessionChannel(newSecurityContextToken.ContextId);
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
                MessageFilter sctFilter = new SecuritySessionFilter(sessionToken.ContextId, this.sessionProtocolFactory.StandardsManager, (this.sessionProtocolFactory.SecurityHeaderLayout == SecurityHeaderLayout.Strict), this.SecurityStandardsManager.SecureConversationDriver.RenewAction.Value, this.SecurityStandardsManager.SecureConversationDriver.RenewResponseAction.Value);
                SessionInitiationMessageServiceDispatcher sessionServiceDispatcher
                 = new SessionInitiationMessageServiceDispatcher(this, sessionToken, sctFilter, remoteAddress);
                //logic to separate for Duplex
                if (typeof(IReplyChannel).Equals(this.AcceptorChannelType))
                    this.ChannelBuilder.AddServiceDispatcher<IReplyChannel>(sessionServiceDispatcher, new ChannelDemuxerFilter(sctFilter, Int32.MaxValue));
                this.AddPendingSession(sessionToken.ContextId, sessionToken, sctFilter);
            }
        }

        private void OnTokenIssued(SecurityToken issuedToken, EndpointAddress tokenRequestor)
        {
            this.WrapperCommunicationObj.ThrowIfClosed(); //TODO mark open
            if (!this.acceptNewWork)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new EndpointNotFoundException(SR.SecurityListenerClosing));
            }
            SecurityContextSecurityToken issuedSecurityContextToken = issuedToken as SecurityContextSecurityToken;
            if (issuedSecurityContextToken == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.SessionTokenIsNotSecurityContextToken, issuedToken.GetType(), typeof(SecurityContextSecurityToken))));
            }
            // IServerReliableChannelBinder channelBinder = CreateChannelBinder(issuedSecurityContextToken, tokenRequestor ?? EndpointAddress.AnonymousAddress);
            CreateSessionMessageServiceDispatcher(issuedSecurityContextToken, tokenRequestor ?? EndpointAddress.AnonymousAddress);
        }

        internal SecurityContextSecurityToken GetSecurityContextSecurityToken(UniqueId sessionId)
        {
            if (this.pendingSessions1 != null && this.pendingSessions1.ContainsKey(sessionId))
                return this.pendingSessions1[sessionId];
            if (this.pendingSessions2 != null && this.pendingSessions2.ContainsKey(sessionId))
                return this.pendingSessions2[sessionId];
            return null;
        }

        private void OnTimer(object state)
        {
            if (this.WrapperCommunicationObj.State == CommunicationState.Closed
                || this.WrapperCommunicationObj.State == CommunicationState.Faulted)
            {
                return;
            }
            try
            {
                this.ClearPendingSessions();
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
                if (this.WrapperCommunicationObj.State != CommunicationState.Closed
                    && this.WrapperCommunicationObj.State != CommunicationState.Closing
                    && this.WrapperCommunicationObj.State != CommunicationState.Faulted)
                {
                    this.inactivityTimer.Set(this.inactivityTimeout);
                }
            }
        }

        private void AddPendingSession(UniqueId sessionId, SecurityContextSecurityToken securityToken, MessageFilter filter)
        {
            lock (ThisGlobalLock)
            {
                if ((GetPendingSessionCount() + 1) > this.MaximumPendingSessions)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new QuotaExceededException(SR.SecuritySessionLimitReached));
                }
                if (this.pendingSessions1.ContainsKey(sessionId) || this.pendingSessions2.ContainsKey(sessionId))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.SecuritySessionAlreadyPending, sessionId)));
                }
                this.pendingSessions1.Add(sessionId, securityToken);
                this.sessionFilters.Add(sessionId, filter);
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
                if (this.pendingSessions1.Count == 0 && this.pendingSessions2.Count == 0)
                {
                    return;
                }
                foreach (UniqueId sessionId in this.pendingSessions2.Keys)
                {
                    SecurityContextSecurityToken token = this.pendingSessions2[sessionId];
                    try
                    {
                        //TryCloseBinder(channelBinder, this.CloseTimeout); // Replacing this line with below (being proactive rather reactive(in WCF))
                        this.ChannelBuilder.RemoveServiceDispatcher<IReplyChannel>(sessionFilters[sessionId]);
                        this.SessionTokenCache.RemoveAllContexts(sessionId);
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
                this.pendingSessions2.Clear();
                Dictionary<UniqueId, SecurityContextSecurityToken> temp = this.pendingSessions2;
                this.pendingSessions2 = this.pendingSessions1;
                this.pendingSessions1 = temp;
            }
        }

        internal bool RemovePendingSession(UniqueId sessionId)
        {
            bool result;
            lock (ThisGlobalLock)
            {
                if (this.pendingSessions1.ContainsKey(sessionId))
                {
                    this.pendingSessions1.Remove(sessionId);
                    result = true;
                }
                else if (pendingSessions2.ContainsKey(sessionId))
                {
                    this.pendingSessions2.Remove(sessionId);
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
                this.activeSessions.TryGetValue(sessionId, out result);
            }
            return result;
        }

        private void AddSessionChannel(UniqueId sessionId, IServerSecuritySessionChannel channel, MessageFilter filter)
        {
            lock (ThisGlobalLock)
            {
                this.activeSessions.Add(sessionId, channel);
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
                this.ChannelBuilder.RemoveServiceDispatcher<IReplyChannel>(sessionFilters[sessionId]);
                this.activeSessions.Remove(sessionId);
                sessionFilters.Remove(sessionId);
            }
            //SecurityTraceRecordHelper.TraceActiveSessionRemoved(sessionId, this.Uri);
        }

        public Task CloseAsync(TimeSpan timeout)
        {
            return this.WrapperCommunicationObj.CloseAsync();
        }
        public Task OnCloseAsync(TimeSpan timeout)
        {
            OnCloseCore(timeout);
            return Task.CompletedTask;
        }

        public Task OpenAsync(TimeSpan timeout)
        {
            return this.WrapperCommunicationObj.OpenAsync();
        }
        public Task OnOpenAsync(TimeSpan timeout)
        {
            if (this.sessionProtocolFactory == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SecuritySessionProtocolFactoryShouldBeSetBeforeThisOperation));
            }
            if (this.standardsManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecurityStandardsManagerNotSet, this.GetType())));
            }
            if (this.issuedTokenParameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.IssuedSecurityTokenParametersNotSet, this.GetType())));
            }
            if (this.maximumKeyRenewalInterval < this.keyRolloverInterval)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.KeyRolloverGreaterThanKeyRenewal));
            }
            if (this.securityServiceDispatcher == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecurityChannelListenerNotSet, this.GetType())));
            }
            if (this.settingsLifetimeManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecuritySettingsLifetimeManagerNotSet, this.GetType())));
            }
            this.MessageVersion = this.channelBuilder.Binding.MessageVersion;
            this.listenUri = this.securityServiceDispatcher.BaseAddress;
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            this.pendingSessions1 = new Dictionary<UniqueId, SecurityContextSecurityToken>();
            this.pendingSessions2 = new Dictionary<UniqueId, SecurityContextSecurityToken>();
            this.sessionFilters = new Dictionary<UniqueId, MessageFilter>();
            if (this.inactivityTimeout < TimeSpan.MaxValue)
            {
                this.inactivityTimer = new IOThreadTimer(new Action<object>(this.OnTimer), this, false);
                this.inactivityTimer.Set(this.inactivityTimeout);
            }
            this.ConfigureSessionSecurityProtocolFactory();
            this.sessionProtocolFactory.OpenAsync(timeoutHelper.RemainingTime());
            SetupSessionTokenAuthenticatorAsync();
            ((IIssuanceSecurityTokenAuthenticator)this.SessionTokenAuthenticator).IssuedSecurityTokenHandler = this.OnTokenIssued;
            ((IIssuanceSecurityTokenAuthenticator)this.SessionTokenAuthenticator).RenewedSecurityTokenHandler = this.OnTokenRenewed;
            if (this.SessionTokenAuthenticator is SecuritySessionSecurityTokenAuthenticator)
            {
                ((SecuritySessionSecurityTokenAuthenticator)this.SessionTokenAuthenticator).
                    SecurityServiceDispatcher = this.SecurityServiceDispatcher;
            }
            this.acceptNewWork = true;
            SecurityUtils.OpenTokenAuthenticatorIfRequiredAsync(this.SessionTokenAuthenticator, timeoutHelper.GetCancellationToken());
            SecuritySessionSecurityTokenAuthenticator securityAuth = (SecuritySessionSecurityTokenAuthenticator)this.SessionTokenAuthenticator;
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
            private SecuritySessionServerSettings settings;
            private SecurityContextSecurityToken sessionToken;
            private volatile IServiceChannelDispatcher sessionChannelDispatcher;
            private MessageFilter messageFilter;
            private EndpointAddress remoteAddress;

            public SessionInitiationMessageServiceDispatcher(/*IServerReliableChannelBinder channelBinder,*/ SecuritySessionServerSettings settings, SecurityContextSecurityToken sessionToken, MessageFilter filter, EndpointAddress address)
            {
                this.settings = settings;
                this.sessionToken = sessionToken;
                this.messageFilter = filter;
                this.remoteAddress = address;
            }

            public Uri BaseAddress => throw new NotImplementedException();

            public Binding Binding => throw new NotImplementedException();

            public ServiceHostBase Host => throw new NotImplementedException();

            private object Lock { get; } = new object();

            public IList<Type> SupportedChannelTypes => throw new NotImplementedException();

            /// <summary>
            /// ProcessMessage equivalent in WCF 
            /// </summary>
            /// <returns></returns>
            public async Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel channel)
            {
                if (sessionChannelDispatcher == null)
                {
                    lock (this.Lock)
                    {
                        if (sessionChannelDispatcher == null)
                        {
                            if (!this.settings.RemovePendingSession(this.sessionToken.ContextId))
                            {
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new CommunicationException(SR.Format(SR.SecuritySessionNotPending, this.sessionToken.ContextId)));
                            }
                            if (this.settings.AcceptorChannelType is IDuplexChannel)
                            {
                                throw new PlatformNotSupportedException();
                            }
                            else
                            {
                                ServerSecuritySimplexSessionChannel.SecurityReplySessionServiceChannelDispatcher
                                    replySessionChannelDispatcher = new ServerSecuritySimplexSessionChannel.
                                    SecurityReplySessionServiceChannelDispatcher(this.settings, this.sessionToken,
                                    null, this.settings.SettingsLifetimeManager, channel, this.remoteAddress);
                                Task openTask = replySessionChannelDispatcher.OpenAsync(ServiceDefaults.OpenTimeout);
                                openTask.GetAwaiter().GetResult();
                                sessionChannelDispatcher = replySessionChannelDispatcher;
                                settings.AddSessionChannel(this.sessionToken.ContextId, replySessionChannelDispatcher, this.messageFilter);
                            }
                        }
                    }
                }
                return sessionChannelDispatcher;
            }

        }

        private interface IServerSecuritySessionChannel
        {
            void RenewSessionToken(SecurityContextSecurityToken newToken, SecurityContextSecurityToken supportingToken);
        }

        private abstract class ServerSecuritySessionChannel : /*ChannelBase,*/ IServerSecuritySessionChannel
        {
            private FaultCode renewFaultCode;
            private FaultReason renewFaultReason;
            private FaultCode sessionAbortedFaultCode;
            private FaultReason sessionAbortedFaultReason;

            // Double-checked locking pattern requires volatile for read/write synchronization
            private bool areFaultCodesInitialized;
            private IServerReliableChannelBinder channelBinder;
            private SecurityProtocol securityProtocol;

            // This is used to sign outgoing messages
            private SecurityContextSecurityToken currentSessionToken;
            private UniqueId sessionId;

            // These are renewed tokens that have not been used as yet
            private List<SecurityContextSecurityToken> futureSessionTokens;
            private RequestContext initialRequestContext;
            private bool isInputClosed;
            private MessageVersion messageVersion;
            private SecurityListenerSettingsLifetimeManager settingsLifetimeManager;
            private bool hasSecurityStateReference;

            protected ServerSecuritySessionChannel(SecuritySessionServerSettings settings,
                SecurityContextSecurityToken sessionToken,
                object listenerSecurityProtocolState,
                SecurityListenerSettingsLifetimeManager settingsLifetimeManager, EndpointAddress address)
            {
                this.Settings = settings;
                this.messageVersion = settings.MessageVersion;
                // this.channelBinder.Faulted += this.OnInnerFaulted;
                this.securityProtocol = this.Settings.SessionProtocolFactory.CreateSecurityProtocol(null, null, true, TimeSpan.Zero);
                if (!(this.securityProtocol is IAcceptorSecuritySessionProtocol))
                {
                    Fx.Assert("Security protocol must be IAcceptorSecuritySessionProtocol.");
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ProtocolMisMatch, "IAcceptorSecuritySessionProtocol", this.GetType().ToString())));
                }
                this.currentSessionToken = sessionToken;
                this.sessionId = sessionToken.ContextId;
                this.futureSessionTokens = new List<SecurityContextSecurityToken>(1);
                ((IAcceptorSecuritySessionProtocol)this.securityProtocol).SetOutgoingSessionToken(sessionToken);
                ((IAcceptorSecuritySessionProtocol)this.securityProtocol).SetSessionTokenAuthenticator(this.sessionId, this.Settings.SessionTokenAuthenticator, this.Settings.SessionTokenResolver);
                this.settingsLifetimeManager = settingsLifetimeManager;
                this.LocalAddress = address;
                this.LocalLock = new object();
            }

            protected SecuritySessionServerSettings Settings { get; }

            protected virtual bool CanDoSecurityCorrelation => false;

            internal TimeSpan InternalSendTimeout => ServiceDefaults.SendTimeout;

            public EndpointAddress LocalAddress { get; }

            public object LocalLock { get; }

            public CommunicationState State => this.Settings.WrapperCommunicationObj.State;

            public virtual Task OpenAsync(TimeSpan timeout)
            {
                this.securityProtocol.OpenAsync(timeout);
                if (this.CanDoSecurityCorrelation)
                {
                    ((IAcceptorSecuritySessionProtocol)this.securityProtocol).ReturnCorrelationState = true;

                } // if an abort happened concurrently with the open, then return
                if (this.State == CommunicationState.Closed || this.State == CommunicationState.Closing)
                {
                    return Task.CompletedTask;
                }
                this.settingsLifetimeManager.AddReference();
                this.hasSecurityStateReference = true;
                return Task.CompletedTask;
            }

            protected virtual void AbortCore()
            {
                if (this.securityProtocol != null)
                {
                    TimeoutHelper timeout = new TimeoutHelper(ServiceDefaults.CloseTimeout);
                    this.securityProtocol.CloseAsync(true, timeout.RemainingTime());
                }
                this.Settings.SessionTokenCache.RemoveAllContexts(this.currentSessionToken.ContextId);
                bool abortLifetimeManager = false;
                lock (this.LocalLock)
                {
                    if (hasSecurityStateReference)
                    {
                        abortLifetimeManager = true;
                        hasSecurityStateReference = false;
                    }
                }
                if (abortLifetimeManager)
                {
                    this.settingsLifetimeManager.Abort();
                }
            }

            protected virtual void CloseCore(CancellationToken token)
            {
                try
                {
                    TimeoutHelper helper = new TimeoutHelper(ServiceDefaults.CloseTimeout);
                    if (this.securityProtocol != null)
                    {
                        this.securityProtocol.CloseAsync(false, helper.RemainingTime()); ;
                    }
                    bool closeLifetimeManager = false;
                    lock (this.LocalLock)
                    {
                        if (hasSecurityStateReference)
                        {
                            closeLifetimeManager = true;
                            hasSecurityStateReference = false;
                        }
                    }
                    if (closeLifetimeManager)
                    {
                        this.settingsLifetimeManager.CloseAsync(helper.RemainingTime());
                    }
                }
                catch (CommunicationObjectAbortedException)
                {
                    if (this.State != CommunicationState.Closed)
                    {
                        throw;
                    }
                    // a parallel thread aborted the channel. Ignore the exception
                }
                this.Settings.SessionTokenCache.RemoveAllContexts(this.currentSessionToken.ContextId);
            }

            protected abstract void OnCloseMessageReceived(RequestContext requestContext, Message message, SecurityProtocolCorrelationState correlationState, CancellationToken token);

            protected abstract void OnCloseResponseMessageReceived(RequestContext requestContext, Message message, SecurityProtocolCorrelationState correlationState, TimeSpan timeout);

            public void RenewSessionToken(SecurityContextSecurityToken newToken, SecurityContextSecurityToken supportingToken)
            {
                ThrowIfClosedOrNotOpen();
                // enforce that the token being renewed is the current session token
                lock (this.LocalLock)
                {
                    if (supportingToken.ContextId != this.currentSessionToken.ContextId || supportingToken.KeyGeneration != this.currentSessionToken.KeyGeneration)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.CurrentSessionTokenNotRenewed, supportingToken.KeyGeneration, this.currentSessionToken.KeyGeneration)));
                    }
                    if (this.futureSessionTokens.Count == this.Settings.MaximumPendingKeysPerSession)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.TooManyPendingSessionKeys));
                    }
                    this.futureSessionTokens.Add(newToken);
                }
                // SecurityTraceRecordHelper.TraceNewServerSessionKeyIssued(newToken, supportingToken, GetLocalUri());
            }

            protected Uri GetLocalUri()
            {
                if (this.channelBinder.LocalAddress == null)
                    return null;
                else
                    return this.channelBinder.LocalAddress.Uri;
            }

            private void OnInnerFaulted(IReliableChannelBinder sender, Exception exception)
            {
                this.OnFaulted(exception);
            }

            private SecurityContextSecurityToken GetSessionToken(SecurityMessageProperty securityProperty)
            {
                SecurityContextSecurityToken sct = (securityProperty.ProtectionToken != null) ? securityProperty.ProtectionToken.SecurityToken as SecurityContextSecurityToken : null;
                if (sct != null && sct.ContextId == this.sessionId)
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
                            if (sct != null && sct.ContextId == this.sessionId)
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
                    message.Headers.Action != this.Settings.SecurityStandardsManager.SecureConversationDriver.CloseAction.Value)
                {
                    if (this.Settings.CanRenewSession)
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
                lock (this.LocalLock)
                {
                    if (this.futureSessionTokens.Count > 0 && incomingToken.KeyGeneration != this.currentSessionToken.KeyGeneration)
                    {
                        bool changedCurrentSessionToken = false;
                        for (int i = 0; i < this.futureSessionTokens.Count; ++i)
                        {
                            if (futureSessionTokens[i].KeyGeneration == incomingToken.KeyGeneration)
                            {
                                // let the current token expire after KeyRollover time interval
                                DateTime keyRolloverTime = TimeoutHelper.Add(DateTime.UtcNow, this.Settings.KeyRolloverInterval);
                                this.Settings.SessionTokenCache.UpdateContextCachingTime(this.currentSessionToken, keyRolloverTime);
                                this.currentSessionToken = futureSessionTokens[i];
                                futureSessionTokens.RemoveAt(i);
                                ((IAcceptorSecuritySessionProtocol)this.securityProtocol).SetOutgoingSessionToken(this.currentSessionToken);
                                changedCurrentSessionToken = true;
                                break;
                            }
                        }
                        if (changedCurrentSessionToken)
                        {
                            // SecurityTraceRecordHelper.TraceServerSessionKeyUpdated(this.currentSessionToken, GetLocalUri());
                            // remove all renewed tokens that will never be used.
                            for (int i = 0; i < futureSessionTokens.Count; ++i)
                            {
                                this.Settings.SessionTokenCache.RemoveContext(futureSessionTokens[i].ContextId, futureSessionTokens[i].KeyGeneration);
                            }
                            this.futureSessionTokens.Clear();
                        }
                    }
                }

                return true;
            }

            public RequestContext ReceiveRequest(RequestContext initialRequestContext)
            {
                return this.ReceiveRequest(ServiceDefaults.ReceiveTimeout, initialRequestContext);
            }

            public RequestContext ReceiveRequest(TimeSpan timeout, RequestContext initialRequestContext)
            {
                this.initialRequestContext = initialRequestContext;
                RequestContext requestContext;
                if (this.TryReceiveRequest(timeout, out requestContext))
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
                    if (isInputClosed || this.State == CommunicationState.Faulted)
                    {
                        break;
                    }
                    if (timeoutHelper.RemainingTime() == TimeSpan.Zero)
                    {
                        requestContext = null;
                        return false;
                    }

                    RequestContext innerRequestContext;
                    if (initialRequestContext != null)
                    {
                        innerRequestContext = initialRequestContext;
                        initialRequestContext = null;
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
                    if (this.isInputClosed && innerRequestContext.RequestMessage != null)
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
                    SecurityProtocolCorrelationState correlationState = null;
                    bool isSecurityProcessingFailure;
                    Message requestMessage = ProcessRequestContext(innerRequestContext, timeoutHelper.RemainingTime(), out correlationState, out isSecurityProcessingFailure);
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
                this.Settings.WrapperCommunicationObj.ThrowIfFaulted();
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
                    MessageFault fault = SecurityUtils.CreateSecurityMessageFault(e, this.securityProtocol.SecurityProtocolFactory.StandardsManager);
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
                                faultMessage.InitializeReply(unverifiedMessage);
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
                        if (message.Headers.Action == this.Settings.SecurityStandardsManager.SecureConversationDriver.CloseAction.Value)
                        {
                            //  SecurityTraceRecordHelper.TraceServerSessionCloseReceived(this.currentSessionToken, GetLocalUri());
                            this.isInputClosed = true;
                            // OnCloseMessageReceived is responsible for closing the message and requestContext if required.
                            this.OnCloseMessageReceived(requestContext, message, correlationState, timeoutHelper.GetCancellationToken());
                            correlationState = null;
                        }
                        else if (message.Headers.Action == this.Settings.SecurityStandardsManager.SecureConversationDriver.CloseResponseAction.Value)
                        {
                            // SecurityTraceRecordHelper.TraceServerSessionCloseResponseReceived(this.currentSessionToken, GetLocalUri());
                            this.isInputClosed = true;
                            // OnCloseResponseMessageReceived is responsible for closing the message and requestContext if required.
                            this.OnCloseResponseMessageReceived(requestContext, message, correlationState, timeoutHelper.RemainingTime());
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
                lock (this.LocalLock)
                {
                    if (this.currentSessionToken.KeyExpirationTime < DateTime.UtcNow)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new Exception(SR.SecuritySessionKeyIsStale));
                    }
                }
            }

            internal void SecureApplicationMessage(ref Message message, CancellationToken token, SecurityProtocolCorrelationState correlationState)
            {
                ThrowIfFaulted();
                ThrowIfClosedOrNotOpen();
                CheckOutgoingToken();
                message = this.securityProtocol.SecureOutgoingMessage(message, token);
            }

            private void ThrowIfClosedOrNotOpen()
            {
                //throw new NotImplementedException();
            }

            internal SecurityProtocolCorrelationState VerifyIncomingMessage(ref Message message, TimeSpan timeout)
            {
                return this.securityProtocol.VerifyIncomingMessage(ref message, timeout, null);
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
                if (!areFaultCodesInitialized)
                {
                    lock (this.LocalLock)
                    {
                        if (!areFaultCodesInitialized)
                        {
                            SecurityStandardsManager standardsManager = this.securityProtocol.SecurityProtocolFactory.StandardsManager;
                            SecureConversationDriver scDriver = standardsManager.SecureConversationDriver;
                            renewFaultCode = FaultCode.CreateSenderFaultCode(scDriver.RenewNeededFaultCode.Value, scDriver.Namespace.Value);
                            renewFaultReason = new FaultReason(SR.SecurityRenewFaultReason, System.Globalization.CultureInfo.InvariantCulture);
                            sessionAbortedFaultCode = FaultCode.CreateSenderFaultCode(DotNetSecurityStrings.SecuritySessionAbortedFault, DotNetSecurityStrings.Namespace);
                            sessionAbortedFaultReason = new FaultReason(SR.SecuritySessionAbortedFaultReason, System.Globalization.CultureInfo.InvariantCulture);
                            areFaultCodesInitialized = true;
                        }
                    }
                }
            }

            private void SendRenewFault(RequestContext requestContext, SecurityProtocolCorrelationState correlationState, TimeSpan timeout)
            {
                Message message = requestContext.RequestMessage;
                try
                {
                    InitializeFaultCodesIfRequired();
                    MessageFault renewFault = MessageFault.CreateFault(renewFaultCode, renewFaultReason);
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
                        response = this.securityProtocol.SecureOutgoingMessage(response, timeoutHelper.GetCancellationToken());
                        response.Properties.AllowOutputBatching = false;
                        SendMessage(requestContext, response, timeoutHelper.GetCancellationToken());
                    }
                    finally
                    {
                        response.Close();
                    }
                    //  SecurityTraceRecordHelper.TraceSessionRenewalFaultSent(this.currentSessionToken, GetLocalUri(), message);
                }
                catch (CommunicationException e)
                {
                    //SecurityTraceRecordHelper.TraceRenewFaultSendFailure(this.currentSessionToken, GetLocalUri(), e);
                }
                catch (TimeoutException e)
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
                    rst = this.Settings.SecurityStandardsManager.TrustDriver.CreateRequestSecurityToken(bodyReader);
                    request.ReadFromBodyContentsToEnd(bodyReader);
                }
                if (rst.RequestType != null && rst.RequestType != this.Settings.SecurityStandardsManager.TrustDriver.RequestTypeClose)
                {
                    throw TraceUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.InvalidRstRequestType, rst.RequestType)), request);
                }
                if (rst.CloseTarget == null)
                {
                    throw TraceUtility.ThrowHelperWarning(new MessageSecurityException(SR.NoCloseTargetSpecified), request);
                }
                SecurityContextKeyIdentifierClause sctSkiClause = rst.CloseTarget as SecurityContextKeyIdentifierClause;
                if (sctSkiClause == null || !SecuritySessionSecurityTokenAuthenticator.DoesSkiClauseMatchSigningToken(sctSkiClause, request))
                {
                    throw TraceUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.BadCloseTarget, rst.CloseTarget)), request);
                }
                RequestSecurityTokenResponse rstr = new RequestSecurityTokenResponse(this.Settings.SecurityStandardsManager);
                rstr.Context = rst.Context;
                rstr.IsRequestedTokenClosed = true;
                rstr.MakeReadOnly();
                BodyWriter bodyWriter = rstr;
                if (this.Settings.SecurityStandardsManager.MessageSecurityVersion.TrustVersion == TrustVersion.WSTrust13)
                {
                    List<RequestSecurityTokenResponse> rstrList = new List<RequestSecurityTokenResponse>(1);
                    rstrList.Add(rstr);
                    RequestSecurityTokenResponseCollection rstrc = new RequestSecurityTokenResponseCollection(rstrList, this.Settings.SecurityStandardsManager);
                    bodyWriter = rstrc;
                }
                Message response = Message.CreateMessage(request.Version, ActionHeader.Create(this.Settings.SecurityStandardsManager.SecureConversationDriver.CloseResponseAction, request.Version.Addressing), bodyWriter);
                PrepareReply(request, response);
                return response;
            }

            internal Message CreateCloseResponse(Message message, SecurityProtocolCorrelationState correlationState, CancellationToken token)
            {
                using (message)
                {
                    Message response = this.ProcessCloseRequest(message);
                    response = this.securityProtocol.SecureOutgoingMessage(response, token);
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
                RequestSecurityToken rst = new RequestSecurityToken(this.Settings.SecurityStandardsManager);
                rst.RequestType = this.Settings.SecurityStandardsManager.TrustDriver.RequestTypeClose;
                rst.CloseTarget = this.Settings.IssuedSecurityTokenParameters.CreateKeyIdentifierClause(this.currentSessionToken, SecurityTokenReferenceStyle.External);
                rst.MakeReadOnly();
                Message closeMessage = Message.CreateMessage(this.messageVersion, ActionHeader.Create(this.Settings.SecurityStandardsManager.SecureConversationDriver.CloseAction, this.messageVersion.Addressing), rst);
                RequestReplyCorrelator.PrepareRequest(closeMessage);
                if (this.LocalAddress != null)
                {
                    closeMessage.Headers.ReplyTo = this.LocalAddress;
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
                this.securityProtocol.SecureOutgoingMessage(closeMessage, token);
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
                this.Settings.WrapperCommunicationObj.Fault(ex);
            }
        }

        private abstract class ServerSecuritySimplexSessionChannel : ServerSecuritySessionChannel
        {
            private SoapSecurityInputSession session;
            private bool receivedClose;
            private bool canSendCloseResponse;
            private bool sentCloseResponse;
            private RequestContext closeRequestContext;
            private Message closeResponse;

            public ServerSecuritySimplexSessionChannel(
                SecuritySessionServerSettings settings,
                SecurityContextSecurityToken sessionToken,
                object listenerSecurityState, SecurityListenerSettingsLifetimeManager settingsLifetimeManager, EndpointAddress address)
                : base(settings, sessionToken, listenerSecurityState, settingsLifetimeManager, address)
            {
                this.session = new SoapSecurityInputSession(sessionToken, settings, this);
            }

            public IInputSession Session => this.session;

            private void CleanupPendingCloseState()
            {
                lock (this.LocalLock)
                {
                    if (this.closeResponse != null)
                    {
                        this.closeResponse.Close();
                        this.closeResponse = null;
                    }
                    if (this.closeRequestContext != null)
                    {
                        this.closeRequestContext.Abort();
                        this.closeRequestContext = null;
                    }
                }
            }

            protected override void AbortCore()
            {
                base.AbortCore();
                this.Settings.RemoveSessionChannel(this.session.Id);
                CleanupPendingCloseState();
            }

            protected override void CloseCore(CancellationToken token)
            {
                base.CloseCore(token);
                this.Settings.RemoveSessionChannel(this.session.Id);
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
                lock (this.LocalLock)
                {
                    this.canSendCloseResponse = true;
                    if (!this.sentCloseResponse && this.receivedClose && this.closeResponse != null)
                    {
                        this.sentCloseResponse = true;
                        sendCloseResponse = true;
                        pendingCloseRequestContext = this.closeRequestContext;
                        pendingCloseResponse = this.closeResponse;
                        this.closeResponse = null;
                        this.closeRequestContext = null;
                    }
                    else
                    {
                        canSendCloseResponse = false;
                        pendingCloseRequestContext = null;
                        pendingCloseResponse = null;
                    }
                }
                return sendCloseResponse;
            }

            private bool SendCloseResponseOnCloseIfRequired(CancellationToken token)
            {
                bool aborted = false;
                RequestContext pendingCloseRequestContext;
                Message pendingCloseResponse;
                bool sendCloseResponse = ShouldSendCloseResponseOnClose(out pendingCloseRequestContext, out pendingCloseResponse);
                bool cleanupCloseState = true;
                if (sendCloseResponse)
                {
                    try
                    {
                        this.SendCloseResponse(pendingCloseRequestContext, pendingCloseResponse, token);
                        // this.inputSessionClosedHandle.Set();
                        cleanupCloseState = false;
                    }
                    catch (CommunicationObjectAbortedException)
                    {
                        if (this.State != CommunicationState.Closed)
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
                this.Fault(new ProtocolException(SR.UnexpectedSecuritySessionCloseResponse));
            }

            private void Fault(ProtocolException protocolException)
            {
                this.AbortCore();
                base.OnFaulted(protocolException);
            }

            protected override void OnCloseMessageReceived(RequestContext requestContext, Message message, SecurityProtocolCorrelationState correlationState, CancellationToken token)
            {
                if (this.State == CommunicationState.Created)
                {
                    Fx.Assert("ServerSecuritySimplexSessionChannel.OnCloseMessageReceived (this.State == Created)");
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ServerReceivedCloseMessageStateIsCreated, this.GetType().ToString())));
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
                    lock (this.LocalLock)
                    {
                        if (!this.receivedClose)
                        {
                            this.receivedClose = true;
                            localCloseResponse = CreateCloseResponse(message, correlationState, token);
                            if (canSendCloseResponse)
                            {
                                this.sentCloseResponse = true;
                                sendCloseResponse = true;
                            }
                            else
                            {
                                // save the close requestContext to reply later
                                this.closeRequestContext = requestContext;
                                this.closeResponse = localCloseResponse;
                                cleanupContext = false;
                            }
                        }
                    }
                    if (sendCloseResponse)
                    {
                        this.SendCloseResponse(requestContext, localCloseResponse, token);
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
                private ServerSecuritySessionChannel channel;
                private UniqueId securityContextTokenId;
                private SecurityKeyIdentifierClause sessionTokenIdentifier;
                private SecurityStandardsManager standardsManager;

                public SoapSecurityInputSession(SecurityContextSecurityToken sessionToken,
                    SecuritySessionServerSettings settings, ServerSecuritySessionChannel channel)
                {
                    this.channel = channel;
                    this.securityContextTokenId = sessionToken.ContextId;
                    Claim identityClaim = SecurityUtils.GetPrimaryIdentityClaim(sessionToken.AuthorizationPolicies);
                    if (identityClaim != null)
                    {
                        this.RemoteIdentity = EndpointIdentity.CreateIdentity(identityClaim);
                    }
                    this.sessionTokenIdentifier = settings.IssuedSecurityTokenParameters.CreateKeyIdentifierClause(sessionToken, SecurityTokenReferenceStyle.External);
                    this.standardsManager = settings.SessionProtocolFactory.StandardsManager;
                }

                public string Id => this.securityContextTokenId.ToString();

                public EndpointIdentity RemoteIdentity { get; }

                public void WriteSessionTokenIdentifier(XmlDictionaryWriter writer)
                {
                    this.standardsManager.SecurityTokenSerializer.WriteKeyIdentifierClause(writer, this.sessionTokenIdentifier);
                }

                public bool TryReadSessionTokenIdentifier(XmlReader reader)
                {
                    if (!this.standardsManager.SecurityTokenSerializer.CanReadKeyIdentifierClause(reader))
                    {
                        return false;
                    }
                    SecurityContextKeyIdentifierClause incomingTokenIdentifier = this.standardsManager.SecurityTokenSerializer.ReadKeyIdentifierClause(reader) as SecurityContextKeyIdentifierClause;
                    return incomingTokenIdentifier != null && incomingTokenIdentifier.Matches(this.securityContextTokenId, null);
                }
            }

            //Renamed SecurityReplySessionChannel => SecurityReplySessionServiceChannelDispatcher (implementd IServiceChannelDispatcher)
            public class SecurityReplySessionServiceChannelDispatcher : ServerSecuritySimplexSessionChannel, IServiceChannelDispatcher, IReplySessionChannel
            {
                private readonly IServiceProvider serviceProvider;
                private volatile IServiceChannelDispatcher channelDispatcher;
                public SecurityReplySessionServiceChannelDispatcher(
                    SecuritySessionServerSettings settings,
                    SecurityContextSecurityToken sessionToken,
                    object listenerSecurityState, SecurityListenerSettingsLifetimeManager settingsLifetimeManager
                    , IChannel channel, EndpointAddress address)
                    : base(settings,
                          sessionToken, listenerSecurityState, settingsLifetimeManager, address)
                {
                    this.IncomingChannel = (IReplyChannel)channel;
                    // this.defaultBinder = new ReplyChannelBinder();
                    // this.idleManager = new ServiceChannel.SessionIdleManager();
                    serviceProvider = this.IncomingChannel.GetProperty<IServiceScopeFactory>().CreateScope().ServiceProvider;
                }

                public IReplyChannel IncomingChannel { get; set; }

                public IServiceChannelDispatcher ChannelDispatcher { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

                protected override bool CanDoSecurityCorrelation => true;

                public event EventHandler Closed;
                public event EventHandler Closing;
                public event EventHandler Faulted;
                public event EventHandler Opened;
                public event EventHandler Opening;

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
                    RequestContext securityRequestContext = base.ReceiveRequest(context);
                    return channelDispatcher.DispatchAsync(securityRequestContext);
                }

                public Task DispatchAsync(Message message)
                {
                    throw new NotImplementedException();
                }

                public T GetProperty<T>() where T : class
                {
                    var tObj = serviceProvider.GetService<T>();
                    if (tObj == null)
                        return IncomingChannel.GetProperty<T>();
                    else return tObj;
                }

                public Task OpenAsync()
                {
                    return Task.CompletedTask;
                }

                public async override Task OpenAsync(TimeSpan timeout)
                {
                    await base.OpenAsync(timeout);
                    this.channelDispatcher = await this.Settings.SecurityServiceDispatcher.
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
            private RequestContext requestContext;
            private ServerSecuritySessionChannel channel;
            private SecurityProtocolCorrelationState correlationState;

            public SecuritySessionRequestContext(RequestContext requestContext, Message requestMessage, SecurityProtocolCorrelationState correlationState, ServerSecuritySessionChannel channel)
                : base(requestMessage, ServiceDefaults.CloseTimeout, ServiceDefaults.SendTimeout)
            {
                this.requestContext = requestContext;
                this.correlationState = correlationState;
                this.channel = channel;
            }

            protected override void OnAbort()
            {
                this.requestContext.Abort();
            }

            protected override Task OnCloseAsync(CancellationToken token)
            {
                return this.requestContext.CloseAsync(token);
            }

            protected override Task OnReplyAsync(Message message, CancellationToken token)
            {
                if (message != null)
                {
                    this.channel.SecureApplicationMessage(ref message, token, correlationState);
                    //this.securityProtocol.SecureOutgoingMessageAsync(message);
                    return this.requestContext.ReplyAsync(message);
                }
                else
                {
                    return Task.CompletedTask;
                }
            }
        }
    }
}
