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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace CoreWCF.Security
{
    internal class SecuritySessionSecurityTokenAuthenticator  : CommunicationObjectSecurityTokenAuthenticator, IIssuanceSecurityTokenAuthenticator //, ILogonTokenCacheManager
    {
        internal static readonly TimeSpan defaultSessionTokenLifetime = TimeSpan.MaxValue;
        internal const int defaultMaxCachedSessionTokens = Int32.MaxValue;  
        internal static readonly SecurityStandardsManager defaultStandardsManager = SecurityStandardsManager.DefaultInstance;
        private bool isClientAnonymous;
        private TimeSpan sessionTokenLifetime;
        private ISecurityContextSecurityTokenCache issuedTokenCache;
        private SecurityBindingElement bootstrapSecurityBindingElement;
        private BindingContext issuerBindingContext;
        private SecurityStandardsManager standardsManager;
        private SecurityAlgorithmSuite securityAlgorithmSuite;
        private SecurityKeyEntropyMode keyEntropyMode;
        private TimeSpan keyRenewalInterval;
        private SecurityTokenParameters issuedTokenParameters;
        private Uri listenUri;
        private string sctUri;
        private IMessageFilterTable<EndpointAddress> endpointFilterTable;
        private bool shouldMatchRstWithEndpointFilter;
        private int maximumConcurrentNegotiations;
        private TimeSpan negotiationTimeout;
        private Object thisLock = new Object();
        private IssuedSecurityTokenHandler issuedSecurityTokenHandler;

        public SecuritySessionSecurityTokenAuthenticator()
        {
            this.SessionTokenAuthenticator = new SecurityContextSecurityTokenAuthenticator();
            this.sessionTokenLifetime = defaultSessionTokenLifetime;
            this.isClientAnonymous = false;
            this.standardsManager = defaultStandardsManager;
            this.keyEntropyMode = SecurityKeyEntropyMode.CombinedEntropy;// AcceleratedTokenProvider.defaultKeyEntropyMode;
            this.maximumConcurrentNegotiations = 128;// AcceleratedTokenAuthenticator.defaultServerMaxActiveNegotiations;
            this.negotiationTimeout =  TimeSpan.Parse("00:01:00", CultureInfo.InvariantCulture); // AcceleratedTokenAuthenticator.defaultServerMaxNegotiationLifetime;
        }

        public IssuedSecurityTokenHandler IssuedSecurityTokenHandler
        {
            get
            {
                return this.issuedSecurityTokenHandler;
            }
            set
            {
                this.issuedSecurityTokenHandler = value;
            }
        }

        public RenewedSecurityTokenHandler RenewedSecurityTokenHandler { get; set; }

        public SecurityServiceDispatcher SecurityServiceDispatcher { get; set; }

        public SecurityAlgorithmSuite SecurityAlgorithmSuite
        {
            get
            {
                return this.securityAlgorithmSuite;
            }
            set
            {
                this.CommunicationObject.ThrowIfDisposedOrImmutable();
                this.securityAlgorithmSuite = value;
            }
        }

        public SecurityKeyEntropyMode KeyEntropyMode
        {
            get
            {
                return this.keyEntropyMode;
            }
            set
            {
                this.CommunicationObject.ThrowIfDisposedOrImmutable();
                SecurityKeyEntropyModeHelper.Validate(value);
                this.keyEntropyMode = value;
            }
        }

        public bool IsClientAnonymous
        {
            get
            {
                return this.isClientAnonymous;
            }
            set
            {
                this.CommunicationObject.ThrowIfDisposedOrImmutable();
                this.isClientAnonymous = value;
            }
        }

        public TimeSpan SessionTokenLifetime
        {
            get
            {
                return this.sessionTokenLifetime;
            }
            set
            {
                this.CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.SFxTimeoutOutOfRangeTooBig));
                }
                this.sessionTokenLifetime = value;
            }
        }

        public TimeSpan KeyRenewalInterval
        {
            get
            {
                return this.keyRenewalInterval;
            }
            set
            {
                this.CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.SFxTimeoutOutOfRangeTooBig));
                }
                this.keyRenewalInterval = value;
            }
        }

        public int MaximumConcurrentNegotiations
        {
            get
            {
                return this.maximumConcurrentNegotiations;
            }
            set
            {
                this.CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.ValueMustBeNonNegative));
                }
                this.maximumConcurrentNegotiations = value;
            }
        }

        public TimeSpan NegotiationTimeout
        {
            get
            {
                return this.negotiationTimeout;
            }
            set
            {
                this.CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                this.negotiationTimeout = value;
            }
        }

        public SecurityContextSecurityTokenAuthenticator SessionTokenAuthenticator { get; }

        public ISecurityContextSecurityTokenCache IssuedTokenCache
        {
            get
            {
                return this.issuedTokenCache;
            }
            set
            {
                this.CommunicationObject.ThrowIfDisposedOrImmutable();
                this.issuedTokenCache = value;
            }
        }

        public SecurityStandardsManager StandardsManager
        {
            get
            {
                return this.standardsManager;
            }
            set
            {
                this.CommunicationObject.ThrowIfDisposedOrImmutable();
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
                this.standardsManager = value;
            }
        }

        public SecurityTokenParameters IssuedSecurityTokenParameters
        {
            get
            {
                return this.issuedTokenParameters;
            }
            set
            {
                this.CommunicationObject.ThrowIfDisposedOrImmutable();
                this.issuedTokenParameters = value;
            }
        }

        public BindingContext IssuerBindingContext
        {
            get
            {
                return this.issuerBindingContext;
            }
            set
            {
                this.CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }
                this.issuerBindingContext = value.Clone();
            }
        }

        public SecurityBindingElement BootstrapSecurityBindingElement
        {
            get { return this.bootstrapSecurityBindingElement; }
            set
            {
                this.CommunicationObject.ThrowIfDisposedOrImmutable();
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }
                this.bootstrapSecurityBindingElement = (SecurityBindingElement)value.Clone();
            }
        }

        public IMessageFilterTable<EndpointAddress> EndpointFilterTable
        {
            get
            {
                return this.endpointFilterTable;
            }
            set
            {
                this.CommunicationObject.ThrowIfDisposedOrImmutable();
                this.endpointFilterTable = value;
            }
        }

        public Uri ListenUri
        {
            get { return this.listenUri; }
            set
            {
                this.CommunicationObject.ThrowIfDisposedOrImmutable();
                this.listenUri = value;
            }
        }

        public virtual XmlDictionaryString IssueAction
        {
            get
            {
                return standardsManager.SecureConversationDriver.IssueAction;
            }
        }

        public virtual XmlDictionaryString IssueResponseAction
        {
            get
            {
                return standardsManager.SecureConversationDriver.IssueResponseAction;
            }
        }

        public bool PreserveBootstrapTokens { get; set; }

        public virtual XmlDictionaryString RenewAction
        {
            get
            {
                return standardsManager.SecureConversationDriver.RenewAction;
            }
        }

        public virtual XmlDictionaryString RenewResponseAction
        {
            get
            {
                return standardsManager.SecureConversationDriver.RenewResponseAction;
            }
        }

        public virtual XmlDictionaryString CloseAction
        {
            get
            {
                return standardsManager.SecureConversationDriver.CloseAction;
            }
        }

        public virtual XmlDictionaryString CloseResponseAction
        {
            get
            {
                return standardsManager.SecureConversationDriver.CloseResponseAction;
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
                if (this.issuedSecurityTokenHandler != null)
                {
                    this.issuedSecurityTokenHandler(newSessionToken, remoteAddress);
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.IssueSessionTokenHandlerNotSet));
                }
            }
            else if (operation == SecuritySessionOperation.Renew)
            {
                if (this.RenewedSecurityTokenHandler != null)
                {
                    this.RenewedSecurityTokenHandler(newSessionToken, previousSessionToken);
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
            if (this.BootstrapSecurityBindingElement == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.BootstrapSecurityBindingElementNotSet, this.GetType())));
            }
            if (this.IssuerBindingContext == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.IssuerBuildContextNotSet, this.GetType())));
            }
            if (this.IssuedSecurityTokenParameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.IssuedSecurityTokenParametersNotSet, this.GetType())));
            }
            if (this.SecurityAlgorithmSuite == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecurityAlgorithmSuiteNotSet, this.GetType())));
            }
            if (this.IssuedTokenCache == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.IssuedTokenCacheNotSet, this.GetType())));
            }
            //TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            SetupSessionListener();
            ChannelDispatcher channelDispatcher = RequestSecurityTokenListener.InitializeRuntime(this.SecurityServiceDispatcher);
            this.SecurityServiceDispatcher.SecurityAuthServiceDispatcher = new ServiceDispatcher(channelDispatcher);
            this.sctUri = this.StandardsManager.SecureConversationDriver.TokenTypeUri;
            return base.OpenAsync();
           // base.OnOpen(timeoutHelper.RemainingTime());
        }

        protected override bool CanValidateTokenCore(SecurityToken token)
        {
            return (token is SecurityContextSecurityToken);
        }

        protected override ReadOnlyCollection<IAuthorizationPolicy> ValidateTokenCore(SecurityToken token)
        {
            SecurityContextSecurityToken sct = (SecurityContextSecurityToken)token;
            return sct.AuthorizationPolicies;
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
            DateTime keyExpirationTime = TimeoutHelper.Add(keyEffectiveTime, this.keyRenewalInterval);
            DateTime tokenExpirationTime = (currentToken != null) ? currentToken.ValidTo : TimeoutHelper.Add(keyEffectiveTime, this.sessionTokenLifetime);
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("security");
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
            if (rst.TokenType != null && rst.TokenType != this.sctUri)
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
            byte[] proofKey;
            byte[] issuerEntropy;
            int issuedKeySize;
            SecurityToken proofToken;
            WSTrust.Driver.ProcessRstAndIssueKey(rst, null, this.KeyEntropyMode, this.SecurityAlgorithmSuite, out issuedKeySize,
                out issuerEntropy, out proofKey, out proofToken);
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
                DateTime tokenExpirationTime = TimeoutHelper.Add(tokenEffectiveTime, this.sessionTokenLifetime);
                newToken = new SecurityContextSecurityToken(contextId, id, proofKey, tokenEffectiveTime, tokenExpirationTime, null, keyEffectiveTime,
                    keyExpirationTime, authorizationPolicies);
                if (this.PreserveBootstrapTokens)
                {
                    newToken.BootstrapMessageProperty = (securityProperty == null) ? null : (SecurityMessageProperty)securityProperty.CreateCopy();
                    SecurityUtils.ErasePasswordInUsernameTokenIfPresent(newToken.BootstrapMessageProperty);
                }
            }

            rstr = new RequestSecurityTokenResponse(this.standardsManager);
            rstr.Context = rst.Context;
            rstr.KeySize = issuedKeySize;
            rstr.RequestedUnattachedReference = this.IssuedSecurityTokenParameters.CreateKeyIdentifierClause(newToken, SecurityTokenReferenceStyle.External);
            rstr.RequestedAttachedReference = this.IssuedSecurityTokenParameters.CreateKeyIdentifierClause(newToken, SecurityTokenReferenceStyle.Internal);
            rstr.TokenType = this.sctUri;
            rstr.RequestedSecurityToken = newToken;
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
                SecurityContextSecurityToken sct = supportingTokenProperty.IncomingSupportingTokens[i].SecurityToken as SecurityContextSecurityToken;
                if (sct != null && sctSkiClause.Matches(sct.ContextId, sct.KeyGeneration))
                {
                    return supportingTokenProperty.IncomingSupportingTokens[i];
                }
            }
            return null;
        }

        
        protected virtual Message ProcessRenewRequest(Message request)
        {
            this.CommunicationObject.ThrowIfClosedOrNotOpen();
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
                    rst = this.StandardsManager.TrustDriver.CreateRequestSecurityToken(bodyReader);
                    request.ReadFromBodyContentsToEnd(bodyReader);
                }
                if (rst.RequestType != this.StandardsManager.TrustDriver.RequestTypeRenew)
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
                RequestSecurityTokenResponse rstr;
                SecurityContextSecurityToken newToken = this.IssueToken(rst, request, (SecurityContextSecurityToken)sessionToken.SecurityToken, sessionToken.SecurityTokenPolicies, out rstr);
                rstr.MakeReadOnly();
                BodyWriter replyMessage = rstr;
                if (this.StandardsManager.MessageSecurityVersion.TrustVersion == TrustVersion.WSTrust13)
                {
                    List<RequestSecurityTokenResponse> rstrList = new List<RequestSecurityTokenResponse>(1);
                    rstrList.Add(rstr);
                    RequestSecurityTokenResponseCollection rstrc = new RequestSecurityTokenResponseCollection(rstrList, this.StandardsManager);
                    replyMessage = rstrc;
                }
                this.NotifyOperationCompletion(SecuritySessionOperation.Renew, newToken, (SecurityContextSecurityToken)sessionToken.SecurityToken, request.Headers.ReplyTo);
                Message response = CreateReply(request, this.RenewResponseAction, replyMessage);

                if (!newToken.IsCookieMode)
                {
                    this.issuedTokenCache.AddContext(newToken);
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
            SecurityContextSecurityToken sct = token as SecurityContextSecurityToken;
            if (sct != null)
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
                    rst = this.StandardsManager.TrustDriver.CreateRequestSecurityToken(bodyReader);
                    request.ReadFromBodyContentsToEnd(bodyReader);
                }
                if (rst.RequestType != null && rst.RequestType != this.StandardsManager.TrustDriver.RequestTypeIssue)
                {
                    throw TraceUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.Format(SR.InvalidRstRequestType, rst.RequestType)), request);
                }
                // echo the AppliesTo in the reply if it is an issue request
                EndpointAddress appliesTo = null;
                DataContractSerializer appliesToSerializer;
                string appliesToName;
                string appliesToNamespace;
                rst.GetAppliesToQName(out appliesToName, out appliesToNamespace);
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
                if (this.shouldMatchRstWithEndpointFilter)
                {
                    SecurityUtils.MatchRstWithEndpointFilter(request, this.endpointFilterTable, this.listenUri);
                }
                RequestSecurityTokenResponse rstr;
                SecurityContextSecurityToken issuedToken = this.IssueToken(rst, request, null, null, out rstr);
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
                if (this.StandardsManager.MessageSecurityVersion.TrustVersion == TrustVersion.WSTrust13)
                {
                    List<RequestSecurityTokenResponse> rstrList = new List<RequestSecurityTokenResponse>(1);
                    rstrList.Add(rstr);
                    RequestSecurityTokenResponseCollection rstrc = new RequestSecurityTokenResponseCollection(rstrList, this.StandardsManager);
                    replyMessage = rstrc;
                }
                this.NotifyOperationCompletion(SecuritySessionOperation.Issue, issuedToken, null, request.Headers.ReplyTo);
                Message response = CreateReply(request, this.IssueResponseAction, replyMessage);
                if (!issuedToken.IsCookieMode)
                {
                    this.issuedTokenCache.AddContext(issuedToken);
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
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("request");
                }
                if (request.Headers.Action == this.IssueAction.Value)
                {
                    operation = SecuritySessionOperation.Issue;
                    return this.ProcessIssueRequest(request);
                }
                else if (request.Headers.Action == this.RenewAction.Value)
                {
                    operation = SecuritySessionOperation.Renew;
                    return this.ProcessRenewRequest(request);
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

                return this.HandleOperationException(operation, request, e);
            }
        }

        internal SecuritySessionHost RequestSecurityTokenListener { get; private set; }

        private void SetupSessionListener()
        {
            //ChannelBuilder channelBuilder = new ChannelBuilder(this.IssuerBindingContext, false); //TODO addChannelDemuxerIfRequired to true
            //channelBuilder.Binding.Elements.Insert(0, new ReplyAdapterBindingElement());
            //channelBuilder.Binding.Elements.Insert(0, new SecuritySessionAuthenticatorBindingElement(this));

           List<string> supportedMessageActions = new List<string>();
           supportedMessageActions.Add(this.IssueAction.Value);
           supportedMessageActions.Add(this.RenewAction.Value);
           SecurityBindingElement securityBindingElement = this.IssuerBindingContext.Binding.Elements.Find<SecurityBindingElement>();
            foreach (SecurityTokenParameters stp in new SecurityTokenParametersEnumerable(securityBindingElement))
            {
                if (stp is SecureConversationSecurityTokenParameters)
                {
                    SecureConversationSecurityTokenParameters scstp = (SecureConversationSecurityTokenParameters)stp;
                    if (!scstp.CanRenewSession)
                    {
                        supportedMessageActions.Remove(this.RenewAction.Value);
                        break;
                    }
                }
            }
            MessageFilter issueAndRenewFilter = new SessionActionFilter(this.standardsManager, supportedMessageActions.ToArray());
            SecuritySessionHost sessionListener = new SecuritySessionHost(this, issueAndRenewFilter, this.ListenUri);
            this.RequestSecurityTokenListener = sessionListener;
        }

        
        internal void BuildResponderChannelListener<TChannel>(BindingContext context, SecurityServiceDispatcher securityServiceDispatcher)
            where TChannel : class, IChannel
        {
            SecurityCredentialsManager securityCredentials = this.IssuerBindingContext.BindingParameters.Find<SecurityCredentialsManager>();
            if (securityCredentials == null)
            {
                securityCredentials = ServiceCredentials.CreateDefaultCredentials();
            }
            this.bootstrapSecurityBindingElement.ReaderQuotas = this.IssuerBindingContext.GetInnerProperty<XmlDictionaryReaderQuotas>();
            if (this.bootstrapSecurityBindingElement.ReaderQuotas == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.EncodingBindingElementDoesNotHandleReaderQuotas));
            }

            TransportBindingElement transportBindingElement = context.RemainingBindingElements.Find<TransportBindingElement>();
            if (transportBindingElement != null)
                this.bootstrapSecurityBindingElement.MaxReceivedMessageSize = transportBindingElement.MaxReceivedMessageSize;

            SecurityProtocolFactory bootstrapSecurityProtocolFactory = this.bootstrapSecurityBindingElement.CreateSecurityProtocolFactory<TChannel>(this.IssuerBindingContext.Clone(), securityCredentials, true, this.IssuerBindingContext.Clone());
            if (bootstrapSecurityProtocolFactory is MessageSecurityProtocolFactory)
            {
                MessageSecurityProtocolFactory soapBindingFactory = (MessageSecurityProtocolFactory)bootstrapSecurityProtocolFactory;
                soapBindingFactory.ApplyConfidentiality = soapBindingFactory.ApplyIntegrity
                = soapBindingFactory.RequireConfidentiality = soapBindingFactory.RequireIntegrity = true;

                soapBindingFactory.ProtectionRequirements.IncomingSignatureParts.ChannelParts.IsBodyIncluded = true;
                soapBindingFactory.ProtectionRequirements.OutgoingSignatureParts.ChannelParts.IsBodyIncluded = true;

                MessagePartSpecification bodyPart = new MessagePartSpecification(true);
                soapBindingFactory.ProtectionRequirements.OutgoingSignatureParts.AddParts(bodyPart, this.IssueResponseAction);
                soapBindingFactory.ProtectionRequirements.OutgoingEncryptionParts.AddParts(bodyPart, this.IssueResponseAction);
                soapBindingFactory.ProtectionRequirements.OutgoingSignatureParts.AddParts(bodyPart, this.RenewResponseAction);
                soapBindingFactory.ProtectionRequirements.OutgoingEncryptionParts.AddParts(bodyPart, this.RenewResponseAction);

                soapBindingFactory.ProtectionRequirements.IncomingSignatureParts.AddParts(bodyPart, this.IssueAction);
                soapBindingFactory.ProtectionRequirements.IncomingEncryptionParts.AddParts(bodyPart, this.IssueAction);
                soapBindingFactory.ProtectionRequirements.IncomingSignatureParts.AddParts(bodyPart, this.RenewAction);
                soapBindingFactory.ProtectionRequirements.IncomingEncryptionParts.AddParts(bodyPart, this.RenewAction);
            }

            SupportingTokenParameters renewSupportingTokenParameters = new SupportingTokenParameters();
            SecurityContextSecurityTokenParameters sctParameters = new SecurityContextSecurityTokenParameters();
            sctParameters.RequireDerivedKeys = this.IssuedSecurityTokenParameters.RequireDerivedKeys;
            renewSupportingTokenParameters.Endorsing.Add(sctParameters);
            bootstrapSecurityProtocolFactory.SecurityBindingElement.OperationSupportingTokenParameters.Add(this.RenewAction.Value, renewSupportingTokenParameters);
            bootstrapSecurityProtocolFactory.SecurityTokenManager = new SessionRenewSecurityTokenManager(bootstrapSecurityProtocolFactory.SecurityTokenManager, this.SessionTokenAuthenticator, (SecurityTokenResolver)this.IssuedTokenCache);

           //We are passing as arguments to use existing dispatcher instead of creating another forwarding dispatcher
           // SecurityChannelListener<TChannel> securityChannelListener = new SecurityChannelListener<TChannel>(
           //     this.bootstrapSecurityBindingElement, this.IssuerBindingContext);
            securityServiceDispatcher.SecurityProtocolFactory = bootstrapSecurityProtocolFactory;
            securityServiceDispatcher.SendUnsecuredFaults = true;
            bootstrapSecurityProtocolFactory.OpenAsync(ServiceDefaults.OpenTimeout);
            //TODO if/when we add support for composite duplex bindings, this will need to be false if the binding is a composite duplex binding.
            //securityServiceDispatcher.SendUnsecuredFaults = !SecurityUtils.IsCompositeDuplexBinding(context);

            // ChannelBuilder channelBuilder = new ChannelBuilder(context, true);
            // securityChannelListener.InitializeListener(channelBuilder);
            this.shouldMatchRstWithEndpointFilter = SecurityUtils.ShouldMatchRstWithEndpointFilter(this.bootstrapSecurityBindingElement);
            //return securityChannelListener;
        }

       internal class SecuritySessionHost //: ServiceHostBase
       {
            //ChannelBuilder channelBuilder;
            private MessageFilter filter;
            private Uri listenUri;
            private SecuritySessionSecurityTokenAuthenticator authenticator;

            public SecuritySessionHost(SecuritySessionSecurityTokenAuthenticator authenticator, MessageFilter filter, Uri listenUri)//, ChannelBuilder channelBuilder)
            {
                this.authenticator = authenticator;
                this.filter = filter;
                this.listenUri = listenUri;
            }

            internal ChannelDispatcher InitializeRuntime(SecurityServiceDispatcher securityDispatcher)
            {
                MessageFilter contractFilter = this.filter;
                int filterPriority = Int32.MaxValue - 10;
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
                Binding binding = this.authenticator.IssuerBindingContext.Binding;
                binding.ReceiveTimeout = this.authenticator.NegotiationTimeout;
                securityDispatcher.ChannelBuilder.AddServiceDispatcher<IReplyChannel>(securityDispatcher, new ChannelDemuxerFilter(contractFilter, filterPriority));

                //Injecting here the BuildResponderChannelListener
                this.authenticator.BuildResponderChannelListener<IReplyChannel>(this.authenticator.IssuerBindingContext, securityDispatcher);
                //end

                var bindingQname = new XmlQualifiedName(binding.Name, binding.Namespace);
                var channelDispatcher = new ChannelDispatcher(listenUri, binding, bindingQname.ToString(), binding, endpointChannelTypes);
                channelDispatcher.MessageVersion = binding.MessageVersion;
                channelDispatcher.ManualAddressing = true;
                //  channelDispatcher.ServiceThrottle = new ServiceThrottle(this);
               //  channelDispatcher.ServiceThrottle.MaxConcurrentCalls = this.authenticator.MaximumConcurrentNegotiations;
              //  channelDispatcher.ServiceThrottle.MaxConcurrentSessions = this.authenticator.MaximumConcurrentNegotiations;

                EndpointDispatcher endpointDispatcher = new EndpointDispatcher(new EndpointAddress(this.listenUri), "IssueAndRenewSession", NamingHelper.DefaultNamespace, true);
                endpointDispatcher.DispatchRuntime.SingletonInstanceContext = new InstanceContext(null, this.authenticator, false);
                endpointDispatcher.DispatchRuntime.ConcurrencyMode = ConcurrencyMode.Multiple;
                endpointDispatcher.AddressFilter = new MatchAllMessageFilter();
                endpointDispatcher.ContractFilter = contractFilter;
                endpointDispatcher.FilterPriority = filterPriority;
                endpointDispatcher.DispatchRuntime.PrincipalPermissionMode = PrincipalPermissionMode.None;
                endpointDispatcher.DispatchRuntime.InstanceContextProvider = new SingletonInstanceContextProvider(endpointDispatcher.DispatchRuntime);
                endpointDispatcher.DispatchRuntime.SynchronizationContext = null;

                if (this.authenticator.IssuerBindingContext != null && this.authenticator.IssuerBindingContext.BindingParameters != null)
                {
                    ServiceAuthenticationManager serviceAuthenticationManager = this.authenticator.IssuerBindingContext.BindingParameters.Find<ServiceAuthenticationManager>();
                    if (serviceAuthenticationManager != null)
                    {
                        endpointDispatcher.DispatchRuntime.ServiceAuthenticationManager = new SCTServiceAuthenticationManagerWrapper(serviceAuthenticationManager);
                    }
                }

                DispatchOperation operation = new DispatchOperation(endpointDispatcher.DispatchRuntime, "*", MessageHeaders.WildcardAction, MessageHeaders.WildcardAction);
                operation.Formatter = new MessageOperationFormatter();
                operation.Invoker = new SecuritySessionAuthenticatorInvoker(this.authenticator);

                endpointDispatcher.DispatchRuntime.UnhandledDispatchOperation = operation;
                channelDispatcher.Endpoints.Add(endpointDispatcher);
                channelDispatcher.Init();
                var openTask = channelDispatcher.OpenAsync();
                Fx.Assert(openTask.IsCompleted, "ChannelDispatcher should open synchronously");
                openTask.GetAwaiter().GetResult();
                return channelDispatcher;
            }

            private class SecuritySessionAuthenticatorInvoker : IOperationInvoker
            {
                private SecuritySessionSecurityTokenAuthenticator parent;

                internal SecuritySessionAuthenticatorInvoker(SecuritySessionSecurityTokenAuthenticator parent)
                {
                    this.parent = parent;
                }

                public bool IsSynchronous { get { return true; } }

                public object[] AllocateInputs()
                {
                    return EmptyArray<object>.Allocate(1);
                }

                public ValueTask<(object returnValue, object[] outputs)> InvokeAsync(object instance, object[] inputs)
                {
                   object[] outputs = EmptyArray<object>.Allocate(0);
                    Message message = inputs[0] as Message;
                    if (message == null)
                    {
                        return new ValueTask<(object returnValue, object[] outputs)>(((object)null, outputs));
                    }
                    object returnVal =  parent.ProcessRequest(message);
                    return new ValueTask<(object returnValue, object[] outputs)>((returnVal, outputs));
                }
            }
        }

        private class SecuritySessionAuthenticatorBindingElement : BindingElement
        {
            private SecuritySessionSecurityTokenAuthenticator authenticator;

            public SecuritySessionAuthenticatorBindingElement(SecuritySessionSecurityTokenAuthenticator authenticator)
            {
                this.authenticator = authenticator;
            }

            public override BindingElement Clone()
            {
                return new SecuritySessionAuthenticatorBindingElement(this.authenticator);
            }

            public override T GetProperty<T>(BindingContext context)
            {
                if (typeof(T) == typeof(ISecurityCapabilities))
                {
                    return (T)(object)authenticator.BootstrapSecurityBindingElement.GetProperty<ISecurityCapabilities>(context);
                }

                return context.GetInnerProperty<T>();
            }
        }

        public class SessionRenewSecurityTokenManager : SecurityTokenManager
        {
            private SecurityTokenManager innerTokenManager;
            private SecurityTokenAuthenticator renewTokenAuthenticator;
            private SecurityTokenResolver renewTokenResolver;

            public SessionRenewSecurityTokenManager(SecurityTokenManager innerTokenManager, SecurityTokenAuthenticator renewTokenAuthenticator,
                SecurityTokenResolver renewTokenResolver)
            {
                this.innerTokenManager = innerTokenManager;
                this.renewTokenAuthenticator = renewTokenAuthenticator;
                this.renewTokenResolver = renewTokenResolver;
            }

            public override SecurityTokenAuthenticator CreateSecurityTokenAuthenticator(SecurityTokenRequirement tokenRequirement, out SecurityTokenResolver outOfBandTokenResolver)
            {
                if (tokenRequirement == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("tokenRequirement");

                if (tokenRequirement.TokenType == ServiceModelSecurityTokenTypes.SecurityContext)
                {
                    outOfBandTokenResolver = this.renewTokenResolver;
                    return this.renewTokenAuthenticator;
                }
                else
                {
                    return this.innerTokenManager.CreateSecurityTokenAuthenticator(tokenRequirement, out outOfBandTokenResolver);
                }
            }

            public override SecurityTokenProvider CreateSecurityTokenProvider(SecurityTokenRequirement requirement)
            {
                return this.innerTokenManager.CreateSecurityTokenProvider(requirement);
            }

            internal override SecurityTokenSerializer CreateSecurityTokenSerializer(SecurityTokenVersion version)
            {
                return this.innerTokenManager.CreateSecurityTokenSerializer(version);
            }

        }
    }

    internal class SessionActionFilter : HeaderFilter
    {
        private SecurityStandardsManager standardsManager;
        private string[] actions;

        public SessionActionFilter(SecurityStandardsManager standardsManager, params string[] actions)
        {
            this.actions = actions;
            this.standardsManager = standardsManager;
        }

        public override bool Match(Message message)
        {
            for (int i = 0; i < this.actions.Length; ++i)
            {
                if (message.Headers.Action == this.actions[i])
                {
                    return this.standardsManager.DoesMessageContainSecurityHeader(message);
                }
            }
            return false;
        }
    }
}
