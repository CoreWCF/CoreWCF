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
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal sealed class SymmetricSecurityProtocol : MessageSecurityProtocol
    {
        private SecurityTokenProvider _initiatorSymmetricTokenProvider;
        private SecurityTokenProvider _initiatorAsymmetricTokenProvider;
        private SecurityTokenAuthenticator _initiatorTokenAuthenticator;

        public SymmetricSecurityProtocol(SymmetricSecurityProtocolFactory factory,
            EndpointAddress target, Uri via)
            : base(factory, target, via)
        {
        }

        private SymmetricSecurityProtocolFactory Factory
        {
            get { return (SymmetricSecurityProtocolFactory)base.MessageSecurityProtocolFactory; }
        }

        public SecurityTokenProvider InitiatorSymmetricTokenProvider
        {
            get
            {
                CommunicationObject.ThrowIfNotOpened();
                return _initiatorSymmetricTokenProvider;
            }
        }

        public SecurityTokenProvider InitiatorAsymmetricTokenProvider
        {
            get
            {
                CommunicationObject.ThrowIfNotOpened();
                return _initiatorAsymmetricTokenProvider;
            }
        }

        public SecurityTokenAuthenticator InitiatorTokenAuthenticator
        {
            get
            {

                CommunicationObject.ThrowIfNotOpened();
                return _initiatorTokenAuthenticator;
            }
        }

        private InitiatorServiceModelSecurityTokenRequirement CreateInitiatorTokenRequirement()
        {
            InitiatorServiceModelSecurityTokenRequirement tokenRequirement = CreateInitiatorSecurityTokenRequirement();
            Factory.SecurityTokenParameters.InitializeSecurityTokenRequirement(tokenRequirement);
            tokenRequirement.KeyUsage = Factory.SecurityTokenParameters.HasAsymmetricKey ? SecurityKeyUsage.Exchange : SecurityKeyUsage.Signature;
            tokenRequirement.Properties[ServiceModelSecurityTokenRequirement.MessageDirectionProperty] = MessageDirection.Output;
            if (Factory.SecurityTokenParameters.HasAsymmetricKey)
            {
                tokenRequirement.IsOutOfBandToken = true;
            }
            return tokenRequirement;
        }

        public override async Task OnOpenAsync(TimeSpan timeout)
        {
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            await base.OnOpenAsync(timeoutHelper.RemainingTime());
            if (Factory.ActAsInitiator)
            {
                // 1. Create a token requirement for the provider
                InitiatorServiceModelSecurityTokenRequirement tokenProviderRequirement = CreateInitiatorTokenRequirement();

                // 2. Create a provider
                SecurityTokenProvider tokenProvider = Factory.SecurityTokenManager.CreateSecurityTokenProvider(tokenProviderRequirement);
                await SecurityUtils.OpenTokenProviderIfRequiredAsync(tokenProvider, timeoutHelper.GetCancellationToken());
                if (Factory.SecurityTokenParameters.HasAsymmetricKey)
                {
                    _initiatorAsymmetricTokenProvider = tokenProvider;
                }
                else
                {
                    _initiatorSymmetricTokenProvider = tokenProvider;
                }

                // 3. Create a token requirement for authenticator
                InitiatorServiceModelSecurityTokenRequirement tokenAuthenticatorRequirement = CreateInitiatorTokenRequirement();

                // 4. Create authenticator (we dont support out of band resolvers on the client side
                SecurityTokenResolver outOfBandTokenResolver;
                _initiatorTokenAuthenticator = Factory.SecurityTokenManager.CreateSecurityTokenAuthenticator(tokenAuthenticatorRequirement, out outOfBandTokenResolver);
                await SecurityUtils.OpenTokenAuthenticatorIfRequiredAsync(_initiatorTokenAuthenticator, timeoutHelper.GetCancellationToken());
            }
        }

        public override void OnAbort()
        {
            if (Factory.ActAsInitiator)
            {
                SecurityTokenProvider provider = _initiatorSymmetricTokenProvider ?? _initiatorAsymmetricTokenProvider;
                if (provider != null)
                {
                    SecurityUtils.AbortTokenProviderIfRequired(provider);
                }
                if (_initiatorTokenAuthenticator != null)
                {
                    SecurityUtils.AbortTokenAuthenticatorIfRequired(_initiatorTokenAuthenticator);
                }
            }
            base.OnAbort();
        }

        public async Task CloseAsync(TimeSpan timeout)
        {
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            if (Factory.ActAsInitiator)
            {
                SecurityTokenProvider provider = _initiatorSymmetricTokenProvider ?? _initiatorAsymmetricTokenProvider;
                if (provider != null)
                {
                    await SecurityUtils.CloseTokenProviderIfRequiredAsync(provider, timeoutHelper.GetCancellationToken());
                }
                if (_initiatorTokenAuthenticator != null)
                {
                    await SecurityUtils.CloseTokenAuthenticatorIfRequiredAsync(_initiatorTokenAuthenticator, timeoutHelper.GetCancellationToken());
                }
            }
            await base.CloseAsync(false, timeoutHelper.RemainingTime());
        }


        private SecurityTokenProvider GetTokenProvider()
        {
            if (Factory.ActAsInitiator)
            {
                return (_initiatorSymmetricTokenProvider ?? _initiatorAsymmetricTokenProvider);
            }
            else
            {
                return Factory.RecipientAsymmetricTokenProvider;
            }
        }

        protected override SecurityProtocolCorrelationState SecureOutgoingMessageCore(ref Message message, CancellationToken cancellationToken, SecurityProtocolCorrelationState correlationState)
        {
            SecurityToken token;
            SecurityTokenParameters tokenParameters;
            IList<SupportingTokenSpecification> supportingTokens;
            SecurityToken prerequisiteWrappingToken;
            SecurityProtocolCorrelationState newCorrelationState;

            TryGetTokenSynchronouslyForOutgoingSecurity(message, correlationState, true, DefaultOpenTimeout, out token, out tokenParameters, out prerequisiteWrappingToken, out supportingTokens, out newCorrelationState);
            SetUpDelayedSecurityExecution(ref message, prerequisiteWrappingToken, token, tokenParameters, supportingTokens, GetSignatureConfirmationCorrelationState(correlationState, newCorrelationState));
            return newCorrelationState;
        }

        private void SetUpDelayedSecurityExecution(ref Message message,
            SecurityToken prerequisiteToken,
            SecurityToken primaryToken,
            SecurityTokenParameters primaryTokenParameters,
            IList<SupportingTokenSpecification> supportingTokens,
            SecurityProtocolCorrelationState correlationState
        )
        {
            string actor = string.Empty;
            SendSecurityHeader securityHeader = ConfigureSendSecurityHeader(message, actor, supportingTokens, correlationState);
            if (prerequisiteToken != null)
            {
                securityHeader.AddPrerequisiteToken(prerequisiteToken);
            }
            if (Factory.ApplyIntegrity || securityHeader.HasSignedTokens)
            {
                if (!Factory.ApplyIntegrity)
                {
                    securityHeader.SignatureParts = MessagePartSpecification.NoParts;
                }
                securityHeader.SetSigningToken(primaryToken, primaryTokenParameters);
            }
            if (Factory.ApplyConfidentiality || securityHeader.HasEncryptedTokens)
            {
                if (!Factory.ApplyConfidentiality)
                {
                    securityHeader.EncryptionParts = MessagePartSpecification.NoParts;
                }
                securityHeader.SetEncryptionToken(primaryToken, primaryTokenParameters);
            }
            message = securityHeader.SetupExecution();
        }

        // try to get the token if it can be obtained within the
        // synchronous requirements of the call; return true iff a
        // token not required OR a token is required AND has been
        // obtained within the specified synchronous requirements.
        private bool TryGetTokenSynchronouslyForOutgoingSecurity(Message message, SecurityProtocolCorrelationState correlationState, bool isBlockingCall, TimeSpan timeout, out SecurityToken token, out SecurityTokenParameters tokenParameters, out SecurityToken prerequisiteWrappingToken, out IList<SupportingTokenSpecification> supportingTokens, out SecurityProtocolCorrelationState newCorrelationState)
        {
            SymmetricSecurityProtocolFactory factory = Factory;
            supportingTokens = null;
            prerequisiteWrappingToken = null;
            token = null;
            tokenParameters = null;
            newCorrelationState = null;
            if (factory.ApplyIntegrity || factory.ApplyConfidentiality)
            {
                if (factory.ActAsInitiator)
                {
                    if (!isBlockingCall || ! TryGetSupportingTokens(factory, Target, Via, message, timeout, isBlockingCall, out supportingTokens))
                    {
                        return false;
                    }
                }
                else
                {
                    token = GetCorrelationToken(correlationState);
                    tokenParameters = Factory.GetProtectionTokenParameters();
                }
            }
            return true;
        }

        private SecurityToken GetCorrelationToken(SecurityProtocolCorrelationState[] correlationStates, out SecurityTokenParameters correlationTokenParameters)
        {
            SecurityToken token = GetCorrelationToken(correlationStates);
            correlationTokenParameters = Factory.GetProtectionTokenParameters();
            return token;
        }

        private void EnsureWrappedToken(SecurityToken token, Message message)
        {
            if (!(token is WrappedKeySecurityToken))
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.IncomingSigningTokenMustBeAnEncryptedKey)), message);
            }
        }

        protected override async Task<Tuple<SecurityProtocolCorrelationState,Message>> VerifyIncomingMessageCoreAsync(Message message, string actor, TimeSpan timeout, SecurityProtocolCorrelationState[] correlationStates)
        {
            SymmetricSecurityProtocolFactory factory = Factory;
            IList<SupportingTokenAuthenticatorSpecification> supportingAuthenticators;
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            ReceiveSecurityHeader securityHeader = ConfigureReceiveSecurityHeader(message, string.Empty, correlationStates, out supportingAuthenticators);
            SecurityToken requiredReplySigningToken = null;
            if (Factory.ActAsInitiator)
            {
                // set the outofband protection token
                SecurityTokenParameters outOfBandTokenParameters;
                SecurityToken outOfBandToken = GetCorrelationToken(correlationStates, out outOfBandTokenParameters);
                securityHeader.ConfigureSymmetricBindingClientReceiveHeader(outOfBandToken, outOfBandTokenParameters);
                requiredReplySigningToken = outOfBandToken;
            }
            else
            {
                if (factory.RecipientSymmetricTokenAuthenticator != null)
                {
                    securityHeader.ConfigureSymmetricBindingServerReceiveHeader(Factory.RecipientSymmetricTokenAuthenticator, Factory.SecurityTokenParameters, supportingAuthenticators);
                }
                else
                {
                    securityHeader.ConfigureSymmetricBindingServerReceiveHeader(Factory.RecipientAsymmetricTokenProvider.GetToken(timeoutHelper.RemainingTime()), Factory.SecurityTokenParameters, supportingAuthenticators);
                    securityHeader.WrappedKeySecurityTokenAuthenticator = Factory.WrappedKeySecurityTokenAuthenticator;
                }
                securityHeader.ConfigureOutOfBandTokenResolver(MergeOutOfBandResolvers(supportingAuthenticators, Factory.RecipientOutOfBandTokenResolverList));
            }

            message = await ProcessSecurityHeaderAsync(securityHeader, message, requiredReplySigningToken, timeoutHelper.RemainingTime(), correlationStates);
            SecurityToken signingToken = securityHeader.SignatureToken;
            if (factory.RequireIntegrity)
            {
                if (factory.SecurityTokenParameters.HasAsymmetricKey)
                {
                    // enforce that the signing token is a wrapped key token
                    EnsureWrappedToken(signingToken, message);
                }
                else
                {
                    EnsureNonWrappedToken(signingToken, message);
                }

                if (factory.ActAsInitiator)
                {
                    if (!factory.SecurityTokenParameters.HasAsymmetricKey)
                    {
                        ReadOnlyCollection<IAuthorizationPolicy> signingTokenPolicies = _initiatorTokenAuthenticator.ValidateToken(signingToken);
                        DoIdentityCheckAndAttachInitiatorSecurityProperty(message, signingToken, signingTokenPolicies);
                    }
                    else
                    {
                        SecurityToken wrappingToken = (signingToken as WrappedKeySecurityToken).WrappingToken;
                        ReadOnlyCollection<IAuthorizationPolicy> wrappingTokenPolicies = _initiatorTokenAuthenticator.ValidateToken(wrappingToken);
                        DoIdentityCheckAndAttachInitiatorSecurityProperty(message, signingToken, wrappingTokenPolicies);
                    }
                }
                else
                {
                    AttachRecipientSecurityProperty(message, signingToken, Factory.SecurityTokenParameters.HasAsymmetricKey, securityHeader.BasicSupportingTokens, securityHeader.EndorsingSupportingTokens, securityHeader.SignedEndorsingSupportingTokens,
                        securityHeader.SignedSupportingTokens, securityHeader.SecurityTokenAuthorizationPoliciesMapping);
                }
            }
            return new Tuple<SecurityProtocolCorrelationState, Message>(GetCorrelationState(signingToken, securityHeader),message);
        }

        /*
        sealed class SecureOutgoingMessageAsyncResult : GetOneTokenAndSetUpSecurityAsyncResult
        {
            SymmetricSecurityProtocol symmetricBinding;

            public SecureOutgoingMessageAsyncResult(Message m, SymmetricSecurityProtocol binding, SecurityTokenProvider provider,
                bool doIdentityChecks, SecurityTokenAuthenticator identityCheckAuthenticator, SecurityProtocolCorrelationState correlationState, TimeSpan timeout, AsyncCallback callback, object state)
                : base(m, binding, provider, doIdentityChecks, identityCheckAuthenticator, correlationState, timeout, callback, state)
            {
                symmetricBinding = binding;
                Start();
            }

            protected override void OnGetTokenDone(ref Message message, SecurityToken providerToken, TimeSpan timeout)
            {
                SecurityTokenParameters tokenParameters;
                SecurityToken prerequisiteWrappingToken;
                SecurityToken token = symmetricBinding.GetInitiatorToken(providerToken, message, timeout, out tokenParameters, out prerequisiteWrappingToken);
                this.SetCorrelationToken(token);
                symmetricBinding.SetUpDelayedSecurityExecution(ref message, prerequisiteWrappingToken, token, tokenParameters, this.SupportingTokens, Binding.GetSignatureConfirmationCorrelationState(OldCorrelationState, NewCorrelationState));
            }
        }
        */
    }
}
