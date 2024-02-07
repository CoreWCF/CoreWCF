// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Security.Tokens;
using System.Xml;
using System;
using System.Threading.Tasks;

namespace CoreWCF.Security
{
    internal sealed class AcceptorSessionSymmetricMessageSecurityProtocol : MessageSecurityProtocol, IAcceptorSecuritySessionProtocol
    {
        private SecurityToken outgoingSessionToken;
        private SecurityTokenAuthenticator sessionTokenAuthenticator;
        private SecurityTokenResolver sessionTokenResolver;
        private ReadOnlyCollection<SecurityTokenResolver> sessionResolverList;
        private bool returnCorrelationState = false;
        private DerivedKeySecurityToken derivedSignatureToken;
        private DerivedKeySecurityToken derivedEncryptionToken;
        private UniqueId sessionId;
        private SecurityStandardsManager sessionStandardsManager;
        private Object thisLock = new Object();
        private bool requireDerivedKeys;

        public AcceptorSessionSymmetricMessageSecurityProtocol(SessionSymmetricMessageSecurityProtocolFactory factory,
            EndpointAddress target)
            : base(factory, target, null)
        {
            if (factory.ActAsInitiator == true)
            {
                Fx.Assert("This protocol can only be used at the recipient.");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ProtocolMustBeRecipient, GetType().ToString())));
            }
            requireDerivedKeys = factory.SecurityTokenParameters.RequireDerivedKeys;
            if (requireDerivedKeys)
            {
                SecurityTokenSerializer innerTokenSerializer = Factory.StandardsManager.SecurityTokenSerializer;
                WSSecureConversation secureConversation = (innerTokenSerializer is WSSecurityTokenSerializer) ? ((WSSecurityTokenSerializer)innerTokenSerializer).SecureConversation : new WSSecurityTokenSerializer(Factory.MessageSecurityVersion.SecurityVersion).SecureConversation;
                sessionStandardsManager = new SecurityStandardsManager(factory.MessageSecurityVersion, new DerivedKeyCachingSecurityTokenSerializer(2, false, secureConversation, innerTokenSerializer));
            }
        }

        private Object ThisLock
        {
            get
            {
                return thisLock;
            }
        }

        public bool ReturnCorrelationState 
        {
            get
            {
                return returnCorrelationState;
            }
            set
            {
                returnCorrelationState = value;
            }
        }

        protected override bool PerformIncomingAndOutgoingMessageExpectationChecks
        {
            get { return false; }
        }

        private SessionSymmetricMessageSecurityProtocolFactory Factory
        {
            get { return (SessionSymmetricMessageSecurityProtocolFactory)base.MessageSecurityProtocolFactory; }
        }

        public SecurityToken GetOutgoingSessionToken()
        {
            lock (ThisLock)
            {
                return outgoingSessionToken;
            }
        }

        public void SetOutgoingSessionToken(SecurityToken token)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("token");
            }
            lock (ThisLock)
            {
                outgoingSessionToken = token;
                if (requireDerivedKeys)
                {
                    string derivationAlgorithm = SecurityUtils.GetKeyDerivationAlgorithm(sessionStandardsManager.MessageSecurityVersion.SecureConversationVersion);

                    derivedSignatureToken = new DerivedKeySecurityToken(-1, 0, 
                        Factory.OutgoingAlgorithmSuite.GetSignatureKeyDerivationLength(token, sessionStandardsManager.MessageSecurityVersion.SecureConversationVersion), null, 
                        DerivedKeySecurityToken.DefaultNonceLength, token, Factory.SecurityTokenParameters.CreateKeyIdentifierClause(token, SecurityTokenReferenceStyle.External), derivationAlgorithm, SecurityUtils.GenerateId());

                    derivedEncryptionToken = new DerivedKeySecurityToken(-1, 0,
                        Factory.OutgoingAlgorithmSuite.GetEncryptionKeyDerivationLength(token, sessionStandardsManager.MessageSecurityVersion.SecureConversationVersion), null,
                        DerivedKeySecurityToken.DefaultNonceLength, token, Factory.SecurityTokenParameters.CreateKeyIdentifierClause(token, SecurityTokenReferenceStyle.External), derivationAlgorithm, SecurityUtils.GenerateId());
                }
            }
        }

        public void SetSessionTokenAuthenticator(UniqueId sessionId, SecurityTokenAuthenticator sessionTokenAuthenticator, SecurityTokenResolver sessionTokenResolver)
        {
            CommunicationObject.ThrowIfDisposedOrImmutable();
            lock (ThisLock)
            {
                this.sessionId = sessionId;
                this.sessionTokenAuthenticator = sessionTokenAuthenticator;
                this.sessionTokenResolver = sessionTokenResolver;
                List<SecurityTokenResolver> tmp = new List<SecurityTokenResolver>(1);
                tmp.Add(this.sessionTokenResolver);
                sessionResolverList = new ReadOnlyCollection<SecurityTokenResolver>(tmp);
            }
        }

        private void GetTokensForOutgoingMessages(out SecurityToken signingToken, out SecurityToken encryptionToken, out SecurityTokenParameters tokenParameters)
        {
            lock (ThisLock)
            {
                if (requireDerivedKeys)
                {
                    signingToken = derivedSignatureToken;
                    encryptionToken = derivedEncryptionToken;
                }
                else
                {
                    signingToken = encryptionToken = outgoingSessionToken;
                }
            }
            tokenParameters = Factory.GetTokenParameters();
        }

        protected override SecurityProtocolCorrelationState SecureOutgoingMessageCore(ref Message message, TimeSpan timeout, SecurityProtocolCorrelationState correlationState)
        {
            SecurityToken signingToken;
            SecurityToken encryptionToken;
            SecurityTokenParameters tokenParameters;
            GetTokensForOutgoingMessages(out signingToken, out encryptionToken, out tokenParameters);
            SetUpDelayedSecurityExecution(ref message, signingToken, encryptionToken, tokenParameters, correlationState);
            return null;
        }

        private void SetUpDelayedSecurityExecution(ref Message message, SecurityToken signingToken, SecurityToken encryptionToken, 
            SecurityTokenParameters tokenParameters, SecurityProtocolCorrelationState correlationState)
        {
            string actor = string.Empty;
            SendSecurityHeader securityHeader = ConfigureSendSecurityHeader(message, actor, null, correlationState);
            if (Factory.ApplyIntegrity)
            {
                securityHeader.SetSigningToken(signingToken, tokenParameters);
            }
            if (Factory.ApplyConfidentiality)
            {
                securityHeader.SetEncryptionToken(encryptionToken, tokenParameters);
            }
            message = securityHeader.SetupExecution();
        }

        protected override async Task<Tuple<SecurityProtocolCorrelationState, Message>> VerifyIncomingMessageCoreAsync(Message message, string actor, TimeSpan timeout, SecurityProtocolCorrelationState[] correlationStates)
        {
            SessionSymmetricMessageSecurityProtocolFactory factory = Factory;
            IList<SupportingTokenAuthenticatorSpecification> supportingAuthenticators;
            ReceiveSecurityHeader securityHeader = ConfigureReceiveSecurityHeader(message, string.Empty, correlationStates, (requireDerivedKeys) ? sessionStandardsManager : null, out supportingAuthenticators);
            securityHeader.ConfigureSymmetricBindingServerReceiveHeader(sessionTokenAuthenticator, Factory.SecurityTokenParameters, supportingAuthenticators);
            securityHeader.ConfigureOutOfBandTokenResolver(MergeOutOfBandResolvers(supportingAuthenticators, sessionResolverList));
            // do not enforce key derivation requirement for Cancel messages due to WSE interop
            securityHeader.EnforceDerivedKeyRequirement = (message.Headers.Action != factory.StandardsManager.SecureConversationDriver.CloseAction.Value);
            await ProcessSecurityHeaderAsync(securityHeader, message, null, timeout, correlationStates);
            SecurityToken signingToken = securityHeader.SignatureToken;
            SecurityContextSecurityToken signingSct = (signingToken as SecurityContextSecurityToken);
            if (signingSct == null || signingSct.ContextId != sessionId)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.NoSessionTokenPresentInMessage)));
            }
            AttachRecipientSecurityProperty(message, signingToken, false, securityHeader.BasicSupportingTokens, securityHeader.EndorsingSupportingTokens, securityHeader.SignedEndorsingSupportingTokens,
                securityHeader.SignedSupportingTokens, securityHeader.SecurityTokenAuthorizationPoliciesMapping);
            return new(GetCorrelationState(null, securityHeader), message);
        }
    }
}
