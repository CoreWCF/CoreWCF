// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal abstract class MessageSecurityProtocol : SecurityProtocol
    {
        private readonly MessageSecurityProtocolFactory factory;
        private SecurityToken identityVerifiedToken; // verified for the readonly target

        protected MessageSecurityProtocol(MessageSecurityProtocolFactory factory, EndpointAddress target, Uri via)
            : base(factory, target, via)
        {
            this.factory = factory;
        }

        // Protocols that have more than one active, identity checked
        // token at any time should override this property and return
        // false
        protected virtual bool CacheIdentityCheckResultForToken
        {
            get { return true; }
        }

        protected virtual bool DoAutomaticEncryptionMatch
        {
            get { return true; }
        }

        protected virtual bool PerformIncomingAndOutgoingMessageExpectationChecks
        {
            get { return true; }
        }

        protected bool RequiresIncomingSecurityProcessing(Message message)
        {
            // if we are receiveing a response that has no security that we should accept this AND no security header exists
            // then it is OK to skip the header.
            if (factory.ActAsInitiator
              && factory.SecurityBindingElement.EnableUnsecuredResponse
              && !factory.StandardsManager.SecurityVersion.DoesMessageContainSecurityHeader(message))
                return false;

            bool requiresAppSecurity = factory.RequireIntegrity || factory.RequireConfidentiality || factory.DetectReplays;
            return requiresAppSecurity || factory.ExpectSupportingTokens;
        }

        protected bool RequiresOutgoingSecurityProcessing
        {
            get
            {
                // If were are the listener, don't apply security if the flag is set
                if (!factory.ActAsInitiator && factory.SecurityBindingElement.EnableUnsecuredResponse)
                    return false;

                bool requiresAppSecurity = factory.ApplyIntegrity || factory.ApplyConfidentiality || factory.AddTimestamp;
                return requiresAppSecurity || factory.ExpectSupportingTokens;
            }
        }

        protected MessageSecurityProtocolFactory MessageSecurityProtocolFactory
        {
            get { return factory; }
        }


        // helper method for attaching the client claims in a symmetric security protocol
        protected void AttachRecipientSecurityProperty(Message message, SecurityToken protectionToken, bool isWrappedToken, IList<SecurityToken> basicTokens, IList<SecurityToken> endorsingTokens,
           IList<SecurityToken> signedEndorsingTokens, IList<SecurityToken> signedTokens, Dictionary<SecurityToken, ReadOnlyCollection<IAuthorizationPolicy>> tokenPoliciesMapping)
        {
            ReadOnlyCollection<IAuthorizationPolicy> protectionTokenPolicies;
            if (isWrappedToken)
            {
                protectionTokenPolicies = EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance;
            }
            else
            {
                protectionTokenPolicies = tokenPoliciesMapping[protectionToken];
            }
            SecurityMessageProperty security = SecurityMessageProperty.GetOrCreate(message);
            security.ProtectionToken = new SecurityTokenSpecification(protectionToken, protectionTokenPolicies);
            AddSupportingTokenSpecification(security, basicTokens, endorsingTokens, signedEndorsingTokens, signedTokens, tokenPoliciesMapping);
            security.ServiceSecurityContext = new ServiceSecurityContext(security.GetInitiatorTokenAuthorizationPolicies());
        }

        // helper method for attaching the server claims in a symmetric security protocol
        protected void DoIdentityCheckAndAttachInitiatorSecurityProperty(Message message, SecurityToken protectionToken, ReadOnlyCollection<IAuthorizationPolicy> protectionTokenPolicies)
        {
            AuthorizationContext protectionAuthContext = EnsureIncomingIdentity(message, protectionToken, protectionTokenPolicies);
            SecurityMessageProperty security = SecurityMessageProperty.GetOrCreate(message);
            security.ProtectionToken = new SecurityTokenSpecification(protectionToken, protectionTokenPolicies);
            security.ServiceSecurityContext = new ServiceSecurityContext(protectionAuthContext, protectionTokenPolicies ?? EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance);
        }

        protected AuthorizationContext EnsureIncomingIdentity(Message message, SecurityToken token, ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies)
        {
            if (token == null)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.NoSigningTokenAvailableToDoIncomingIdentityCheck)), message);
            }
            AuthorizationContext authContext = (authorizationPolicies != null) ? AuthorizationContext.CreateDefaultAuthorizationContext(authorizationPolicies) : null;
            if (factory.IdentityVerifier != null)
            {
                if (Target == null)
                {
                    throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.NoOutgoingEndpointAddressAvailableForDoingIdentityCheckOnReply)), message);
                }

                factory.IdentityVerifier.EnsureIncomingIdentity(Target, authContext);
            }
            return authContext;
        }

        protected void EnsureOutgoingIdentity(SecurityToken token, SecurityTokenAuthenticator authenticator)
        {
            if (object.ReferenceEquals(token, identityVerifiedToken))
            {
                return;
            }
            if (factory.IdentityVerifier == null)
            {
                return;
            }
            if (Target == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.NoOutgoingEndpointAddressAvailableForDoingIdentityCheck)));
            }
            ReadOnlyCollection<IAuthorizationPolicy> authzPolicies = authenticator.ValidateToken(token);
            factory.IdentityVerifier.EnsureOutgoingIdentity(Target, authzPolicies);
            if (CacheIdentityCheckResultForToken)
            {
                identityVerifiedToken = token;
            }
        }

        protected SecurityProtocolCorrelationState GetCorrelationState(SecurityToken correlationToken)
        {
            return new SecurityProtocolCorrelationState(correlationToken);
        }

        protected SecurityProtocolCorrelationState GetCorrelationState(SecurityToken correlationToken, ReceiveSecurityHeader securityHeader)
        {
            SecurityProtocolCorrelationState result = new SecurityProtocolCorrelationState(correlationToken);
            if (securityHeader.MaintainSignatureConfirmationState && !factory.ActAsInitiator)
            {
                result.SignatureConfirmations = securityHeader.GetSentSignatureValues();
            }
            return result;
        }

        protected SecurityToken GetCorrelationToken(SecurityProtocolCorrelationState[] correlationStates)
        {
            SecurityToken token = null;
            if (correlationStates != null)
            {
                for (int i = 0; i < correlationStates.Length; ++i)
                {
                    if (correlationStates[i].Token == null)
                        continue;
                    if (token == null)
                    {
                        token = correlationStates[i].Token;
                    }
                    else if (!object.ReferenceEquals(token, correlationStates[i].Token))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.MultipleCorrelationTokensFound)));
                    }
                }
            }
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.NoCorrelationTokenFound)));
            }
            return token;
        }


        protected SecurityToken GetCorrelationToken(SecurityProtocolCorrelationState correlationState)
        {
            if (correlationState == null || correlationState.Token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.CannotFindCorrelationStateForApplyingSecurity)));
            }
            return correlationState.Token;
        }

        protected static void EnsureNonWrappedToken(SecurityToken token, Message message)
        {
            if (token is WrappedKeySecurityToken)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.TokenNotExpectedInSecurityHeader, token)), message);
            }
        }

        protected SendSecurityHeader ConfigureSendSecurityHeader(Message message, string actor, IList<SupportingTokenSpecification> supportingTokens, SecurityProtocolCorrelationState correlationState)
        {
            MessageSecurityProtocolFactory factory = MessageSecurityProtocolFactory;
            SendSecurityHeader securityHeader = CreateSendSecurityHeader(message, actor, factory);
            securityHeader.SignThenEncrypt = factory.MessageProtectionOrder != MessageProtectionOrder.EncryptBeforeSign;
            // If ProtectTokens is enabled then we make sure that both the client side and the service side sign the primary token 
            // ( if it is an issued token, the check exists in sendsecurityheader)in the primary signature while sending a message.
            securityHeader.ShouldProtectTokens = factory.SecurityBindingElement.ProtectTokens;
            securityHeader.EncryptPrimarySignature = factory.MessageProtectionOrder == MessageProtectionOrder.SignBeforeEncryptAndEncryptSignature;

            if (factory.DoRequestSignatureConfirmation && correlationState != null)
            {
                if (factory.ActAsInitiator)
                {
                    securityHeader.MaintainSignatureConfirmationState = true;
                    securityHeader.CorrelationState = correlationState;
                }
                else if (correlationState.SignatureConfirmations != null)
                {
                    securityHeader.AddSignatureConfirmations(correlationState.SignatureConfirmations);
                }
            }

            string action = message.Headers.Action;
            if (this.factory.ApplyIntegrity)
            {
                securityHeader.SignatureParts = this.factory.GetOutgoingSignatureParts(action);
            }

            if (factory.ApplyConfidentiality)
            {
                securityHeader.EncryptionParts = this.factory.GetOutgoingEncryptionParts(action);
            }
            AddSupportingTokens(securityHeader, supportingTokens);
            return securityHeader;
        }

        protected ReceiveSecurityHeader CreateSecurityHeader(Message message, string actor, MessageDirection transferDirection, SecurityStandardsManager standardsManager)
        {
            standardsManager = standardsManager ?? factory.StandardsManager;
            ReceiveSecurityHeader securityHeader = standardsManager.TryCreateReceiveSecurityHeader(message, actor,
               factory.IncomingAlgorithmSuite, transferDirection);
            securityHeader.Layout = factory.SecurityHeaderLayout;
            securityHeader.MaxReceivedMessageSize = factory.SecurityBindingElement.MaxReceivedMessageSize;
            securityHeader.ReaderQuotas = factory.SecurityBindingElement.ReaderQuotas;
            if (factory.ExpectKeyDerivation)
            {
                securityHeader.DerivedTokenAuthenticator = factory.DerivedKeyTokenAuthenticator;
            }
            return securityHeader;
        }

        private bool HasCorrelationState(SecurityProtocolCorrelationState[] correlationState)
        {
            if (correlationState == null || correlationState.Length == 0)
            {
                return false;
            }
            else if (correlationState.Length == 1 && correlationState[0] == null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        protected ReceiveSecurityHeader ConfigureReceiveSecurityHeader(Message message, string actor, SecurityProtocolCorrelationState[] correlationStates, out IList<SupportingTokenAuthenticatorSpecification> supportingAuthenticators)
        {
            return ConfigureReceiveSecurityHeader(message, actor, correlationStates, null, out supportingAuthenticators);
        }

        protected ReceiveSecurityHeader ConfigureReceiveSecurityHeader(Message message, string actor, SecurityProtocolCorrelationState[] correlationStates, SecurityStandardsManager standardsManager, out IList<SupportingTokenAuthenticatorSpecification> supportingAuthenticators)
        {
            MessageSecurityProtocolFactory factory = MessageSecurityProtocolFactory;
            MessageDirection direction = factory.ActAsInitiator ? MessageDirection.Output : MessageDirection.Input;
            ReceiveSecurityHeader securityHeader = CreateSecurityHeader(message, actor, direction, standardsManager);

            string action = message.Headers.Action;
            supportingAuthenticators = GetSupportingTokenAuthenticatorsAndSetExpectationFlags(this.factory, message, securityHeader);
            if (factory.RequireIntegrity || securityHeader.ExpectSignedTokens)
            {
                securityHeader.RequiredSignatureParts = factory.GetIncomingSignatureParts(action);
            }
            if (factory.RequireConfidentiality || securityHeader.ExpectBasicTokens)
            {
                securityHeader.RequiredEncryptionParts = factory.GetIncomingEncryptionParts(action);
            }

            securityHeader.ExpectEncryption = factory.RequireConfidentiality || securityHeader.ExpectBasicTokens;
            securityHeader.ExpectSignature = factory.RequireIntegrity || securityHeader.ExpectSignedTokens;
            securityHeader.SetRequiredProtectionOrder(factory.MessageProtectionOrder);

            // On the receiving side if protectTokens is enabled
            // 1. If we are service, we make sure that the client always signs the primary token( can be any token type)else we throw.
            //    But currently the service can sign the primary token in reply only if the primary token is an issued token 
            // 2. If we are client, we do not care if the service signs the primary token or not. Otherwise it will be impossible to have a wcf client /service talk to each other unless we 
            // either use a symmetric binding with issued tokens or asymmetric bindings with both the intiator and recipient parameters being issued tokens( later one is rare).
            securityHeader.RequireSignedPrimaryToken = !factory.ActAsInitiator && factory.SecurityBindingElement.ProtectTokens;

            if (factory.ActAsInitiator && factory.DoRequestSignatureConfirmation && HasCorrelationState(correlationStates))
            {
                securityHeader.MaintainSignatureConfirmationState = true;
                securityHeader.ExpectSignatureConfirmation = true;
            }
            else if (!factory.ActAsInitiator && factory.DoRequestSignatureConfirmation)
            {
                securityHeader.MaintainSignatureConfirmationState = true;
            }
            else
            {
                securityHeader.MaintainSignatureConfirmationState = false;
            }
            return securityHeader;
        }

        protected async Task<Message> ProcessSecurityHeaderAsync(ReceiveSecurityHeader securityHeader,Message message,
            SecurityToken requiredSigningToken, TimeSpan timeout, SecurityProtocolCorrelationState[] correlationStates)
        {
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);

            securityHeader.ReplayDetectionEnabled = factory.DetectReplays;
            securityHeader.SetTimeParameters(factory.NonceCache, factory.ReplayWindow, factory.MaxClockSkew);

            await securityHeader.ProcessAsync(timeoutHelper.RemainingTime(), SecurityUtils.GetChannelBindingFromMessage(message), factory.ExtendedProtectionPolicy);
            if (factory.AddTimestamp && securityHeader.Timestamp == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.RequiredTimestampMissingInSecurityHeader)));
            }

            if (requiredSigningToken != null && requiredSigningToken != securityHeader.SignatureToken)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.ReplyWasNotSignedWithRequiredSigningToken)), message);
            }

            if (DoAutomaticEncryptionMatch)
            {
                SecurityUtils.EnsureExpectedSymmetricMatch(securityHeader.SignatureToken, securityHeader.EncryptionToken, message);
            }

            if (securityHeader.MaintainSignatureConfirmationState && factory.ActAsInitiator)
            {
                CheckSignatureConfirmation(securityHeader, correlationStates);
            }

            message = securityHeader.ProcessedMessage;
            return message;
        }

        protected void CheckSignatureConfirmation(ReceiveSecurityHeader securityHeader, SecurityProtocolCorrelationState[] correlationStates)
        {
            SignatureConfirmations receivedConfirmations = securityHeader.GetSentSignatureConfirmations();
            SignatureConfirmations sentSignatures = null;
            if (correlationStates != null)
            {
                for (int i = 0; i < correlationStates.Length; ++i)
                {
                    if (correlationStates[i].SignatureConfirmations != null)
                    {
                        sentSignatures = correlationStates[i].SignatureConfirmations;
                        break;
                    }
                }
            }
            if (sentSignatures == null)
            {
                if (receivedConfirmations != null && receivedConfirmations.Count > 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.FoundUnexpectedSignatureConfirmations)));
                }
                return;
            }
            bool allSignaturesConfirmed = false;
            if (receivedConfirmations != null && sentSignatures.Count == receivedConfirmations.Count)
            {
                bool[] matchingSigIndexes = new bool[sentSignatures.Count];
                for (int i = 0; i < sentSignatures.Count; ++i)
                {
                    byte[] sentSignature;
                    bool wasSentSigEncrypted;
                    sentSignatures.GetConfirmation(i, out sentSignature, out wasSentSigEncrypted);
                    for (int j = 0; j < receivedConfirmations.Count; ++j)
                    {
                        byte[] receivedSignature;
                        bool wasReceivedSigEncrypted;
                        if (matchingSigIndexes[j])
                        {
                            continue;
                        }
                        receivedConfirmations.GetConfirmation(j, out receivedSignature, out wasReceivedSigEncrypted);
                        if ((wasReceivedSigEncrypted == wasSentSigEncrypted) && CryptoHelper.IsEqual(receivedSignature, sentSignature))
                        {
                            matchingSigIndexes[j] = true;
                            break;
                        }
                    }
                }
                int k;
                for (k = 0; k < matchingSigIndexes.Length; ++k)
                {
                    if (!matchingSigIndexes[k])
                    {
                        break;
                    }
                }
                if (k == matchingSigIndexes.Length)
                {
                    allSignaturesConfirmed = true;
                }
            }
            if (!allSignaturesConfirmed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.NotAllSignaturesConfirmed)));
            }
        }

        /*
        protected SecurityToken GetTokenAndEnsureOutgoingIdentity(SecurityTokenProvider provider, bool isEncryptionOn, TimeSpan timeout, SecurityTokenAuthenticator authenticator)
        {
            SecurityToken token = GetToken(provider, this.Target, timeout);
            if (isEncryptionOn)
            {
                EnsureOutgoingIdentity(token, authenticator);
            }
            return token;
        }*/

        public override Message SecureOutgoingMessage(Message message, CancellationToken token)
        {
            try
            {
                CommunicationObject.ThrowIfClosedOrNotOpen();
                ValidateOutgoingState(message);
                if (!RequiresOutgoingSecurityProcessing && message.Properties.Security == null)
                {
                    return message;
                }
                TimeoutHelper timeoutHelper = new TimeoutHelper(DefaultOpenTimeout);

                var result = SecureOutgoingMessageCore(ref message, timeoutHelper.RemainingTime(), null);
                base.OnOutgoingMessageSecured(message);
                return message;
            }
            catch (Exception exception)
            {
                // Always immediately rethrow fatal exceptions.
                if (Fx.IsFatal(exception)) throw;

                base.OnSecureOutgoingMessageFailure(message);
                throw;
            }
        }

        //TODO :- really check if State is needed.
        protected abstract SecurityProtocolCorrelationState SecureOutgoingMessageCore(ref Message message, TimeSpan timeOut, SecurityProtocolCorrelationState correlationState);

        private void ValidateOutgoingState(Message message)
        {
            if (PerformIncomingAndOutgoingMessageExpectationChecks && !factory.ExpectOutgoingMessages)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecurityBindingNotSetUpToProcessOutgoingMessages)));
            }
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("message");
            }
        }

        public sealed override async ValueTask<Message> VerifyIncomingMessageAsync(Message message, TimeSpan timeout)
        {
            try
            {
                CommunicationObject.ThrowIfClosedOrNotOpen();
                if (PerformIncomingAndOutgoingMessageExpectationChecks && !factory.ExpectIncomingMessages)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecurityBindingNotSetUpToProcessIncomingMessages)));
                }
                if (message == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("message");
                }
                if (!RequiresIncomingSecurityProcessing(message))
                {
                    return message;
                }
                string actor = string.Empty; // message.Version.Envelope.UltimateDestinationActor;
               Tuple<SecurityProtocolCorrelationState,Message> result =  await VerifyIncomingMessageCoreAsync(message, actor, timeout, null);
                message = result.Item2;
                base.OnIncomingMessageVerified(message);
                return message;
            }
            catch (MessageSecurityException e)
            {
                base.OnVerifyIncomingMessageFailure(message, e);
                throw;
            }
            catch (Exception e)
            {
                // Always immediately rethrow fatal exceptions.
                if (Fx.IsFatal(e)) throw;

                base.OnVerifyIncomingMessageFailure(message, e);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.MessageSecurityVerificationFailed), e));
            }
        }

        /*
        public override SecurityProtocolCorrelationState VerifyIncomingMessage(ref Message message, TimeSpan timeout, params SecurityProtocolCorrelationState[] correlationStates)
        {
            try
            {
                CommunicationObject.ThrowIfClosedOrNotOpen();
                if (PerformIncomingAndOutgoingMessageExpectationChecks && !factory.ExpectIncomingMessages)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecurityBindingNotSetUpToProcessIncomingMessages)));
                }
                if (message == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("message");
                }
                if (!RequiresIncomingSecurityProcessing(message))
                {
                    return null;
                }
                string actor = string.Empty; // message.Version.Envelope.UltimateDestinationActor;
                SecurityProtocolCorrelationState newCorrelationState = VerifyIncomingMessageCore(ref message, actor, timeout, correlationStates);
                base.OnIncomingMessageVerified(message);
                return newCorrelationState;
            }
            catch (MessageSecurityException e)
            {
                base.OnVerifyIncomingMessageFailure(message, e);
                throw;
            }
            catch (Exception e)
            {
                // Always immediately rethrow fatal exceptions.
                if (Fx.IsFatal(e)) throw;

                base.OnVerifyIncomingMessageFailure(message, e);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.MessageSecurityVerificationFailed), e));
            }
        }
        */

        //TODO Check with Matt if we really need SecurityProtocolCorrelationState , in that case return Tuples <SecurityProtocolCorrelationState, Message>
        protected abstract Task<Tuple<SecurityProtocolCorrelationState,Message>> VerifyIncomingMessageCoreAsync(Message message, string actor, TimeSpan timeout, SecurityProtocolCorrelationState[] correlationStates);

        internal SecurityProtocolCorrelationState GetSignatureConfirmationCorrelationState(SecurityProtocolCorrelationState oldCorrelationState, SecurityProtocolCorrelationState newCorrelationState)
        {
            if (factory.ActAsInitiator)
            {
                return newCorrelationState;
            }
            else
            {
                return oldCorrelationState;
            }
        }


