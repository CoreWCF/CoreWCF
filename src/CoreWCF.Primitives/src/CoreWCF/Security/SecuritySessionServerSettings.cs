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
        private readonly Dictionary<UniqueId, IServerSecuritySessionChannel> activeSessions;
        private SecurityServiceDispatcher securityServiceDispatcher;
        private ChannelBuilder channelBuilder;
        private SecurityStandardsManager standardsManager;
        private SecurityTokenParameters issuedTokenParameters;
        private SecurityTokenResolver sessionTokenResolver;
        private bool acceptNewWork;
        private Uri listenUri;
        private SecurityListenerSettingsLifetimeManager settingsLifetimeManager;

        public SecuritySessionServerSettings()
        {
            activeSessions = new Dictionary<UniqueId, IServerSecuritySessionChannel>();
            maximumKeyRenewalInterval = defaultKeyRenewalInterval;
            maximumPendingKeysPerSession = 5;
            keyRolloverInterval = defaultKeyRolloverInterval;
            inactivityTimeout = defaultInactivityTimeout;
            tolerateTransportFailures = defaultTolerateTransportFailures;
            maximumPendingSessions = defaultMaximumPendingSessions;
            WrapperCommunicationObj = new WrapperSecurityCommunicationObject(this);
        }

        internal ChannelBuilder ChannelBuilder
        {
            get
            {
                return channelBuilder;
            }
            set
            {
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                channelBuilder = value;
            }
        }

        internal WrapperSecurityCommunicationObject WrapperCommunicationObj { get; }

        internal SecurityListenerSettingsLifetimeManager SettingsLifetimeManager
        {
            get
            {
                return settingsLifetimeManager;
            }
            set
            {
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                settingsLifetimeManager = value;
            }
        }

        internal SecurityServiceDispatcher SecurityServiceDispatcher
        {
            get
            {
                return securityServiceDispatcher;
            }
            set
            {
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                securityServiceDispatcher = value;
            }
        }

        /// <summary>
        /// AcceptorChannelType will help determine if it's a duplex or simple reply channel
        /// </summary>
        internal Type AcceptorChannelType { get; set; }

        private Uri Uri
        {
            get
            {
                WrapperCommunicationObj.ThrowIfNotOpened();
                return listenUri;
            }
        }

        internal object ThisGlobalLock { get; } = new object();

        public SecurityTokenAuthenticator SessionTokenAuthenticator { get; private set; }

        public ISecurityContextSecurityTokenCache SessionTokenCache { get; private set; }

        public SecurityTokenResolver SessionTokenResolver => sessionTokenResolver;

        public SecurityTokenParameters IssuedSecurityTokenParameters
        {
            get
            {
                return issuedTokenParameters;
            }
            set
            {
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                issuedTokenParameters = value;
            }
        }

        internal SecurityStandardsManager SecurityStandardsManager
        {
            get
            {
                return standardsManager;
            }
            set
            {
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                standardsManager = value;
            }
        }

        public bool TolerateTransportFailures
        {
            get
            {
                return tolerateTransportFailures;
            }
            set
            {
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                tolerateTransportFailures = value;
            }
        }

        public bool CanRenewSession { get; set; } = true;

        public int MaximumPendingSessions
        {
            get
            {
                return maximumPendingSessions;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                maximumPendingSessions = value;
            }
        }

        public TimeSpan InactivityTimeout
        {
            get
            {
                return inactivityTimeout;
            }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }

                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                inactivityTimeout = value;
            }
        }

        public TimeSpan MaximumKeyRenewalInterval
        {
            get
            {
                return maximumKeyRenewalInterval;
            }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                maximumKeyRenewalInterval = value;
            }
        }

        public TimeSpan KeyRolloverInterval
        {
            get
            {
                return keyRolloverInterval;
            }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                keyRolloverInterval = value;
            }
        }

        public int MaximumPendingKeysPerSession
        {
            get
            {
                return maximumPendingKeysPerSession;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.ValueMustBeGreaterThanZero));
                }
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                maximumPendingKeysPerSession = value;
            }
        }

        public SecurityProtocolFactory SessionProtocolFactory
        {
            get
            {
                return sessionProtocolFactory;
            }
            set
            {
                WrapperCommunicationObj.ThrowIfDisposedOrImmutable();
                sessionProtocolFactory = value;
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
            if (inactivityTimer != null)
            {
                inactivityTimer.Cancel();
            }
            if (sessionProtocolFactory != null)
            {
                sessionProtocolFactory.OnCloseAsync(timeoutHelper.RemainingTime());
            }
            if (SessionTokenAuthenticator != null)
            {
                SecurityUtils.CloseTokenAuthenticatorIfRequiredAsync(SessionTokenAuthenticator, timeoutHelper.GetCancellationToken());
            }
        }

        private void OnAbortCore()
        {
            if (inactivityTimer != null)
            {
                inactivityTimer.Cancel();
            }
            if (sessionProtocolFactory != null)
            {
                sessionProtocolFactory.OnCloseAsync(TimeSpan.Zero);
            }
            if (SessionTokenAuthenticator != null)
            {
                SecurityUtils.AbortTokenAuthenticatorIfRequired(SessionTokenAuthenticator);
            }
        }

        private Task SetupSessionTokenAuthenticatorAsync()
        {
            RecipientServiceModelSecurityTokenRequirement requirement = new RecipientServiceModelSecurityTokenRequirement();
            issuedTokenParameters.InitializeSecurityTokenRequirement(requirement);
            requirement.KeyUsage = SecurityKeyUsage.Signature;
            requirement.ListenUri = listenUri;
            requirement.SecurityBindingElement = sessionProtocolFactory.SecurityBindingElement;
            requirement.SecurityAlgorithmSuite = sessionProtocolFactory.IncomingAlgorithmSuite;
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
            SessionTokenAuthenticator = sessionProtocolFactory.SecurityTokenManager.CreateSecurityTokenAuthenticator(requirement, out sessionTokenResolver);
            if (!(SessionTokenAuthenticator is IIssuanceSecurityTokenAuthenticator))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecuritySessionRequiresIssuanceAuthenticator, typeof(IIssuanceSecurityTokenAuthenticator), SessionTokenAuthenticator.GetType())));
            }
            if (sessionTokenResolver == null || (!(sessionTokenResolver is ISecurityContextSecurityTokenCache)))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecuritySessionRequiresSecurityContextTokenCache, sessionTokenResolver.GetType(), typeof(ISecurityContextSecurityTokenCache))));
            }
            SessionTokenCache = (ISecurityContextSecurityTokenCache)sessionTokenResolver;
            return Task.CompletedTask;
        }

        public void StopAcceptingNewWork()
        {
            acceptNewWork = false;
        }

        private int GetPendingSessionCount()
        {
            return pendingSessions1.Count + pendingSessions2.Count;
        }

        private void AbortPendingChannels(CancellationToken token)
        {
            ClosePendingChannels(token);
        }

        private async void ClosePendingChannels(CancellationToken token)
        {
            var tasks = new Task[activeSessions.Count];
            lock (ThisGlobalLock)
            {
                int index = 0;
                if (typeof(IReplyChannel).Equals(AcceptorChannelType))
                {
                    foreach (ServerSecuritySimplexSessionChannel securitySessionSimplexChannel in activeSessions.Values)
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
            if (sessionProtocolFactory is SessionSymmetricTransportSecurityProtocolFactory)
            {
                SessionSymmetricTransportSecurityProtocolFactory transportPf = (SessionSymmetricTransportSecurityProtocolFactory)sessionProtocolFactory;
                transportPf.AddTimestamp = true;
                transportPf.SecurityTokenParameters = IssuedSecurityTokenParameters;
                transportPf.SecurityTokenParameters.RequireDerivedKeys = false;
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
            }
        }

        private void OnTokenRenewed(SecurityToken newToken, SecurityToken oldToken)
        {
            WrapperCommunicationObj.ThrowIfClosed();
            if (!acceptNewWork)
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
                MessageFilter sctFilter = new SecuritySessionFilter(sessionToken.ContextId, sessionProtocolFactory.StandardsManager, (sessionProtocolFactory.SecurityHeaderLayout == SecurityHeaderLayout.Strict), SecurityStandardsManager.SecureConversationDriver.RenewAction.Value, SecurityStandardsManager.SecureConversationDriver.RenewResponseAction.Value);
                SessionInitiationMessageServiceDispatcher sessionServiceDispatcher
                 = new SessionInitiationMessageServiceDispatcher(this, sessionToken, sctFilter, remoteAddress);
                //logic to separate for Duplex
                if (typeof(IReplyChannel).Equals(AcceptorChannelType))
                {
                    ChannelBuilder.AddServiceDispatcher<IReplyChannel>(sessionServiceDispatcher, new ChannelDemuxerFilter(sctFilter, Int32.MaxValue));
                }

                AddPendingSession(sessionToken.ContextId, sessionToken, sctFilter);
            }
        }

        private void OnTokenIssued(SecurityToken issuedToken, EndpointAddress tokenRequestor)
        {
            WrapperCommunicationObj.ThrowIfClosed(); //TODO mark open
            if (!acceptNewWork)
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
            if (pendingSessions1 != null && pendingSessions1.ContainsKey(sessionId))
            {
                return pendingSessions1[sessionId];
            }

            if (pendingSessions2 != null && pendingSessions2.ContainsKey(sessionId))
            {
                return pendingSessions2[sessionId];
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
                    inactivityTimer.Set(inactivityTimeout);
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
                if (pendingSessions1.ContainsKey(sessionId) || pendingSessions2.ContainsKey(sessionId))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.SecuritySessionAlreadyPending, sessionId)));
                }
                pendingSessions1.Add(sessionId, securityToken);
                sessionFilters.Add(sessionId, filter);
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
                if (pendingSessions1.Count == 0 && pendingSessions2.Count == 0)
                {
                    return;
                }
                foreach (UniqueId sessionId in pendingSessions2.Keys)
                {
                    SecurityContextSecurityToken token = pendingSessions2[sessionId];
                    try
                    {
                        //TryCloseBinder(channelBinder, this.CloseTimeout); // Replacing this line with below (being proactive rather reactive(in WCF))
                        ChannelBuilder.RemoveServiceDispatcher<IReplyChannel>(sessionFilters[sessionId]);
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
                pendingSessions2.Clear();
                Dictionary<UniqueId, SecurityContextSecurityToken> temp = pendingSessions2;
                pendingSessions2 = pendingSessions1;
                pendingSessions1 = temp;
            }
        }

        internal bool RemovePendingSession(UniqueId sessionId)
        {
            bool result;
            lock (ThisGlobalLock)
            {
                if (pendingSessions1.ContainsKey(sessionId))
                {
                    pendingSessions1.Remove(sessionId);
                    result = true;
                }
                else if (pendingSessions2.ContainsKey(sessionId))
                {
                    pendingSessions2.Remove(sessionId);
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
                activeSessions.TryGetValue(sessionId, out result);
            }
            return result;
        }

        private void AddSessionChannel(UniqueId sessionId, IServerSecuritySessionChannel channel, MessageFilter filter)
        {
            lock (ThisGlobalLock)
            {
                activeSessions.Add(sessionId, channel);
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
                ChannelBuilder.RemoveServiceDispatcher<IReplyChannel>(sessionFilters[sessionId]);
                activeSessions.Remove(sessionId);
                sessionFilters.Remove(sessionId);
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
            if (sessionProtocolFactory == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SecuritySessionProtocolFactoryShouldBeSetBeforeThisOperation));
            }
            if (standardsManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecurityStandardsManagerNotSet, GetType())));
            }
            if (issuedTokenParameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.IssuedSecurityTokenParametersNotSet, GetType())));
            }
            if (maximumKeyRenewalInterval < keyRolloverInterval)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.KeyRolloverGreaterThanKeyRenewal));
            }
            if (securityServiceDispatcher == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecurityChannelListenerNotSet, GetType())));
            }
            if (settingsLifetimeManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecuritySettingsLifetimeManagerNotSet, GetType())));
            }
            MessageVersion = channelBuilder.Binding.MessageVersion;
            listenUri = securityServiceDispatcher.BaseAddress;
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            pendingSessions1 = new Dictionary<UniqueId, SecurityContextSecurityToken>();
            pendingSessions2 = new Dictionary<UniqueId, SecurityContextSecurityToken>();
            sessionFilters = new Dictionary<UniqueId, MessageFilter>();
            if (inactivityTimeout < TimeSpan.MaxValue)
            {
                inactivityTimer = new IOThreadTimer(new Action<object>(OnTimer), this, false);
                inactivityTimer.Set(inactivityTimeout);
            }
            ConfigureSessionSecurityProtocolFactory();
            sessionProtocolFactory.OpenAsync(timeoutHelper.RemainingTime());
            SetupSessionTokenAuthenticatorAsync();
            ((IIssuanceSecurityTokenAuthenticator)SessionTokenAuthenticator).IssuedSecurityTokenHandler = OnTokenIssued;
            ((IIssuanceSecurityTokenAuthenticator)SessionTokenAuthenticator).RenewedSecurityTokenHandler = OnTokenRenewed;
            if (SessionTokenAuthenticator is SecuritySessionSecurityTokenAuthenticator)
            {
                ((SecuritySessionSecurityTokenAuthenticator)SessionTokenAuthenticator).
                    SecurityServiceDispatcher = SecurityServiceDispatcher;
            }
            acceptNewWork = true;
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
            private readonly SecuritySessionServerSettings settings;
            private readonly SecurityContextSecurityToken sessionToken;
            private volatile IServiceChannelDispatcher sessionChannelDispatcher;
            private readonly MessageFilter messageFilter;
            private readonly EndpointAddress remoteAddress;

            public SessionInitiationMessageServiceDispatcher(/*IServerReliableChannelBinder channelBinder,*/ SecuritySessionServerSettings settings, SecurityContextSecurityToken sessionToken, MessageFilter filter, EndpointAddress address)
            {
                this.settings = settings;
                this.sessionToken = sessionToken;
                messageFilter = filter;
                remoteAddress = address;
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
                if (sessionChannelDispatcher == null)
                {
                    using (await AsyncLock.TakeLockAsync())
                    {
                        if (sessionChannelDispatcher == null)
                        {
                            if (!settings.RemovePendingSession(sessionToken.ContextId))
                            {
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new CommunicationException(SR.Format(SR.SecuritySessionNotPending, sessionToken.ContextId)));
                            }
                            if (settings.AcceptorChannelType is IDuplexChannel)
                            {
                                throw new PlatformNotSupportedException();
                            }
                            else
                            {
                                ServerSecuritySimplexSessionChannel.SecurityReplySessionServiceChannelDispatcher
                                    replySessionChannelDispatcher = new ServerSecuritySimplexSessionChannel.
                                    SecurityReplySessionServiceChannelDispatcher(settings, sessionToken,
                                    null, settings.SettingsLifetimeManager, channel, remoteAddress);
                                await replySessionChannelDispatcher.OpenAsync(ServiceDefaults.OpenTimeout);
                                sessionChannelDispatcher = replySessionChannelDispatcher;
                                settings.AddSessionChannel(sessionToken.ContextId, replySessionChannelDispatcher, messageFilter);
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
            private readonly IServerReliableChannelBinder channelBinder;
            private readonly SecurityProtocol securityProtocol;

            // This is used to sign outgoing messages
            private SecurityContextSecurityToken currentSessionToken;
            private readonly UniqueId sessionId;

            // These are renewed tokens that have not been used as yet
            private readonly List<SecurityContextSecurityToken> futureSessionTokens;
            private RequestContext initialRequestContext;
            private bool isInputClosed;
            private readonly MessageVersion messageVersion;
            private readonly SecurityListenerSettingsLifetimeManager settingsLifetimeManager;
            private bool hasSecurityStateReference;

            protected ServerSecuritySessionChannel(SecuritySessionServerSettings settings,
                SecurityContextSecurityToken sessionToken,
                object listenerSecurityProtocolState,
                SecurityListenerSettingsLifetimeManager settingsLifetimeManager, EndpointAddress address)
            {
                Settings = settings;
                messageVersion = settings.MessageVersion;
                // this.channelBinder.Faulted += this.OnInnerFaulted;
                securityProtocol = Settings.SessionProtocolFactory.CreateSecurityProtocol(null, null, true, TimeSpan.Zero);
                if (!(securityProtocol is IAcceptorSecuritySessionProtocol))
                {
                    Fx.Assert("Security protocol must be IAcceptorSecuritySessionProtocol.");
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ProtocolMisMatch, "IAcceptorSecuritySessionProtocol", GetType().ToString())));
                }
                currentSessionToken = sessionToken;
                sessionId = sessionToken.ContextId;
                futureSessionTokens = new List<SecurityContextSecurityToken>(1);
                ((IAcceptorSecuritySessionProtocol)securityProtocol).SetOutgoingSessionToken(sessionToken);
                ((IAcceptorSecuritySessionProtocol)securityProtocol).SetSessionTokenAuthenticator(sessionId, Settings.SessionTokenAuthenticator, Settings.SessionTokenResolver);
                this.settingsLifetimeManager = settingsLifetimeManager;
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
                securityProtocol.OpenAsync(timeout);
                if (CanDoSecurityCorrelation)
                {
                    ((IAcceptorSecuritySessionProtocol)securityProtocol).ReturnCorrelationState = true;

                } // if an abort happened concurrently with the open, then return
                if (State == CommunicationState.Closed || State == CommunicationState.Closing)
                {
                    return Task.CompletedTask;
                }
                settingsLifetimeManager.AddReference();
                hasSecurityStateReference = true;
                return Task.CompletedTask;
            }

            protected virtual void AbortCore()
            {
                if (securityProtocol != null)
                {
                    TimeoutHelper timeout = new TimeoutHelper(ServiceDefaults.CloseTimeout);
                    securityProtocol.CloseAsync(true, timeout.RemainingTime());
                }
                Settings.SessionTokenCache.RemoveAllContexts(currentSessionToken.ContextId);
                bool abortLifetimeManager = false;
                lock (LocalLock)
                {
                    if (hasSecurityStateReference)
                    {
                        abortLifetimeManager = true;
                        hasSecurityStateReference = false;
                    }
                }
                if (abortLifetimeManager)
                {
                    settingsLifetimeManager.Abort();
                }
            }

            protected virtual void CloseCore(CancellationToken token)
            {
                try
                {
                    TimeoutHelper helper = new TimeoutHelper(ServiceDefaults.CloseTimeout);
                    if (securityProtocol != null)
                    {
                        securityProtocol.CloseAsync(false, helper.RemainingTime()); ;
                    }
                    bool closeLifetimeManager = false;
                    lock (LocalLock)
                    {
                        if (hasSecurityStateReference)
                        {
                            closeLifetimeManager = true;
                            hasSecurityStateReference = false;
                        }
                    }
                    if (closeLifetimeManager)
                    {
                        settingsLifetimeManager.CloseAsync(helper.RemainingTime());
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
                Settings.SessionTokenCache.RemoveAllContexts(currentSessionToken.ContextId);
            }

            protected abstract void OnCloseMessageReceived(RequestContext requestContext, Message message, SecurityProtocolCorrelationState correlationState, CancellationToken token);

            protected abstract void OnCloseResponseMessageReceived(RequestContext requestContext, Message message, SecurityProtocolCorrelationState correlationState, TimeSpan timeout);

            public void RenewSessionToken(SecurityContextSecurityToken newToken, SecurityContextSecurityToken supportingToken)
            {
                ThrowIfClosedOrNotOpen();
                // enforce that the token being renewed is the current session token
                lock (LocalLock)
                {
                    if (supportingToken.ContextId != currentSessionToken.ContextId || supportingToken.KeyGeneration != currentSessionToken.KeyGeneration)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.CurrentSessionTokenNotRenewed, supportingToken.KeyGeneration, currentSessionToken.KeyGeneration)));
                    }
                    if (futureSessionTokens.Count == Settings.MaximumPendingKeysPerSession)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.TooManyPendingSessionKeys));
                    }
                    futureSessionTokens.Add(newToken);
                }
                // SecurityTraceRecordHelper.TraceNewServerSessionKeyIssued(newToken, supportingToken, GetLocalUri());
            }

            protected Uri GetLocalUri()
            {
                if (channelBinder.LocalAddress == null)
                {
                    return null;
                }
                else
                {
                    return channelBinder.LocalAddress.Uri;
                }
            }

            private void OnInnerFaulted(IReliableChannelBinder sender, Exception exception)
            {
                OnFaulted(exception);
            }

            private SecurityContextSecurityToken GetSessionToken(SecurityMessageProperty securityProperty)
            {
                SecurityContextSecurityToken sct = (securityProperty.ProtectionToken != null) ? securityProperty.ProtectionToken.SecurityToken as SecurityContextSecurityToken : null;
                if (sct != null && sct.ContextId == sessionId)
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
                            if (sct != null && sct.ContextId == sessionId)
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
                    if (futureSessionTokens.Count > 0 && incomingToken.KeyGeneration != currentSessionToken.KeyGeneration)
                    {
                        bool changedCurrentSessionToken = false;
                        for (int i = 0; i < futureSessionTokens.Count; ++i)
                        {
                            if (futureSessionTokens[i].KeyGeneration == incomingToken.KeyGeneration)
                            {
                                // let the current token expire after KeyRollover time interval
                                DateTime keyRolloverTime = TimeoutHelper.Add(DateTime.UtcNow, Settings.KeyRolloverInterval);
                                Settings.SessionTokenCache.UpdateContextCachingTime(currentSessionToken, keyRolloverTime);
                                currentSessionToken = futureSessionTokens[i];
                                futureSessionTokens.RemoveAt(i);
                                ((IAcceptorSecuritySessionProtocol)securityProtocol).SetOutgoingSessionToken(currentSessionToken);
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
                                Settings.SessionTokenCache.RemoveContext(futureSessionTokens[i].ContextId, futureSessionTokens[i].KeyGeneration);
                            }
                            futureSessionTokens.Clear();
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
                this.initialRequestContext = initialRequestContext;
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
                    if (isInputClosed || State == CommunicationState.Faulted)
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
                    if (isInputClosed && innerRequestContext.RequestMessage != null)
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
                    MessageFault fault = SecurityUtils.CreateSecurityMessageFault(e, securityProtocol.SecurityProtocolFactory.StandardsManager);
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
                            isInputClosed = true;
                            // OnCloseMessageReceived is responsible for closing the message and requestContext if required.
                            OnCloseMessageReceived(requestContext, message, correlationState, timeoutHelper.GetCancellationToken());
                            correlationState = null;
                        }
                        else if (message.Headers.Action == Settings.SecurityStandardsManager.SecureConversationDriver.CloseResponseAction.Value)
                        {
                            // SecurityTraceRecordHelper.TraceServerSessionCloseResponseReceived(this.currentSessionToken, GetLocalUri());
                            isInputClosed = true;
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
                    if (currentSessionToken.KeyExpirationTime < DateTime.UtcNow)
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
                message = securityProtocol.SecureOutgoingMessage(message, token);
            }

            private void ThrowIfClosedOrNotOpen()
            {
                //throw new NotImplementedException();
            }

            internal SecurityProtocolCorrelationState VerifyIncomingMessage(ref Message message, TimeSpan timeout)
            {
                return securityProtocol.VerifyIncomingMessage(ref message, timeout, null);
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
                    lock (LocalLock)
                    {
                        if (!areFaultCodesInitialized)
                        {
                            SecurityStandardsManager standardsManager = securityProtocol.SecurityProtocolFactory.StandardsManager;
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
                        response = securityProtocol.SecureOutgoingMessage(response, timeoutHelper.GetCancellationToken());
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
                SecurityContextKeyIdentifierClause sctSkiClause = rst.CloseTarget as SecurityContextKeyIdentifierClause;
                if (sctSkiClause == null || !SecuritySessionSecurityTokenAuthenticator.DoesSkiClauseMatchSigningToken(sctSkiClause, request))
                {
                    throw TraceUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.BadCloseTarget, rst.CloseTarget)), request);
                }
                RequestSecurityTokenResponse rstr = new RequestSecurityTokenResponse(Settings.SecurityStandardsManager);
                rstr.Context = rst.Context;
                rstr.IsRequestedTokenClosed = true;
                rstr.MakeReadOnly();
                BodyWriter bodyWriter = rstr;
                if (Settings.SecurityStandardsManager.MessageSecurityVersion.TrustVersion == TrustVersion.WSTrust13)
                {
                    List<RequestSecurityTokenResponse> rstrList = new List<RequestSecurityTokenResponse>(1);
                    rstrList.Add(rstr);
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
                    response = securityProtocol.SecureOutgoingMessage(response, token);
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
                RequestSecurityToken rst = new RequestSecurityToken(Settings.SecurityStandardsManager);
                rst.RequestType = Settings.SecurityStandardsManager.TrustDriver.RequestTypeClose;
                rst.CloseTarget = Settings.IssuedSecurityTokenParameters.CreateKeyIdentifierClause(currentSessionToken, SecurityTokenReferenceStyle.External);
                rst.MakeReadOnly();
                Message closeMessage = Message.CreateMessage(messageVersion, ActionHeader.Create(Settings.SecurityStandardsManager.SecureConversationDriver.CloseAction, messageVersion.Addressing), rst);
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
                securityProtocol.SecureOutgoingMessage(closeMessage, token);
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
            private readonly SoapSecurityInputSession session;
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
                session = new SoapSecurityInputSession(sessionToken, settings, this);
            }

            public IInputSession Session => session;

            private void CleanupPendingCloseState()
            {
                lock (LocalLock)
                {
                    if (closeResponse != null)
                    {
                        closeResponse.Close();
                        closeResponse = null;
                    }
                    if (closeRequestContext != null)
                    {
                        closeRequestContext.Abort();
                        closeRequestContext = null;
                    }
                }
            }

            protected override void AbortCore()
            {
                base.AbortCore();
                Settings.RemoveSessionChannel(session.Id);
                CleanupPendingCloseState();
            }

            protected override void CloseCore(CancellationToken token)
            {
                base.CloseCore(token);
                Settings.RemoveSessionChannel(session.Id);
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
                    canSendCloseResponse = true;
                    if (!sentCloseResponse && receivedClose && closeResponse != null)
                    {
                        sentCloseResponse = true;
                        sendCloseResponse = true;
                        pendingCloseRequestContext = closeRequestContext;
                        pendingCloseResponse = closeResponse;
                        closeResponse = null;
                        closeRequestContext = null;
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
                base.OnFaulted(protocolException);
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
                        if (!receivedClose)
                        {
                            receivedClose = true;
                            localCloseResponse = CreateCloseResponse(message, correlationState, token);
                            if (canSendCloseResponse)
                            {
                                sentCloseResponse = true;
                                sendCloseResponse = true;
                            }
                            else
                            {
                                // save the close requestContext to reply later
                                closeRequestContext = requestContext;
                                closeResponse = localCloseResponse;
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
                private readonly ServerSecuritySessionChannel channel;
                private readonly UniqueId securityContextTokenId;
                private readonly SecurityKeyIdentifierClause sessionTokenIdentifier;
                private readonly SecurityStandardsManager standardsManager;

                public SoapSecurityInputSession(SecurityContextSecurityToken sessionToken,
                    SecuritySessionServerSettings settings, ServerSecuritySessionChannel channel)
                {
                    this.channel = channel;
                    securityContextTokenId = sessionToken.ContextId;
                    Claim identityClaim = SecurityUtils.GetPrimaryIdentityClaim(sessionToken.AuthorizationPolicies);
                    if (identityClaim != null)
                    {
                        RemoteIdentity = EndpointIdentity.CreateIdentity(identityClaim);
                    }
                    sessionTokenIdentifier = settings.IssuedSecurityTokenParameters.CreateKeyIdentifierClause(sessionToken, SecurityTokenReferenceStyle.External);
                    standardsManager = settings.SessionProtocolFactory.StandardsManager;
                }

                public string Id => securityContextTokenId.ToString();

                public EndpointIdentity RemoteIdentity { get; }

                public void WriteSessionTokenIdentifier(XmlDictionaryWriter writer)
                {
                    standardsManager.SecurityTokenSerializer.WriteKeyIdentifierClause(writer, sessionTokenIdentifier);
                }

                public bool TryReadSessionTokenIdentifier(XmlReader reader)
                {
                    if (!standardsManager.SecurityTokenSerializer.CanReadKeyIdentifierClause(reader))
                    {
                        return false;
                    }
                    SecurityContextKeyIdentifierClause incomingTokenIdentifier = standardsManager.SecurityTokenSerializer.ReadKeyIdentifierClause(reader) as SecurityContextKeyIdentifierClause;
                    return incomingTokenIdentifier != null && incomingTokenIdentifier.Matches(securityContextTokenId, null);
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
                    IncomingChannel = (IReplyChannel)channel;
                    // this.defaultBinder = new ReplyChannelBinder();
                    // this.idleManager = new ServiceChannel.SessionIdleManager();
                    serviceProvider = IncomingChannel.GetProperty<IServiceScopeFactory>().CreateScope().ServiceProvider;
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

                public async override Task OpenAsync(TimeSpan timeout)
                {
                    await base.OpenAsync(timeout);
                    channelDispatcher = await Settings.SecurityServiceDispatcher.
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
            private readonly RequestContext requestContext;
            private readonly ServerSecuritySessionChannel channel;
            private readonly SecurityProtocolCorrelationState correlationState;

            public SecuritySessionRequestContext(RequestContext requestContext, Message requestMessage, SecurityProtocolCorrelationState correlationState, ServerSecuritySessionChannel channel)
                : base(requestMessage, ServiceDefaults.CloseTimeout, ServiceDefaults.SendTimeout)
            {
                this.requestContext = requestContext;
                this.correlationState = correlationState;
                this.channel = channel;
            }

            protected override void OnAbort()
            {
                requestContext.Abort();
            }

            protected override Task OnCloseAsync(CancellationToken token)
            {
                return requestContext.CloseAsync(token);
            }

            protected override Task OnReplyAsync(Message message, CancellationToken token)
            {
                if (message != null)
                {
                    channel.SecureApplicationMessage(ref message, token, correlationState);
                    //this.securityProtocol.SecureOutgoingMessageAsync(message);
                    return requestContext.ReplyAsync(message);
                }
                else
                {
                    return Task.CompletedTask;
                }
            }
        }
    }
}
