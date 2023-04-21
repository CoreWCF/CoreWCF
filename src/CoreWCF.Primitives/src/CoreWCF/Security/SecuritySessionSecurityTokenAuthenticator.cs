// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class SecuritySessionSecurityTokenAuthenticator : CommunicationObjectSecurityTokenAuthenticator, IIssuanceSecurityTokenAuthenticator //, ILogonTokenCacheManager
    {
        internal static readonly TimeSpan s_defaultSessionTokenLifetime = TimeSpan.MaxValue;
        internal const int DefaultMaxCachedSessionTokens = int.MaxValue;
        internal static readonly SecurityStandardsManager s_defaultStandardsManager = SecurityStandardsManager.DefaultInstance;
        private bool _isClientAnonymous;
        private TimeSpan _sessionTokenLifetime;
        private ISecurityContextSecurityTokenCache _issuedTokenCache;
        private SecurityBindingElement _bootstrapSecurityBindingElement;
        private BindingContext _issuerBindingContext;
        private SecurityStandardsManager _standardsManager;
        private SecurityAlgorithmSuite _securityAlgorithmSuite;
        private SecurityKeyEntropyMode _keyEntropyMode;
        private TimeSpan _keyRenewalInterval;
        private SecurityTokenParameters _issuedTokenParameters;
        private Uri _listenUri;
        private string _sctUri;
        private IMessageFilterTable<EndpointAddress> _endpointFilterTable;
        private bool _shouldMatchRstWithEndpointFilter;
        private int _maximumConcurrentNegotiations;
        private TimeSpan _negotiationTimeout;
        private readonly object _thisLock = new object();

        public SecuritySessionSecurityTokenAuthenticator()
        {
            SessionTokenAuthenticator = new SecurityContextSecurityTokenAuthenticator();
            _sessionTokenLifetime = s_defaultSessionTokenLifetime;
            _isClientAnonymous = false;
            _standardsManager = s_defaultStandardsManager;
            _keyEntropyMode = SecurityKeyEntropyMode.CombinedEntropy;// AcceleratedTokenProvider.defaultKeyEntropyMode;
            _maximumConcurrentNegotiations = 128;// AcceleratedTokenAuthenticator.defaultServerMaxActiveNegotiations;
            _negotiationTimeout = TimeSpan.Parse("00:01:00", CultureInfo.InvariantCulture); // AcceleratedTokenAuthenticator.defaultServerMaxNegotiationLifetime;
        }

        public IssuedSecurityTokenHandler IssuedSecurityTokenHandler { get; set; }

        public RenewedSecurityTokenHandler RenewedSecurityTokenHandler { get; set; }

        public SecurityServiceDispatcher SecurityServiceDispatcher { get; set; }

        public SecurityAlgorithmSuite SecurityAlgorithmSuite
        {
            get
            {
                return _securityAlgorithmSuite;
            }
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _securityAlgorithmSuite = value;
            }
        }

        public SecurityKeyEntropyMode KeyEntropyMode
        {
            get
            {
                return _keyEntropyMode;
            }
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                SecurityKeyEntropyModeHelper.Validate(value);
                _keyEntropyMode = value;
            }
        }

        public bool IsClientAnonymous
        {
            get
            {
                return _isClientAnonymous;
            }
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _isClientAnonymous = value;
            }
        }

        public TimeSpan SessionTokenLifetime
        {
            get
            {
                return _sessionTokenLifetime;
            }
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
                        SRCommon.SFxTimeoutOutOfRangeTooBig));
                }
                _sessionTokenLifetime = value;
            }
        }

        public TimeSpan KeyRenewalInterval
        {
            get
            {
                return _keyRenewalInterval;
            }
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
                        SRCommon.SFxTimeoutOutOfRangeTooBig));
                }
                _keyRenewalInterval = value;
            }
        }

        public int MaximumConcurrentNegotiations
        {
            get
            {
                return _maximumConcurrentNegotiations;
            }
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SRCommon.ValueMustBeNonNegative));
                }
                _maximumConcurrentNegotiations = value;
            }
        }

        public TimeSpan NegotiationTimeout
        {
            get
            {
                return _negotiationTimeout;
            }
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                _negotiationTimeout = value;
            }
        }

        public SecurityContextSecurityTokenAuthenticator SessionTokenAuthenticator { get; }

        public ISecurityContextSecurityTokenCache IssuedTokenCache
        {
            get
            {
                return _issuedTokenCache;
            }
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _issuedTokenCache = value;
            }
        }

        public SecurityStandardsManager StandardsManager
        {
            get
            {
                return _standardsManager;
            }
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
                }
                if (!value.TrustDriver.IsSessionSupported)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.TrustDriverVersionDoesNotSupportSession, nameof(value)));
                }
                if (!value.SecureConversationDriver.IsSessionSupported)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.SecureConversationDriverVersionDoesNotSupportSession, nameof(value)));
                }
                _standardsManager = value;
            }
        }

        public SecurityTokenParameters IssuedSecurityTokenParameters
        {
            get
            {
                return _issuedTokenParameters;
            }
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _issuedTokenParameters = value;
            }
        }

        public BindingContext IssuerBindingContext
        {
            get
            {
                return _issuerBindingContext;
            }
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

        public SecurityBindingElement BootstrapSecurityBindingElement
        {
            get { return _bootstrapSecurityBindingElement; }
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }
                _bootstrapSecurityBindingElement = (SecurityBindingElement)value.Clone();
            }
        }

        public IMessageFilterTable<EndpointAddress> EndpointFilterTable
        {
            get
            {
                return _endpointFilterTable;
            }
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _endpointFilterTable = value;
            }
        }

        public Uri ListenUri
        {
            get { return _listenUri; }
            set
            {
                CommunicationObject.ThrowIfDisposedOrImmutable();
                _listenUri = value;
            }
        }

        public virtual XmlDictionaryString IssueAction
        {
            get
            {
                return _standardsManager.SecureConversationDriver.IssueAction;
            }
        }

        public virtual XmlDictionaryString IssueResponseAction
        {
            get
            {
                return _standardsManager.SecureConversationDriver.IssueResponseAction;
            }
        }

        public bool PreserveBootstrapTokens { get; set; }

        public virtual XmlDictionaryString RenewAction
        {
            get
            {
                return _standardsManager.SecureConversationDriver.RenewAction;
            }
        }

        public virtual XmlDictionaryString RenewResponseAction
        {
            get
            {
                return _standardsManager.SecureConversationDriver.RenewResponseAction;
            }
        }

        public virtual XmlDictionaryString CloseAction
        {
            get
            {
                return _standardsManager.SecureConversationDriver.CloseAction;
            }
        }

        public virtual XmlDictionaryString CloseResponseAction
        {
            get
            {
                return _standardsManager.SecureConversationDriver.CloseResponseAction;
            }
        }

        //public bool RemoveCachedLogonToken(string username)
        //{
        //    if (this.RequestSecurityTokenListener != null)
        //    {
        //        //
        //        // this is the SCT case, delegate to the RST's listener list
        //        //
        //        IChannelListener listener = null;
        //        ILogonTokenCacheManager manager = null;

        //        for (int i = 0; i < this.RequestSecurityTokenListener.ChannelDispatchers.Count; i++)
        //        {
        //            listener = this.RequestSecurityTokenListener.ChannelDispatchers[i].Listener;

        //            if (listener != null)
        //            {
        //                manager = listener.GetProperty<ILogonTokenCacheManager>();

        //                if (manager != null)
        //                    return manager.RemoveCachedLogonToken(username);
        //            }
        //        }
        //    }
        //    return false;
        //}

        //public void FlushLogonTokenCache()
        //{
        //    if (this.RequestSecurityTokenListener != null && this.RequestSecurityTokenListener.ChannelDispatchers.Count > 0)
        //    {
        //        //
        //        // this is the SCT case, delegate to the RST's listener list
        //        //
        //        IChannelListener listener = null;
        //        ILogonTokenCacheManager manager = null;

        //        for (int i = 0; i < this.RequestSecurityTokenListener.ChannelDispatchers.Count; i++)
        //        {
        //            listener = this.RequestSecurityTokenListener.ChannelDispatchers[i].Listener;

        //            if (listener != null)
        //            {
        //                manager = listener.GetProperty<ILogonTokenCacheManager>();

        //                if (manager != null)
        //                    manager.FlushLogonTokenCache();
        //            }
        //        }
        //    }

        // }

        private Message HandleOperationException(SecuritySessionOperation operation, Message request, Exception e)
        {
            // SecurityTraceRecordHelper.TraceServerSessionOperationException(operation, e, this.ListenUri);
            return CreateFault(request, e);
        }

        private Message CreateFault(Message request, Exception e)
        {
            FaultCode subCode;
            FaultReason reason;
            bool isSenderFault;
            if (e is QuotaExceededException)
            {
                // send a receiver fault so that the sender can retry
                subCode = new FaultCode(DotNetSecurityStrings.SecurityServerTooBusyFault, DotNetSecurityStrings.Namespace);
                reason = new FaultReason(SR.PendingSessionsExceededFaultReason, CultureInfo.CurrentCulture);
                isSenderFault = false;
            }
            else if (e is EndpointNotFoundException)
            {
                // send a receiver fault so that the sender can retry
                subCode = new FaultCode(AddressingStrings.EndpointUnavailable, request.Version.Addressing.Namespace);
                reason = new FaultReason(SR.SecurityListenerClosingFaultReason, CultureInfo.CurrentCulture);
                isSenderFault = false;
            }
            else
            {
                subCode = new FaultCode(TrustApr2004Strings.InvalidRequestFaultCode, TrustFeb2005Strings.Namespace);
                reason = new FaultReason(SR.InvalidRequestTrustFaultCode, CultureInfo.CurrentCulture);
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
            Message faultReply = Message.CreateMessage(request.Version, fault, request.Version.Addressing.DefaultFaultAction);
            faultReply.Headers.RelatesTo = request.Headers.MessageId;
            return faultReply;
        }

        private void NotifyOperationCompletion(SecuritySessionOperation operation, SecurityContextSecurityToken newSessionToken, SecurityContextSecurityToken previousSessionToken, EndpointAddress remoteAddress)
        {
            if (operation == SecuritySessionOperation.Issue)
            {
                if (IssuedSecurityTokenHandler != null)
                {
                    IssuedSecurityTokenHandler(newSessionToken, remoteAddress);
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.IssueSessionTokenHandlerNotSet));
                }
            }
            else if (operation == SecuritySessionOperation.Renew)
            {
                if (RenewedSecurityTokenHandler != null)
                {
                    RenewedSecurityTokenHandler(newSessionToken, previousSessionToken);
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.RenewSessionTokenHandlerNotSet));
                }
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
            }
        }

        public override Task CloseAsync(CancellationToken token)
        {
            /* TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
             if (this.rstListener != null)
             {
                 this.rstListener.Close(timeoutHelper.RemainingTime());
                 this.rstListener = null;
             }*/

            return base.CloseAsync(token);
        }

        public override Task OpenAsync(CancellationToken token)
        {
            if (BootstrapSecurityBindingElement == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.BootstrapSecurityBindingElementNotSet, GetType())));
            }
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
            //TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            SetupSessionListener();
            ChannelDispatcher channelDispatcher = RequestSecurityTokenListener.InitializeRuntime(SecurityServiceDispatcher);
            SecurityServiceDispatcher.SecurityAuthServiceDispatcher = new ServiceDispatcher(channelDispatcher);
            _sctUri = StandardsManager.SecureConversationDriver.TokenTypeUri;
            return base.OpenAsync();
            // base.OnOpen(timeoutHelper.RemainingTime());
        }

        protected override bool CanValidateTokenCore(SecurityToken token)
        {
            return (token is SecurityContextSecurityToken);
        }

        protected override ValueTask<ReadOnlyCollection<IAuthorizationPolicy>> ValidateTokenCoreAsync(SecurityToken token)
        {
            SecurityContextSecurityToken sct = (SecurityContextSecurityToken)token;
            return new ValueTask<ReadOnlyCollection<IAuthorizationPolicy>>(sct.AuthorizationPolicies);
        }

        private static bool IsSameIdentity(ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies, ServiceSecurityContext incomingContext)
        {
            Claim identityClaim = SecurityUtils.GetPrimaryIdentityClaim(authorizationPolicies);

            if (identityClaim == null)
            {
                return incomingContext.IsAnonymous;
            }
            else
            {
                return Claim.DefaultComparer.Equals(incomingContext.IdentityClaim, identityClaim);
            }
        }

        private DateTime GetKeyExpirationTime(SecurityToken currentToken, DateTime keyEffectiveTime)
        {
            DateTime keyExpirationTime = TimeoutHelper.Add(keyEffectiveTime, _keyRenewalInterval);
            DateTime tokenExpirationTime = (currentToken != null) ? currentToken.ValidTo : TimeoutHelper.Add(keyEffectiveTime, _sessionTokenLifetime);
            if (keyExpirationTime > tokenExpirationTime)
            {
                keyExpirationTime = tokenExpirationTime;
            }
            return keyExpirationTime;
        }

        internal static ReadOnlyCollection<IAuthorizationPolicy> CreateSecureConversationPolicies(SecurityMessageProperty security, DateTime expirationTime)
        {
            return CreateSecureConversationPolicies(security, null, expirationTime);
        }

        private static ReadOnlyCollection<IAuthorizationPolicy> CreateSecureConversationPolicies(SecurityMessageProperty security, ReadOnlyCollection<IAuthorizationPolicy> currentTokenPolicies, DateTime expirationTime)
        {
            if (security == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(security));
            }

            List<IAuthorizationPolicy> authorizationPolicies = new List<IAuthorizationPolicy>();
            if ((security.ServiceSecurityContext != null) &&
                (security.ServiceSecurityContext.AuthorizationPolicies != null))
            {
                authorizationPolicies.AddRange(security.ServiceSecurityContext.AuthorizationPolicies);

                // Remove any Transport token policies. We do not include
                // these in the SCT as these policies will be available with
                // the application messages as well.
                if ((security.TransportToken != null) &&
                    (security.TransportToken.SecurityTokenPolicies != null) &&
                    (security.TransportToken.SecurityTokenPolicies.Count > 0))
                {
                    foreach (IAuthorizationPolicy policy in security.TransportToken.SecurityTokenPolicies)
                    {
                        if (authorizationPolicies.Contains(policy))
                        {
                            authorizationPolicies.Remove(policy);
                        }
                    }
                }

                if (currentTokenPolicies != null)
                {
                    for (int i = 0; i < currentTokenPolicies.Count; ++i)
                    {
                        if (authorizationPolicies.Contains(currentTokenPolicies[i]))
                        {
                            authorizationPolicies.Remove(currentTokenPolicies[i]);
                        }
                    }
                }

                UnconditionalPolicy sctPolicy;
                for (int i = 0; i < authorizationPolicies.Count; i++)
                {
                    if (authorizationPolicies[i].GetType() == typeof(UnconditionalPolicy))
                    {
                        UnconditionalPolicy bootstrapPolicy = (UnconditionalPolicy)authorizationPolicies[i];
                        sctPolicy = new UnconditionalPolicy(bootstrapPolicy.PrimaryIdentity, bootstrapPolicy.Issuances, expirationTime);
                        authorizationPolicies[i] = sctPolicy;
                    }
                }
            }

            return authorizationPolicies.AsReadOnly();
        }

        private SecurityContextSecurityToken IssueToken(RequestSecurityToken rst, Message request, SecurityContextSecurityToken currentToken, ReadOnlyCollection<IAuthorizationPolicy> currentTokenPolicies, out RequestSecurityTokenResponse rstr)
        {
            if (rst.TokenType != null && rst.TokenType != _sctUri)
            {
                throw TraceUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.CannotIssueRstTokenType, rst.TokenType)), request);
            }
            // ensure that a SecurityContext is present in the message
            ServiceSecurityContext clientContext;
            SecurityMessageProperty securityProperty = request.Properties.Security;
            if (securityProperty != null)
            {
                clientContext = securityProperty.ServiceSecurityContext;
            }
            else
            {
                clientContext = ServiceSecurityContext.Anonymous;
            }
            if (clientContext == null)
            {
                throw TraceUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.SecurityContextMissing, request.Headers.Action)), request);
            }
            if (currentToken != null)
            {
                // ensure that the same party is renewing the token
                if (!IsSameIdentity(currentToken.AuthorizationPolicies, clientContext))
                {
                    throw TraceUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.WrongIdentityRenewingToken), request);
                }
            }

            // check if the client specified entropy
            WSTrust.Driver.ProcessRstAndIssueKey(rst, null, KeyEntropyMode, SecurityAlgorithmSuite, out int issuedKeySize,
                out byte[] issuerEntropy, out byte[] proofKey, out SecurityToken proofToken);
            SecurityContextSecurityToken newToken;
            DateTime keyEffectiveTime = DateTime.UtcNow;
            DateTime keyExpirationTime = GetKeyExpirationTime(currentToken, keyEffectiveTime);
            ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies = (securityProperty != null) ?
                    CreateSecureConversationPolicies(securityProperty, currentTokenPolicies, keyExpirationTime) : EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance;
            if (currentToken != null)
            {
                newToken = new SecurityContextSecurityToken(currentToken, SecurityUtils.GenerateId(), proofKey,
                    SecurityUtils.GenerateUniqueId(), keyEffectiveTime, keyExpirationTime, authorizationPolicies);
            }
            else
            {
                UniqueId contextId = SecurityUtils.GenerateUniqueId();
                string id = SecurityUtils.GenerateId();
                DateTime tokenEffectiveTime = keyEffectiveTime;
                DateTime tokenExpirationTime = TimeoutHelper.Add(tokenEffectiveTime, _sessionTokenLifetime);
                newToken = new SecurityContextSecurityToken(contextId, id, proofKey, tokenEffectiveTime, tokenExpirationTime, null, keyEffectiveTime,
                    keyExpirationTime, authorizationPolicies);
                if (PreserveBootstrapTokens)
                {
                    newToken.BootstrapMessageProperty = (securityProperty == null) ? null : (SecurityMessageProperty)securityProperty.CreateCopy();
                    SecurityUtils.ErasePasswordInUsernameTokenIfPresent(newToken.BootstrapMessageProperty);
                }
            }

            rstr = new RequestSecurityTokenResponse(_standardsManager)
            {
                Context = rst.Context,
                KeySize = issuedKeySize,
                RequestedUnattachedReference = IssuedSecurityTokenParameters.CreateKeyIdentifierClause(newToken, SecurityTokenReferenceStyle.External),
                RequestedAttachedReference = IssuedSecurityTokenParameters.CreateKeyIdentifierClause(newToken, SecurityTokenReferenceStyle.Internal),
                TokenType = _sctUri,
                RequestedSecurityToken = newToken
            };
            if (issuerEntropy != null)
            {
                rstr.SetIssuerEntropy(issuerEntropy);
                rstr.ComputeKey = true;
            }
            if (proofToken != null)
            {
                rstr.RequestedProofToken = proofToken;
            }
            rstr.SetLifetime(keyEffectiveTime, keyExpirationTime);
            return newToken;
        }

        private static SecurityTokenSpecification GetMatchingEndorsingSct(SecurityContextKeyIdentifierClause sctSkiClause, SecurityMessageProperty supportingTokenProperty)
        {
            if (sctSkiClause == null)
            {
                return null;
            }
            for (int i = 0; i < supportingTokenProperty.IncomingSupportingTokens.Count; ++i)
            {
                if (supportingTokenProperty.IncomingSupportingTokens[i].SecurityTokenAttachmentMode != SecurityTokenAttachmentMode.Endorsing
                    && supportingTokenProperty.IncomingSupportingTokens[i].SecurityTokenAttachmentMode != SecurityTokenAttachmentMode.SignedEndorsing)
                {
                    continue;
                }
                if (supportingTokenProperty.IncomingSupportingTokens[i].SecurityToken is SecurityContextSecurityToken sct && sctSkiClause.Matches(sct.ContextId, sct.KeyGeneration))
                {
                    return supportingTokenProperty.IncomingSupportingTokens[i];
                }
            }
            return null;
        }


        protected virtual Message ProcessRenewRequest(Message request)
        {
            CommunicationObject.ThrowIfClosedOrNotOpen();
            try
            {
                // first verify that the session token being renewed is present as a supportingToken
                SecurityMessageProperty supportingTokenProperty = request.Properties.Security;
                if (supportingTokenProperty == null || !supportingTokenProperty.HasIncomingSupportingTokens)
                {
                    throw TraceUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.RenewSessionMissingSupportingToken), request);
                }

                RequestSecurityToken rst;
                XmlDictionaryReader bodyReader = request.GetReaderAtBodyContents();
                using (bodyReader)
                {
                    rst = StandardsManager.TrustDriver.CreateRequestSecurityToken(bodyReader);
                    request.ReadFromBodyContentsToEnd(bodyReader);
                }
                if (rst.RequestType != StandardsManager.TrustDriver.RequestTypeRenew)
                {
                    throw TraceUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.Format(SR.InvalidRstRequestType, rst.RequestType)), request);
                }
                if (rst.RenewTarget == null)
                {
                    throw TraceUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.NoRenewTargetSpecified), request);
                }
                SecurityContextKeyIdentifierClause sctSkiClause = rst.RenewTarget as SecurityContextKeyIdentifierClause;
                SecurityTokenSpecification sessionToken = GetMatchingEndorsingSct(sctSkiClause, supportingTokenProperty);
                if (sctSkiClause == null || sessionToken == null)
                {
                    throw TraceUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.Format(SR.BadRenewTarget, rst.RenewTarget)), request);
                }
                SecurityContextSecurityToken newToken = IssueToken(rst, request, (SecurityContextSecurityToken)sessionToken.SecurityToken, sessionToken.SecurityTokenPolicies, out RequestSecurityTokenResponse rstr);
                rstr.MakeReadOnly();
                BodyWriter replyMessage = rstr;
                if (StandardsManager.MessageSecurityVersion.TrustVersion == TrustVersion.WSTrust13)
                {
                    List<RequestSecurityTokenResponse> rstrList = new List<RequestSecurityTokenResponse>(1)
                    {
                        rstr
                    };
                    RequestSecurityTokenResponseCollection rstrc = new RequestSecurityTokenResponseCollection(rstrList, StandardsManager);
                    replyMessage = rstrc;
                }
                NotifyOperationCompletion(SecuritySessionOperation.Renew, newToken, (SecurityContextSecurityToken)sessionToken.SecurityToken, request.Headers.ReplyTo);
                Message response = CreateReply(request, RenewResponseAction, replyMessage);

                if (!newToken.IsCookieMode)
                {
                    _issuedTokenCache.AddContext(newToken);
                }
                return response;
            }
            finally
            {
                RemoveCachedTokensIfRequired(request.Properties.Security);
            }
        }

        private static void AddTokenToRemoveIfRequired(SecurityToken token, Collection<SecurityContextSecurityToken> sctsToRemove)
        {
            if (token is SecurityContextSecurityToken sct)
            {
                sctsToRemove.Add(sct);
            }
        }

        internal static void RemoveCachedTokensIfRequired(SecurityMessageProperty security)
        {
            if (security == null)
            {
                return;
            }
            // ILogonTokenCacheManager logonManager = OperationContext.Current.EndpointDispatcher.ChannelDispatcher.Listener.GetProperty<ILogonTokenCacheManager>();

            // Collection<ISecurityContextSecurityTokenCache> sctCaches = OperationContext.Current.EndpointDispatcher.ChannelDispatcher.Channels .GetProperty<Collection<ISecurityContextSecurityTokenCache>>();
            // if ( (sctCaches == null || sctCaches.Count == 0))
            // {
            //     return;
            //  }
            //TODO debug and incorporate above logic
            Collection<ISecurityContextSecurityTokenCache> sctCaches = new Collection<ISecurityContextSecurityTokenCache>();
            Collection<SecurityContextSecurityToken> securityContextTokensToRemove = new Collection<SecurityContextSecurityToken>();
            if (security.ProtectionToken != null)
            {
                AddTokenToRemoveIfRequired(security.ProtectionToken.SecurityToken, securityContextTokensToRemove);
            }
            if (security.InitiatorToken != null)
            {
                AddTokenToRemoveIfRequired(security.InitiatorToken.SecurityToken, securityContextTokensToRemove);
            }
            if (security.HasIncomingSupportingTokens)
            {
                for (int i = 0; i < security.IncomingSupportingTokens.Count; ++i)
                {
                    if (security.IncomingSupportingTokens[i].SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.Endorsing
                        || security.IncomingSupportingTokens[i].SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.SignedEncrypted
                        || security.IncomingSupportingTokens[i].SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.SignedEndorsing)
                    {
                        AddTokenToRemoveIfRequired(security.IncomingSupportingTokens[i].SecurityToken, securityContextTokensToRemove);
                    }
                }
            }
            if (sctCaches != null)
            {
                for (int i = 0; i < securityContextTokensToRemove.Count; ++i)
                {
                    for (int j = 0; j < sctCaches.Count; ++j)
                    {
                        sctCaches[j].RemoveContext(securityContextTokensToRemove[i].ContextId, securityContextTokensToRemove[i].KeyGeneration);
                    }
                }
            }
        }

        protected virtual Message ProcessIssueRequest(Message request)
        {
            try
            {
                RequestSecurityToken rst;
                using (XmlDictionaryReader bodyReader = request.GetReaderAtBodyContents())
                {
                    rst = StandardsManager.TrustDriver.CreateRequestSecurityToken(bodyReader);
                    request.ReadFromBodyContentsToEnd(bodyReader);
                }
                if (rst.RequestType != null && rst.RequestType != StandardsManager.TrustDriver.RequestTypeIssue)
                {
                    throw TraceUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.Format(SR.InvalidRstRequestType, rst.RequestType)), request);
                }
                // echo the AppliesTo in the reply if it is an issue request
                EndpointAddress appliesTo = null;
                DataContractSerializer appliesToSerializer;
                rst.GetAppliesToQName(out string appliesToName, out string appliesToNamespace);
                if (appliesToName == AddressingStrings.EndpointReference && appliesToNamespace == request.Version.Addressing.Namespace)
                {
                    if (request.Version.Addressing == AddressingVersion.WSAddressing10)
                    {
                        appliesToSerializer = DataContractSerializerDefaults.CreateSerializer(typeof(EndpointAddress10), DataContractSerializerDefaults.MaxItemsInObjectGraph);
                        appliesTo = rst.GetAppliesTo<EndpointAddress10>(appliesToSerializer).ToEndpointAddress();
                    }
                    else if (request.Version.Addressing == AddressingVersion.WSAddressingAugust2004)
                    {
                        appliesToSerializer = DataContractSerializerDefaults.CreateSerializer(typeof(EndpointAddressAugust2004), DataContractSerializerDefaults.MaxItemsInObjectGraph);
                        appliesTo = rst.GetAppliesTo<EndpointAddressAugust2004>(appliesToSerializer).ToEndpointAddress();
                    }
                    else
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            new ProtocolException(SR.Format(SR.AddressingVersionNotSupported, request.Version.Addressing)));
                    }
                }
                else
                {
                    appliesTo = null;
                    appliesToSerializer = null;
                }
                if (_shouldMatchRstWithEndpointFilter)
                {
                    SecurityUtils.MatchRstWithEndpointFilter(request, _endpointFilterTable, _listenUri);
                }
                SecurityContextSecurityToken issuedToken = IssueToken(rst, request, null, null, out RequestSecurityTokenResponse rstr);
                if (appliesTo != null)
                {
                    if (request.Version.Addressing == AddressingVersion.WSAddressing10)
                    {
                        rstr.SetAppliesTo<EndpointAddress10>(EndpointAddress10.FromEndpointAddress(appliesTo), appliesToSerializer);
                    }
                    else if (request.Version.Addressing == AddressingVersion.WSAddressingAugust2004)
                    {
                        rstr.SetAppliesTo<EndpointAddressAugust2004>(EndpointAddressAugust2004.FromEndpointAddress(appliesTo), appliesToSerializer);
                    }
                    else
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            new ProtocolException(SR.Format(SR.AddressingVersionNotSupported, request.Version.Addressing)));
                    }
                }
                rstr.MakeReadOnly();
                BodyWriter replyMessage = rstr;
                if (StandardsManager.MessageSecurityVersion.TrustVersion == TrustVersion.WSTrust13)
                {
                    List<RequestSecurityTokenResponse> rstrList = new List<RequestSecurityTokenResponse>(1)
                    {
                        rstr
                    };
                    RequestSecurityTokenResponseCollection rstrc = new RequestSecurityTokenResponseCollection(rstrList, StandardsManager);
                    replyMessage = rstrc;
                }
                NotifyOperationCompletion(SecuritySessionOperation.Issue, issuedToken, null, request.Headers.ReplyTo);
                Message response = CreateReply(request, IssueResponseAction, replyMessage);
                if (!issuedToken.IsCookieMode)
                {
                    _issuedTokenCache.AddContext(issuedToken);
                }
                return response;
            }
            finally
            {
                RemoveCachedTokensIfRequired(request.Properties.Security);
            }
        }

        internal static bool DoesSkiClauseMatchSigningToken(SecurityContextKeyIdentifierClause skiClause, Message request)
        {
            SecurityMessageProperty securityProperty = request.Properties.Security;
            if (securityProperty == null)
            {
                throw TraceUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.SFxSecurityContextPropertyMissingFromRequestMessage), request);
            }
            SecurityContextSecurityToken sct = (securityProperty.ProtectionToken != null) ? (securityProperty.ProtectionToken.SecurityToken as SecurityContextSecurityToken) : null;
            if (sct != null && skiClause.Matches(sct.ContextId, sct.KeyGeneration))
            {
                return true;
            }

            if (securityProperty.HasIncomingSupportingTokens)
            {
                for (int i = 0; i < securityProperty.IncomingSupportingTokens.Count; ++i)
                {
                    if (securityProperty.IncomingSupportingTokens[i].SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.Endorsing)
                    {
                        sct = securityProperty.IncomingSupportingTokens[i].SecurityToken as SecurityContextSecurityToken;
                        if (sct != null && skiClause.Matches(sct.ContextId, sct.KeyGeneration))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
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

        private Message ProcessRequest(Message request)
        {
            SecuritySessionOperation operation = SecuritySessionOperation.None;
            try
            {
                if (request == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(request));
                }
                if (request.Headers.Action == IssueAction.Value)
                {
                    operation = SecuritySessionOperation.Issue;
                    return ProcessIssueRequest(request);
                }
                else if (request.Headers.Action == RenewAction.Value)
                {
                    operation = SecuritySessionOperation.Renew;
                    return ProcessRenewRequest(request);
                }
                else
                {
                    throw TraceUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.Format(SR.InvalidActionForNegotiationMessage, request.Headers.Action)), request);
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                return HandleOperationException(operation, request, e);
            }
        }

        internal SecuritySessionHost RequestSecurityTokenListener { get; private set; }

        private void SetupSessionListener()
        {
            //ChannelBuilder channelBuilder = new ChannelBuilder(this.IssuerBindingContext, false); //TODO addChannelDemuxerIfRequired to true
            //channelBuilder.Binding.Elements.Insert(0, new ReplyAdapterBindingElement());
            //channelBuilder.Binding.Elements.Insert(0, new SecuritySessionAuthenticatorBindingElement(this));

            List<string> supportedMessageActions = new List<string>
            {
                IssueAction.Value,
                RenewAction.Value
            };
            SecurityBindingElement securityBindingElement = IssuerBindingContext.Binding.Elements.Find<SecurityBindingElement>();
            foreach (SecurityTokenParameters stp in new SecurityTokenParametersEnumerable(securityBindingElement))
            {
                if (stp is SecureConversationSecurityTokenParameters)
                {
                    SecureConversationSecurityTokenParameters scstp = (SecureConversationSecurityTokenParameters)stp;
                    if (!scstp.CanRenewSession)
                    {
                        supportedMessageActions.Remove(RenewAction.Value);
                        break;
                    }
                }
            }
            MessageFilter issueAndRenewFilter = new SessionActionFilter(_standardsManager, supportedMessageActions.ToArray());
            SecuritySessionHost sessionListener = new SecuritySessionHost(this, issueAndRenewFilter, ListenUri);
            RequestSecurityTokenListener = sessionListener;
        }


        internal void BuildResponderChannelListener<TChannel>(BindingContext context, SecurityServiceDispatcher securityServiceDispatcher)
            where TChannel : class, IChannel
        {
            SecurityCredentialsManager securityCredentials = IssuerBindingContext.BindingParameters.Find<SecurityCredentialsManager>();
            if (securityCredentials == null)
            {
                securityCredentials = ServiceCredentials.CreateDefaultCredentials();
            }
            _bootstrapSecurityBindingElement.ReaderQuotas = IssuerBindingContext.GetInnerProperty<XmlDictionaryReaderQuotas>();
            if (_bootstrapSecurityBindingElement.ReaderQuotas == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.EncodingBindingElementDoesNotHandleReaderQuotas));
            }

            TransportBindingElement transportBindingElement = context.RemainingBindingElements.Find<TransportBindingElement>();
            if (transportBindingElement != null)
            {
                _bootstrapSecurityBindingElement.MaxReceivedMessageSize = transportBindingElement.MaxReceivedMessageSize;
            }

            SecurityProtocolFactory bootstrapSecurityProtocolFactory = _bootstrapSecurityBindingElement.CreateSecurityProtocolFactory<TChannel>(IssuerBindingContext.Clone(), securityCredentials, true, IssuerBindingContext.Clone());
            if (bootstrapSecurityProtocolFactory is MessageSecurityProtocolFactory)
            {
                MessageSecurityProtocolFactory soapBindingFactory = (MessageSecurityProtocolFactory)bootstrapSecurityProtocolFactory;
                soapBindingFactory.ApplyConfidentiality = soapBindingFactory.ApplyIntegrity
                = soapBindingFactory.RequireConfidentiality = soapBindingFactory.RequireIntegrity = true;

                soapBindingFactory.ProtectionRequirements.IncomingSignatureParts.ChannelParts.IsBodyIncluded = true;
                soapBindingFactory.ProtectionRequirements.OutgoingSignatureParts.ChannelParts.IsBodyIncluded = true;

                MessagePartSpecification bodyPart = new MessagePartSpecification(true);
                soapBindingFactory.ProtectionRequirements.OutgoingSignatureParts.AddParts(bodyPart, IssueResponseAction);
                soapBindingFactory.ProtectionRequirements.OutgoingEncryptionParts.AddParts(bodyPart, IssueResponseAction);
                soapBindingFactory.ProtectionRequirements.OutgoingSignatureParts.AddParts(bodyPart, RenewResponseAction);
                soapBindingFactory.ProtectionRequirements.OutgoingEncryptionParts.AddParts(bodyPart, RenewResponseAction);

                soapBindingFactory.ProtectionRequirements.IncomingSignatureParts.AddParts(bodyPart, IssueAction);
                soapBindingFactory.ProtectionRequirements.IncomingEncryptionParts.AddParts(bodyPart, IssueAction);
                soapBindingFactory.ProtectionRequirements.IncomingSignatureParts.AddParts(bodyPart, RenewAction);
                soapBindingFactory.ProtectionRequirements.IncomingEncryptionParts.AddParts(bodyPart, RenewAction);
            }

            SupportingTokenParameters renewSupportingTokenParameters = new SupportingTokenParameters();
            SecurityContextSecurityTokenParameters sctParameters = new SecurityContextSecurityTokenParameters
            {
                RequireDerivedKeys = IssuedSecurityTokenParameters.RequireDerivedKeys
            };
            renewSupportingTokenParameters.Endorsing.Add(sctParameters);
            bootstrapSecurityProtocolFactory.SecurityBindingElement.OperationSupportingTokenParameters.Add(RenewAction.Value, renewSupportingTokenParameters);
            bootstrapSecurityProtocolFactory.SecurityTokenManager = new SessionRenewSecurityTokenManager(bootstrapSecurityProtocolFactory.SecurityTokenManager, SessionTokenAuthenticator, (SecurityTokenResolver)IssuedTokenCache);

            //We are passing as arguments to use existing dispatcher instead of creating another forwarding dispatcher
            // SecurityChannelListener<TChannel> securityChannelListener = new SecurityChannelListener<TChannel>(
            //     this.bootstrapSecurityBindingElement, this.IssuerBindingContext);
            securityServiceDispatcher.SecurityProtocolFactory = bootstrapSecurityProtocolFactory;
            securityServiceDispatcher.SendUnsecuredFaults = true;
            if (bootstrapSecurityProtocolFactory.ListenUri == null)
                bootstrapSecurityProtocolFactory.ListenUri = _listenUri;
            bootstrapSecurityProtocolFactory.OpenAsync(ServiceDefaults.OpenTimeout);
            //TODO if/when we add support for composite duplex bindings, this will need to be false if the binding is a composite duplex binding.
            //securityServiceDispatcher.SendUnsecuredFaults = !SecurityUtils.IsCompositeDuplexBinding(context);

            // ChannelBuilder channelBuilder = new ChannelBuilder(context, true);
            // securityChannelListener.InitializeListener(channelBuilder);
            _shouldMatchRstWithEndpointFilter = SecurityUtils.ShouldMatchRstWithEndpointFilter(_bootstrapSecurityBindingElement);
            //return securityChannelListener;
        }

        internal class SecuritySessionHost //: ServiceHostBase
        {
            //ChannelBuilder channelBuilder;
            private readonly MessageFilter _filter;
            private readonly Uri _listenUri;
            private readonly SecuritySessionSecurityTokenAuthenticator _authenticator;

            public SecuritySessionHost(SecuritySessionSecurityTokenAuthenticator authenticator, MessageFilter filter, Uri listenUri)//, ChannelBuilder channelBuilder)
            {
                _authenticator = authenticator;
                _filter = filter;
                _listenUri = listenUri;
            }

            internal ChannelDispatcher InitializeRuntime(SecurityServiceDispatcher securityDispatcher)
            {
                if (securityDispatcher.AcceptorChannelType.Equals(typeof(IReplyChannel)))
                {
                    return InitializeRuntime<IReplyChannel>(securityDispatcher);
                }

                if (securityDispatcher.AcceptorChannelType.Equals(typeof(IDuplexSessionChannel)))
                {
                    return InitializeRuntime<IDuplexSessionChannel>(securityDispatcher);
                }

                throw new NotImplementedException();
            }

            internal ChannelDispatcher InitializeRuntime<TChannel>(SecurityServiceDispatcher securityDispatcher) where TChannel : class, IChannel
            {
                MessageFilter contractFilter = _filter;
                int filterPriority = int.MaxValue - 10;
                List<Type> endpointChannelTypes = new List<Type> {  typeof(IReplyChannel),
                                                           typeof(IDuplexChannel),
                                                           typeof(IReplySessionChannel),
                                                           typeof(IDuplexSessionChannel) };
                
                //  IChannelListener listener = null;
                //  BindingParameterCollection parameters = new BindingParameterCollection(this.channelBuilder.BindingParameters);
                //  Binding binding = this.channelBuilder.Binding;
                //  binding.ReceiveTimeout = this.authenticator.NegotiationTimeout;
                // parameters.Add(new ChannelDemuxerFilter(contractFilter, filterPriority)); 
                // DispatcherBuilder.MaybeCreateListener(true, endpointChannelTypes, binding, parameters,
                //                                      this.listenUri, "", ListenUriMode.Explicit, this.ServiceThrottle, out listener);
                //   if (listener == null)
                // {
                //      throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.CannotCreateTwoWayListenerForNegotiation)));
                //  }

                //Replacing above code by below 3 lines.(adding securityservicedispatcher to demuxer, how to respond)

                Binding binding = _authenticator.IssuerBindingContext.Binding;
                binding.ReceiveTimeout = _authenticator.NegotiationTimeout;
                securityDispatcher.ChannelBuilder.AddServiceDispatcher<TChannel>(securityDispatcher, new ChannelDemuxerFilter(contractFilter, filterPriority));

                //Injecting here the BuildResponderChannelListener
                _authenticator.BuildResponderChannelListener<TChannel>(_authenticator.IssuerBindingContext, securityDispatcher);
                //end

                var bindingQname = new XmlQualifiedName(binding.Name, binding.Namespace);
                var channelDispatcher = new ChannelDispatcher(_listenUri, binding, bindingQname.ToString(), binding, endpointChannelTypes)
                {
                    MessageVersion = binding.MessageVersion,
                    ManualAddressing = true
                };
                //  channelDispatcher.ServiceThrottle = new ServiceThrottle(this);
                //  channelDispatcher.ServiceThrottle.MaxConcurrentCalls = this.authenticator.MaximumConcurrentNegotiations;
                //  channelDispatcher.ServiceThrottle.MaxConcurrentSessions = this.authenticator.MaximumConcurrentNegotiations;

                EndpointDispatcher endpointDispatcher = new EndpointDispatcher(new EndpointAddress(_listenUri), "IssueAndRenewSession", NamingHelper.DefaultNamespace, true);
                endpointDispatcher.DispatchRuntime.SingletonInstanceContext = new InstanceContext(null, _authenticator, false);
                endpointDispatcher.DispatchRuntime.ConcurrencyMode = ConcurrencyMode.Multiple;
                endpointDispatcher.AddressFilter = new MatchAllMessageFilter();
                endpointDispatcher.ContractFilter = contractFilter;
                endpointDispatcher.FilterPriority = filterPriority;
                endpointDispatcher.DispatchRuntime.PrincipalPermissionMode = PrincipalPermissionMode.None;
                endpointDispatcher.DispatchRuntime.InstanceContextProvider = new SingletonInstanceContextProvider(endpointDispatcher.DispatchRuntime);
                endpointDispatcher.DispatchRuntime.SynchronizationContext = null;

                if (_authenticator.IssuerBindingContext != null && _authenticator.IssuerBindingContext.BindingParameters != null)
                {
                    ServiceAuthenticationManager serviceAuthenticationManager = _authenticator.IssuerBindingContext.BindingParameters.Find<ServiceAuthenticationManager>();
                    if (serviceAuthenticationManager != null)
                    {
                        endpointDispatcher.DispatchRuntime.ServiceAuthenticationManager = new SCTServiceAuthenticationManagerWrapper(serviceAuthenticationManager);
                    }
                }

                DispatchOperation operation = new DispatchOperation(endpointDispatcher.DispatchRuntime, "*", MessageHeaders.WildcardAction, MessageHeaders.WildcardAction)
                {
                    Formatter = new MessageOperationFormatter(),
                    Invoker = new SecuritySessionAuthenticatorInvoker(_authenticator)
                };

                endpointDispatcher.DispatchRuntime.UnhandledDispatchOperation = operation;
                channelDispatcher.Endpoints.Add(endpointDispatcher);
                channelDispatcher.Init();
                Task openTask = channelDispatcher.OpenAsync();
                Fx.Assert(openTask.IsCompleted, "ChannelDispatcher should open synchronously");
                openTask.GetAwaiter().GetResult();
                return channelDispatcher;
            }

            private class SecuritySessionAuthenticatorInvoker : IOperationInvoker
            {
                private readonly SecuritySessionSecurityTokenAuthenticator _parent;

                internal SecuritySessionAuthenticatorInvoker(SecuritySessionSecurityTokenAuthenticator parent)
                {
                    _parent = parent;
                }

                public bool IsSynchronous { get { return true; } }

                public object[] AllocateInputs()
                {
                    return EmptyArray<object>.Allocate(1);
                }

                public ValueTask<(object returnValue, object[] outputs)> InvokeAsync(object instance, object[] inputs)
                {
                    object[] outputs = EmptyArray<object>.Allocate(0);
                    if (!(inputs[0] is Message message))
                    {
                        return new ValueTask<(object returnValue, object[] outputs)>(((object)null, outputs));
                    }
                    object returnVal = _parent.ProcessRequest(message);
                    return new ValueTask<(object returnValue, object[] outputs)>((returnVal, outputs));
                }
            }
        }

        private class SecuritySessionAuthenticatorBindingElement : BindingElement
        {
            private readonly SecuritySessionSecurityTokenAuthenticator _authenticator;

            public SecuritySessionAuthenticatorBindingElement(SecuritySessionSecurityTokenAuthenticator authenticator)
            {
                _authenticator = authenticator;
            }

            public override BindingElement Clone()
            {
                return new SecuritySessionAuthenticatorBindingElement(_authenticator);
            }

            public override T GetProperty<T>(BindingContext context)
            {
                if (typeof(T) == typeof(ISecurityCapabilities))
                {
                    return (T)(object)_authenticator.BootstrapSecurityBindingElement.GetProperty<ISecurityCapabilities>(context);
                }

                return context.GetInnerProperty<T>();
            }
        }

        public class SessionRenewSecurityTokenManager : SecurityTokenManager
        {
            private readonly SecurityTokenManager _innerTokenManager;
            private readonly SecurityTokenAuthenticator _renewTokenAuthenticator;
            private readonly SecurityTokenResolver _renewTokenResolver;

            public SessionRenewSecurityTokenManager(SecurityTokenManager innerTokenManager, SecurityTokenAuthenticator renewTokenAuthenticator,
                SecurityTokenResolver renewTokenResolver)
            {
                _innerTokenManager = innerTokenManager;
                _renewTokenAuthenticator = renewTokenAuthenticator;
                _renewTokenResolver = renewTokenResolver;
            }

            public override SecurityTokenAuthenticator CreateSecurityTokenAuthenticator(SecurityTokenRequirement tokenRequirement, out SecurityTokenResolver outOfBandTokenResolver)
            {
                if (tokenRequirement == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenRequirement));
                }

                if (tokenRequirement.TokenType == ServiceModelSecurityTokenTypes.SecurityContext)
                {
                    outOfBandTokenResolver = _renewTokenResolver;
                    return _renewTokenAuthenticator;
                }
                else
                {
                    return _innerTokenManager.CreateSecurityTokenAuthenticator(tokenRequirement, out outOfBandTokenResolver);
                }
            }

            public override SecurityTokenProvider CreateSecurityTokenProvider(SecurityTokenRequirement requirement)
            {
                return _innerTokenManager.CreateSecurityTokenProvider(requirement);
            }

            public override SecurityTokenSerializer CreateSecurityTokenSerializer(SecurityTokenVersion version)
            {
                return _innerTokenManager.CreateSecurityTokenSerializer(version);
            }
        }
    }

    internal class SessionActionFilter : HeaderFilter
    {
        private readonly SecurityStandardsManager _standardsManager;
        private readonly string[] _actions;

        public SessionActionFilter(SecurityStandardsManager standardsManager, params string[] actions)
        {
            _actions = actions;
            _standardsManager = standardsManager;
        }

        public override bool Match(Message message)
        {
            for (int i = 0; i < _actions.Length; ++i)
            {
                if (message.Headers.Action == _actions[i])
                {
                    return _standardsManager.DoesMessageContainSecurityHeader(message);
                }
            }
            return false;
        }
    }
}