//        protected abstract class GetOneTokenAndSetUpSecurityAsyncResult //: GetSupportingTokensAsyncResult
//        {
//            private readonly MessageSecurityProtocol binding;
//            private readonly SecurityTokenProvider provider;
//            private Message message;
//            private readonly bool doIdentityChecks;
//            private SecurityTokenAuthenticator identityCheckAuthenticator;
//            private static AsyncCallback getTokenCompleteCallback = Fx.ThunkCallback(new AsyncCallback(GetTokenCompleteCallback));
//            private SecurityProtocolCorrelationState newCorrelationState;
//            private SecurityProtocolCorrelationState oldCorrelationState;
//            private TimeoutHelper timeoutHelper;

//            public GetOneTokenAndSetUpSecurityAsyncResult(Message m, MessageSecurityProtocol binding, SecurityTokenProvider provider,
//                bool doIdentityChecks, SecurityTokenAuthenticator identityCheckAuthenticator, SecurityProtocolCorrelationState oldCorrelationState, TimeSpan timeout, AsyncCallback callback, object state)
//               // : base(m, binding, timeout, callback, state)
//            {
//                message = m;
//                this.binding = binding;
//                this.provider = provider;
//                this.doIdentityChecks = doIdentityChecks;
//                this.oldCorrelationState = oldCorrelationState;
//                this.identityCheckAuthenticator = identityCheckAuthenticator;
//            }

