// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    // See SecurityProtocolFactory for contracts on subclasses etc
    internal abstract class SecurityProtocol : ISecurityCommunicationObject
    {
        private static ReadOnlyCollection<SupportingTokenProviderSpecification> s_emptyTokenProviders;
        private Dictionary<string, Collection<SupportingTokenProviderSpecification>> _mergedSupportingTokenProvidersMap;

        protected SecurityProtocol(SecurityProtocolFactory factory, EndpointAddress target, Uri via)
        {
            SecurityProtocolFactory = factory;
            Target = target;
            Via = via;
            CommunicationObject = new WrapperSecurityCommunicationObject(this);
        }

        protected WrapperSecurityCommunicationObject CommunicationObject { get; }

        public SecurityProtocolFactory SecurityProtocolFactory { get; }

        public EndpointAddress Target { get; }

        public Uri Via { get; }

        public ICollection<SupportingTokenProviderSpecification> ChannelSupportingTokenProviderSpecification { get; private set; }

        public Dictionary<string, ICollection<SupportingTokenProviderSpecification>> ScopedSupportingTokenProviderSpecification { get; private set; }

        private static ReadOnlyCollection<SupportingTokenProviderSpecification> EmptyTokenProviders
        {
            get
            {
                if (s_emptyTokenProviders == null)
                {
                    s_emptyTokenProviders = new ReadOnlyCollection<SupportingTokenProviderSpecification>(new List<SupportingTokenProviderSpecification>());
                }
                return s_emptyTokenProviders;
            }
        }

        // ISecurityCommunicationObject members
        public TimeSpan DefaultOpenTimeout
        {
            get { return ServiceDefaults.OpenTimeout; }
        }

        public TimeSpan DefaultCloseTimeout
        {
            get { return ServiceDefaults.CloseTimeout; }
        }

        public Task OpenAsync(TimeSpan timeout)
        {
            return CommunicationObject.OpenAsync();
        }

        public void OnClosed() { }

        public void OnClosing() { }

        public void OnFaulted() { }

        public void OnOpened() { }

        public void OnOpening() { }

        internal IList<SupportingTokenProviderSpecification> GetSupportingTokenProviders(string action)
        {
            if (_mergedSupportingTokenProvidersMap != null && _mergedSupportingTokenProvidersMap.Count > 0)
            {
                if (action != null && _mergedSupportingTokenProvidersMap.ContainsKey(action))
                {
                    return _mergedSupportingTokenProvidersMap[action];
                }
                else if (_mergedSupportingTokenProvidersMap.ContainsKey(MessageHeaders.WildcardAction))
                {
                    return _mergedSupportingTokenProvidersMap[MessageHeaders.WildcardAction];
                }
            }
            // return null if the token providers list is empty - this gets a perf benefit since calling Count is expensive for an empty
            // ReadOnlyCollection
            return (ChannelSupportingTokenProviderSpecification == EmptyTokenProviders) ? null : (IList<SupportingTokenProviderSpecification>)ChannelSupportingTokenProviderSpecification;
        }

        protected InitiatorServiceModelSecurityTokenRequirement CreateInitiatorSecurityTokenRequirement()
        {
            InitiatorServiceModelSecurityTokenRequirement requirement = new InitiatorServiceModelSecurityTokenRequirement
            {
                TargetAddress = Target,
                Via = Via,
                SecurityBindingElement = SecurityProtocolFactory.SecurityBindingElement,
                SecurityAlgorithmSuite = SecurityProtocolFactory.OutgoingAlgorithmSuite,
                MessageSecurityVersion = SecurityProtocolFactory.MessageSecurityVersion.SecurityTokenVersion
            };
            return requirement;
        }

        private InitiatorServiceModelSecurityTokenRequirement CreateInitiatorSecurityTokenRequirement(SecurityTokenParameters parameters, SecurityTokenAttachmentMode attachmentMode)
        {
            InitiatorServiceModelSecurityTokenRequirement requirement = CreateInitiatorSecurityTokenRequirement();
            parameters.InitializeSecurityTokenRequirement(requirement);
            requirement.KeyUsage = SecurityKeyUsage.Signature;
            requirement.Properties[ServiceModelSecurityTokenRequirement.MessageDirectionProperty] = MessageDirection.Output;
            requirement.Properties[ServiceModelSecurityTokenRequirement.SupportingTokenAttachmentModeProperty] = attachmentMode;
            return requirement;
        }

        private void AddSupportingTokenProviders(SupportingTokenParameters supportingTokenParameters, bool isOptional, IList<SupportingTokenProviderSpecification> providerSpecList)
        {
            for (int i = 0; i < supportingTokenParameters.Endorsing.Count; ++i)
            {
                SecurityTokenRequirement requirement = CreateInitiatorSecurityTokenRequirement(supportingTokenParameters.Endorsing[i], SecurityTokenAttachmentMode.Endorsing);
                try
                {
                    if (isOptional)
                    {
                        requirement.IsOptionalToken = true;
                    }
                    SecurityTokenProvider provider = SecurityProtocolFactory.SecurityTokenManager.CreateSecurityTokenProvider(requirement);
                    if (provider == null)
                    {
                        continue;
                    }
                    SupportingTokenProviderSpecification providerSpec = new SupportingTokenProviderSpecification(provider, SecurityTokenAttachmentMode.Endorsing, supportingTokenParameters.Endorsing[i]);
                    providerSpecList.Add(providerSpec);
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
                SecurityTokenRequirement requirement = CreateInitiatorSecurityTokenRequirement(supportingTokenParameters.SignedEndorsing[i], SecurityTokenAttachmentMode.SignedEndorsing);
                try
                {
                    if (isOptional)
                    {
                        requirement.IsOptionalToken = true;
                    }
                    SecurityTokenProvider provider = SecurityProtocolFactory.SecurityTokenManager.CreateSecurityTokenProvider(requirement);
                    if (provider == null)
                    {
                        continue;
                    }
                    SupportingTokenProviderSpecification providerSpec = new SupportingTokenProviderSpecification(provider, SecurityTokenAttachmentMode.SignedEndorsing, supportingTokenParameters.SignedEndorsing[i]);
                    providerSpecList.Add(providerSpec);
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
                SecurityTokenRequirement requirement = CreateInitiatorSecurityTokenRequirement(supportingTokenParameters.SignedEncrypted[i], SecurityTokenAttachmentMode.SignedEncrypted);
                try
                {
                    if (isOptional)
                    {
                        requirement.IsOptionalToken = true;
                    }
                    SecurityTokenProvider provider = SecurityProtocolFactory.SecurityTokenManager.CreateSecurityTokenProvider(requirement);
                    if (provider == null)
                    {
                        continue;
                    }
                    SupportingTokenProviderSpecification providerSpec = new SupportingTokenProviderSpecification(provider, SecurityTokenAttachmentMode.SignedEncrypted, supportingTokenParameters.SignedEncrypted[i]);
                    providerSpecList.Add(providerSpec);
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
                SecurityTokenRequirement requirement = CreateInitiatorSecurityTokenRequirement(supportingTokenParameters.Signed[i], SecurityTokenAttachmentMode.Signed);
                try
                {
                    if (isOptional)
                    {
                        requirement.IsOptionalToken = true;
                    }
                    SecurityTokenProvider provider = SecurityProtocolFactory.SecurityTokenManager.CreateSecurityTokenProvider(requirement);
                    if (provider == null)
                    {
                        continue;
                    }
                    SupportingTokenProviderSpecification providerSpec = new SupportingTokenProviderSpecification(provider, SecurityTokenAttachmentMode.Signed, supportingTokenParameters.Signed[i]);
                    providerSpecList.Add(providerSpec);
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

        private async Task MergeSupportingTokenProvidersAsync(TimeSpan timeout)
        {
            if (ScopedSupportingTokenProviderSpecification.Count == 0)
            {
                _mergedSupportingTokenProvidersMap = null;
            }
            else
            {
                TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
                SecurityProtocolFactory.ExpectSupportingTokens = true;
                _mergedSupportingTokenProvidersMap = new Dictionary<string, Collection<SupportingTokenProviderSpecification>>();
                foreach (string action in ScopedSupportingTokenProviderSpecification.Keys)
                {
                    ICollection<SupportingTokenProviderSpecification> scopedProviders = ScopedSupportingTokenProviderSpecification[action];
                    if (scopedProviders == null || scopedProviders.Count == 0)
                    {
                        continue;
                    }
                    Collection<SupportingTokenProviderSpecification> mergedProviders = new Collection<SupportingTokenProviderSpecification>();
                    foreach (SupportingTokenProviderSpecification spec in ChannelSupportingTokenProviderSpecification)
                    {
                        mergedProviders.Add(spec);
                    }
                    foreach (SupportingTokenProviderSpecification spec in scopedProviders)
                    {
                        await SecurityUtils.OpenTokenProviderIfRequiredAsync(spec.TokenProvider, timeoutHelper.GetCancellationToken());
                        if (spec.SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.Endorsing || spec.SecurityTokenAttachmentMode == SecurityTokenAttachmentMode.SignedEndorsing)
                        {
                            if (spec.TokenParameters.RequireDerivedKeys && !spec.TokenParameters.HasAsymmetricKey)
                            {
                                SecurityProtocolFactory.ExpectKeyDerivation = true;
                            }
                        }
                        mergedProviders.Add(spec);
                    }
                    _mergedSupportingTokenProvidersMap.Add(action, mergedProviders);
                }
            }
        }

        public virtual Task OnOpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public virtual void OnAbort()
        {
        }

        //public virtual async Task OnCloseAsync(CancellationToken token)
        //{
        //    if (SecurityProtocolFactory.ActAsInitiator)
        //    {
        //        /*
        //        TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
        //        foreach (SupportingTokenProviderSpecification spec in ChannelSupportingTokenProviderSpecification)
        //        {
        //            await SecurityUtils.CloseTokenProviderIfRequiredAsync(spec.TokenProvider, timeoutHelper.RemainingTime());
        //        }

        //        foreach (string action in ScopedSupportingTokenProviderSpecification.Keys)
        //        {
        //            ICollection<SupportingTokenProviderSpecification> supportingProviders = ScopedSupportingTokenProviderSpecification[action];
        //            foreach (SupportingTokenProviderSpecification spec in supportingProviders)
        //            {
        //                await SecurityUtils.CloseTokenProviderIfRequiredAsync(spec.TokenProvider, timeoutHelper.RemainingTime());
        //            }
        //        }*/
        //    }
        //}

        private static void SetSecurityHeaderId(SendSecurityHeader securityHeader, Message message)
        {
            SecurityMessageProperty messageProperty = message.Properties.Security;
            if (messageProperty != null)
            {
                securityHeader.IdPrefix = messageProperty.SenderIdPrefix;
            }
        }

        private void AddSupportingTokenSpecification(SecurityMessageProperty security, IList<SecurityToken> tokens, SecurityTokenAttachmentMode attachmentMode, IDictionary<SecurityToken, ReadOnlyCollection<IAuthorizationPolicy>> tokenPoliciesMapping)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return;
            }

            for (int i = 0; i < tokens.Count; ++i)
            {
                security.IncomingSupportingTokens.Add(new SupportingTokenSpecification(tokens[i], tokenPoliciesMapping[tokens[i]], attachmentMode));
            }
        }

        protected void AddSupportingTokenSpecification(SecurityMessageProperty security, IList<SecurityToken> basicTokens, IList<SecurityToken> endorsingTokens, IList<SecurityToken> signedEndorsingTokens, IList<SecurityToken> signedTokens, IDictionary<SecurityToken, ReadOnlyCollection<IAuthorizationPolicy>> tokenPoliciesMapping)
        {
            AddSupportingTokenSpecification(security, basicTokens, SecurityTokenAttachmentMode.SignedEncrypted, tokenPoliciesMapping);
            AddSupportingTokenSpecification(security, endorsingTokens, SecurityTokenAttachmentMode.Endorsing, tokenPoliciesMapping);
            AddSupportingTokenSpecification(security, signedEndorsingTokens, SecurityTokenAttachmentMode.SignedEndorsing, tokenPoliciesMapping);
            AddSupportingTokenSpecification(security, signedTokens, SecurityTokenAttachmentMode.Signed, tokenPoliciesMapping);
        }

        protected SendSecurityHeader CreateSendSecurityHeader(Message message, string actor, SecurityProtocolFactory factory)
        {
            return CreateSendSecurityHeader(message, actor, factory, true);
        }

        protected SendSecurityHeader CreateSendSecurityHeaderForTransportProtocol(Message message, string actor, SecurityProtocolFactory factory)
        {
            return CreateSendSecurityHeader(message, actor, factory, false);
        }

        private SendSecurityHeader CreateSendSecurityHeader(Message message, string actor, SecurityProtocolFactory factory, bool requireMessageProtection)
        {
            MessageDirection transferDirection = MessageDirection.Output;
            SendSecurityHeader sendSecurityHeader = factory.StandardsManager.CreateSendSecurityHeader(
                message,
                actor, true, false,
                factory.OutgoingAlgorithmSuite, transferDirection);
            sendSecurityHeader.Layout = factory.SecurityHeaderLayout;
            sendSecurityHeader.RequireMessageProtection = requireMessageProtection;
            SetSecurityHeaderId(sendSecurityHeader, message);
            if (factory.AddTimestamp)
            {
                sendSecurityHeader.AddTimestamp(factory.TimestampValidityDuration);
            }

            sendSecurityHeader.StreamBufferManager = factory.StreamBufferManager;
            return sendSecurityHeader;
        }

        internal void AddMessageSupportingTokens(Message message, ref IList<SupportingTokenSpecification> supportingTokens)
        {
            SecurityMessageProperty supportingTokensProperty = message.Properties.Security;
            if (supportingTokensProperty != null && supportingTokensProperty.HasOutgoingSupportingTokens)
            {
                if (supportingTokens == null)
                {
                    supportingTokens = new Collection<SupportingTokenSpecification>();
                }

                for (int i = 0; i < supportingTokensProperty.OutgoingSupportingTokens.Count; ++i)
                {
                    SupportingTokenSpecification spec = supportingTokensProperty.OutgoingSupportingTokens[i];
                    if (spec.SecurityTokenParameters == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.SenderSideSupportingTokensMustSpecifySecurityTokenParameters));
                    }
                    supportingTokens.Add(spec);
                }
            }
        }

        internal Task<IList<SupportingTokenSpecification>> TryGetSupportingTokensAsync(SecurityProtocolFactory factory, EndpointAddress target, Uri via, Message message, TimeSpan timeout)
        {
            return Task.FromResult< IList<SupportingTokenSpecification>>(null);
        }

        protected ReadOnlyCollection<SecurityTokenResolver> MergeOutOfBandResolvers(IList<SupportingTokenAuthenticatorSpecification> supportingAuthenticators, ReadOnlyCollection<SecurityTokenResolver> primaryResolvers)
        {
            Collection<SecurityTokenResolver> outOfBandResolvers = null;
            if (supportingAuthenticators != null && supportingAuthenticators.Count > 0)
            {
                for (int i = 0; i < supportingAuthenticators.Count; ++i)
                {
                    if (supportingAuthenticators[i].TokenResolver != null)
                    {
                        outOfBandResolvers = outOfBandResolvers ?? new Collection<SecurityTokenResolver>();
                        outOfBandResolvers.Add(supportingAuthenticators[i].TokenResolver);
                    }
                }
            }

            if (outOfBandResolvers != null)
            {
                if (primaryResolvers != null)
                {
                    for (int i = 0; i < primaryResolvers.Count; ++i)
                    {
                        outOfBandResolvers.Insert(0, primaryResolvers[i]);
                    }
                }
                return new ReadOnlyCollection<SecurityTokenResolver>(outOfBandResolvers);
            }
            else
            {
                return primaryResolvers ?? EmptyReadOnlyCollection<SecurityTokenResolver>.Instance;
            }
        }

        protected void AddSupportingTokens(SendSecurityHeader securityHeader, IList<SupportingTokenSpecification> supportingTokens)
        {
            if (supportingTokens != null)
            {
                for (int i = 0; i < supportingTokens.Count; ++i)
                {
                    SecurityToken token = supportingTokens[i].SecurityToken;
                    SecurityTokenParameters tokenParameters = supportingTokens[i].SecurityTokenParameters;
                    switch (supportingTokens[i].SecurityTokenAttachmentMode)
                    {
                        case SecurityTokenAttachmentMode.Signed:
                            securityHeader.AddSignedSupportingToken(token, tokenParameters);
                            break;
                        case SecurityTokenAttachmentMode.Endorsing:
                            securityHeader.AddEndorsingSupportingToken(token, tokenParameters);
                            break;
                        case SecurityTokenAttachmentMode.SignedEncrypted:
                            securityHeader.AddBasicSupportingToken(token, tokenParameters);
                            break;
                        case SecurityTokenAttachmentMode.SignedEndorsing:
                            securityHeader.AddSignedEndorsingSupportingToken(token, tokenParameters);
                            break;
                        default:
                            Fx.Assert("Unknown token attachment mode " + supportingTokens[i].SecurityTokenAttachmentMode.ToString());
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnknownTokenAttachmentMode, supportingTokens[i].SecurityTokenAttachmentMode.ToString())));
                    }
                }
            }
        }

        internal static async Task<SecurityToken> GetTokenAsync(SecurityTokenProvider provider, EndpointAddress target, TimeSpan timeout)
        {
            if (provider == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.TokenProviderCannotGetTokensForTarget, target)));
            }

            SecurityToken token;
            try
            {
                token = await provider.GetTokenAsync(TimeoutHelper.GetCancellationToken(timeout));
            }
            catch (SecurityTokenException exception)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.TokenProviderCannotGetTokensForTarget, target), exception));
            }
            catch (SecurityNegotiationException sne)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityNegotiationException(SR.Format(SR.TokenProviderCannotGetTokensForTarget, target), sne));
            }

            return token;
        }

        public abstract void SecureOutgoingMessage(ref Message message);

        // subclasses that offer correlation should override this version
        public virtual (SecurityProtocolCorrelationState, Message) SecureOutgoingMessage(Message message, SecurityProtocolCorrelationState correlationState, CancellationToken token)
        {
            SecureOutgoingMessage(ref message);
            return (null, message);
        }

        protected virtual void OnOutgoingMessageSecured(Message securedMessage)
        {
        }

        protected virtual void OnSecureOutgoingMessageFailure(Message message)
        {
        }

        public abstract ValueTask<Message> VerifyIncomingMessageAsync(Message message);

        // subclasses that offer correlation should override this version
        public virtual async ValueTask<(Message, SecurityProtocolCorrelationState)> VerifyIncomingMessageAsync(Message message, params SecurityProtocolCorrelationState[] correlationStates)
        {
            var verifiedMessage = await VerifyIncomingMessageAsync(message);
            return (verifiedMessage, null);
        }

        protected virtual void OnIncomingMessageVerified(Message verifiedMessage)
        {
        }

        protected virtual void OnVerifyIncomingMessageFailure(Message message, Exception exception)
        {
        }
        protected IList<SupportingTokenAuthenticatorSpecification> GetSupportingTokenAuthenticatorsAndSetExpectationFlags(SecurityProtocolFactory factory, Message message,
    ReceiveSecurityHeader securityHeader)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }
            IList<SupportingTokenAuthenticatorSpecification> authenticators = factory.GetSupportingTokenAuthenticators(message.Headers.Action,
                out bool expectSignedTokens, out bool expectBasicTokens, out bool expectEndorsingTokens);
            securityHeader.ExpectBasicTokens = expectBasicTokens;
            securityHeader.ExpectEndorsingTokens = expectEndorsingTokens;
            securityHeader.ExpectSignedTokens = expectSignedTokens;
            return authenticators;
        }

        public Task CloseAsync(bool aborted, CancellationToken token)
        {
            if (aborted)
            {
                CommunicationObject.Abort();
            }
            else
            {
                return CommunicationObject.CloseAsync(token);
            }

            return Task.CompletedTask;
        }

        public Task OnCloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}