//            protected MessageSecurityProtocol Binding
//            {
//                get { return binding; }
//            }

//            protected SecurityProtocolCorrelationState NewCorrelationState
//            {
//                get { return newCorrelationState; }
//            }

//            protected SecurityProtocolCorrelationState OldCorrelationState
//            {
//                get { return oldCorrelationState; }
//            }

//            /*
//            internal static Message End(IAsyncResult result, out SecurityProtocolCorrelationState newCorrelationState)
//            {
//                GetOneTokenAndSetUpSecurityAsyncResult self = AsyncResult.End<GetOneTokenAndSetUpSecurityAsyncResult>(result);
//                newCorrelationState = self.newCorrelationState;
//                return self.message;
//            }*/

        //            private bool OnGetTokenComplete(SecurityToken token)
        //            {
        //                if (token == null)
        //                {
        //                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.TokenProviderCannotGetTokensForTarget, binding.Target)));
        //                }
        //                if (doIdentityChecks)
        //                {
        //                    binding.EnsureOutgoingIdentity(token, identityCheckAuthenticator);
        //                }
        //                OnGetTokenDone(ref message, token, timeoutHelper.RemainingTime());
        //                return true;
        //            }

        //            protected abstract void OnGetTokenDone(ref Message message, SecurityToken token, TimeSpan timeout);

        //            private static void GetTokenCompleteCallback(IAsyncResult result)
        //            {
        //                if (result == null)
        //                {
        //                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("result");
        //                }
        //                if (result.CompletedSynchronously)
        //                {
        //                    return;
        //                }
        //                GetOneTokenAndSetUpSecurityAsyncResult self = result.AsyncState as GetOneTokenAndSetUpSecurityAsyncResult;
        //                if (self == null)
        //                {
        //                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("result", SR.Format(SR.InvalidAsyncResult));
        //                }
        //                Exception completionException = null;
        //                bool completeSelf = false;
        //                try
        //                {
        //                    SecurityToken token = self.provider.EndGetToken(result);
        //                    completeSelf = self.OnGetTokenComplete(token);
        //                }
        //#pragma warning suppress 56500 // covered by FxCOP
        //                catch (Exception e)
        //                {
        //                    // Always immediately rethrow fatal exceptions.
        //                    if (Fx.IsFatal(e)) throw;

        //                    completeSelf = true;
        //                    completionException = e;
        //                }
        //                if (completeSelf)
        //                {
        //                    self.Complete(false, completionException);
        //                }
        //            }

        //            protected void SetCorrelationToken(SecurityToken token)
        //            {
        //                newCorrelationState = new SecurityProtocolCorrelationState(token);
        //            }

        //            protected override bool OnGetSupportingTokensDone(TimeSpan timeout)
        //            {
        //                timeoutHelper = new TimeoutHelper(timeout);
        //                IAsyncResult result = provider.BeginGetToken(timeoutHelper.RemainingTime(), getTokenCompleteCallback, this);
        //                if (!result.CompletedSynchronously)
        //                {
        //                    return false;

        //                }
        //                SecurityToken token = provider.EndGetToken(result);
        //                return OnGetTokenComplete(token);
        //            }
        //        }

        //        // note: identity check done only on token obtained from first
        //        // token provider; either or both token providers may be null;
        //        // get token calls are skipped for null providers.
        //        protected abstract class GetTwoTokensAndSetUpSecurityAsyncResult //: GetSupportingTokensAsyncResult
        //        {
        //            private readonly SecurityTokenProvider _primaryProvider;
        //            private readonly SecurityTokenProvider _secondaryProvider;
        //            private Message _message;
        //            private readonly bool _doIdentityChecks;
        //            private SecurityTokenAuthenticator _identityCheckAuthenticator;
        //            private SecurityToken _primaryToken;
        //           // private static readonly AsyncCallback getPrimaryTokenCompleteCallback = Fx.ThunkCallback(new AsyncCallback(GetPrimaryTokenCompleteCallback));
        //           // private static readonly AsyncCallback getSecondaryTokenCompleteCallback = Fx.ThunkCallback(new AsyncCallback(GetSecondaryTokenCompleteCallback));
        //            private SecurityProtocolCorrelationState _newCorrelationState;
        //            private SecurityProtocolCorrelationState _oldCorrelationState;
        //            private TimeoutHelper _timeoutHelper;

        //            public GetTwoTokensAndSetUpSecurityAsyncResult(Message m, MessageSecurityProtocol binding,
        //                SecurityTokenProvider primaryProvider, SecurityTokenProvider secondaryProvider, bool doIdentityChecks, SecurityTokenAuthenticator identityCheckAuthenticator,
        //                SecurityProtocolCorrelationState oldCorrelationState,
        //                TimeSpan timeout,
        //                AsyncCallback callback, object state)
        //               // : base(m, binding, timeout, callback, state)
        //            {
        //                _message = m;
        //                Binding = binding;
        //                _primaryProvider = primaryProvider;
        //                _secondaryProvider = secondaryProvider;
        //                _doIdentityChecks = doIdentityChecks;
        //                _identityCheckAuthenticator = identityCheckAuthenticator;
        //                _oldCorrelationState = oldCorrelationState;
        //            }

        //            protected MessageSecurityProtocol Binding { get; }

        //            protected SecurityProtocolCorrelationState NewCorrelationState
        //            {
        //                get { return _newCorrelationState; }
        //            }

        //            protected SecurityProtocolCorrelationState OldCorrelationState
        //            {
        //                get { return _oldCorrelationState; }
        //            }

        //           /* internal static Message End(IAsyncResult result, out SecurityProtocolCorrelationState newCorrelationState)
        //            {
        //                GetTwoTokensAndSetUpSecurityAsyncResult self = AsyncResult.End<GetTwoTokensAndSetUpSecurityAsyncResult>(result);
        //                newCorrelationState = self.newCorrelationState;
        //                return self.message;
        //            }*/

        //            internal Task<bool> OnGetPrimaryTokenComplete(SecurityToken token)
        //            {
        //                return OnGetPrimaryTokenCompleteAsync(token, false);
        //            }

        //            internal async Task<bool> OnGetPrimaryTokenCompleteAsync(SecurityToken token, bool primaryCallSkipped)
        //            {
        //                if (!primaryCallSkipped)
        //                {
        //                    if (token == null)
        //                    {
        //                        throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.TokenProviderCannotGetTokensForTarget, Binding.Target)), _message);
        //                    }
        //                    if (_doIdentityChecks)
        //                    {
        //                        Binding.EnsureOutgoingIdentity(token, _identityCheckAuthenticator);
        //                    }
        //                }
        //                _primaryToken = token;

        //                if (_secondaryProvider == null)
        //                {
        //                    return OnGetSecondaryTokenComplete(null, true);
        //                }
        //                else
        //                {
        //                    /*
        //                    IAsyncResult result = secondaryProvider.BeginGetToken(timeoutHelper.RemainingTime(), getSecondaryTokenCompleteCallback, this);
        //                    if (!result.CompletedSynchronously)
        //                    {
        //                        return false;
        //                    }
        //                    SecurityToken token2 = secondaryProvider.EndGetToken(result);*/
        //                    SecurityToken token2 = await _secondaryProvider.GetTokenAsync(_timeoutHelper.GetCancellationToken());
        //                    return OnGetSecondaryTokenComplete(token2);
        //                }
        //            }

        //            protected bool OnGetSecondaryTokenComplete(SecurityToken token)
        //            {
        //                return OnGetSecondaryTokenComplete(token, false);
        //            }

        //            protected bool OnGetSecondaryTokenComplete(SecurityToken token, bool secondaryCallSkipped)
        //            {
        //                if (!secondaryCallSkipped && token == null)
        //                {
        //                    throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.TokenProviderCannotGetTokensForTarget, Binding.Target)), _message);
        //                }
        //                OnBothGetTokenCallsDone(ref _message, _primaryToken, token, _timeoutHelper.RemainingTime());
        //                return true;
        //            }

        //            protected abstract void OnBothGetTokenCallsDone(ref Message message, SecurityToken primaryToken, SecurityToken secondaryToken, TimeSpan timeout);

        //            private static void GetPrimaryTokenCompleteCallback(IAsyncResult result)
        //            {
        //                if (result == null)
        //                {
        //                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("result");
        //                }
        //                if (result.CompletedSynchronously)
        //                {
        //                    return;
        //                }
        //                GetTwoTokensAndSetUpSecurityAsyncResult self = result.AsyncState as GetTwoTokensAndSetUpSecurityAsyncResult;
        //                if (self == null)
        //                {
        //                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("result", SR.Format(SR.InvalidAsyncResult));
        //                }
        //                bool completeSelf = false;
        //                Exception completionException = null;
        //                try
        //                {
        //                    SecurityToken token = self._primaryProvider.EndGetToken(result);
        //                    completeSelf = self.OnGetPrimaryTokenComplete(token);
        //                }
        //#pragma warning suppress 56500 // covered by FxCOP
        //                catch (Exception e)
        //                {
        //                    // Always immediately rethrow fatal exceptions.
        //                    if (Fx.IsFatal(e)) throw;

        //                    completeSelf = true;
        //                    completionException = e;
        //                }
        //                if (completeSelf)
        //                {
        //                    self.Complete(false, completionException);
        //                }
        //            }

        //            private static void GetSecondaryTokenCompleteCallback(IAsyncResult result)
        //            {
        //                if (result == null)
        //                {
        //                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("result");
        //                }
        //                if (result.CompletedSynchronously)
        //                {
        //                    return;
        //                }
        //                GetTwoTokensAndSetUpSecurityAsyncResult self = result.AsyncState as GetTwoTokensAndSetUpSecurityAsyncResult;
        //                if (self == null)
        //                {
        //                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("result", SR.Format(SR.InvalidAsyncResult));
        //                }
        //                bool completeSelf = false;
        //                Exception completionException = null;
        //                try
        //                {
        //                    SecurityToken token = self._secondaryProvider.EndGetToken(result);
        //                    completeSelf = self.OnGetSecondaryTokenComplete(token);
        //                }
        //#pragma warning suppress 56500 // covered by FxCOP
        //                catch (Exception e)
        //                {
        //                    // Always immediately rethrow fatal exceptions.
        //                    if (Fx.IsFatal(e)) throw;

        //                    completeSelf = true;
        //                    completionException = e;
        //                }
        //                if (completeSelf)
        //                {
        //                    self.Complete(false, completionException);
        //                }
        //            }

        //            protected void SetCorrelationToken(SecurityToken token)
        //            {
        //                _newCorrelationState = new SecurityProtocolCorrelationState(token);
        //            }

        //            protected override bool OnGetSupportingTokensDone(TimeSpan timeout)
        //            {
        //                _timeoutHelper = new TimeoutHelper(timeout);
        //                bool completeSelf = false;
        //                if (_primaryProvider == null)
        //                {
        //                    completeSelf = OnGetPrimaryTokenComplete(null);
        //                }
        //                else
        //                {
        //                    IAsyncResult result = _primaryProvider.BeginGetToken(_timeoutHelper.RemainingTime(), getPrimaryTokenCompleteCallback, this);
        //                    if (result.CompletedSynchronously)
        //                    {
        //                        SecurityToken token = _primaryProvider.EndGetToken(result);
        //                        completeSelf = OnGetPrimaryTokenComplete(token);
        //                    }
        //                }
        //                return completeSelf;
        //            }
        //        }
    }
}
