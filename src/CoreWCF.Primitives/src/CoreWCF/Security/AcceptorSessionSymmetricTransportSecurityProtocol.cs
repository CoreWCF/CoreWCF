using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;

namespace CoreWCF.Security
{
    sealed class AcceptorSessionSymmetricTransportSecurityProtocol : TransportSecurityProtocol, IAcceptorSecuritySessionProtocol
    {
        SecurityToken outgoingSessionToken;
        SecurityTokenAuthenticator sessionTokenAuthenticator;
        SecurityTokenResolver sessionTokenResolver;
        ReadOnlyCollection<SecurityTokenResolver> sessionTokenResolverList;
        UniqueId sessionId;
        Collection<SupportingTokenAuthenticatorSpecification> sessionTokenAuthenticatorSpecificationList;
        bool requireDerivedKeys;

        public AcceptorSessionSymmetricTransportSecurityProtocol(SessionSymmetricTransportSecurityProtocolFactory factory) : base(factory, null, null)
        {
            if (factory.ActAsInitiator == true)
            {
                Fx.Assert("This protocol can only be used at the recipient.");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ProtocolMustBeRecipient, this.GetType().ToString())));
            }
            this.requireDerivedKeys = factory.SecurityTokenParameters.RequireDerivedKeys;
        }

        SessionSymmetricTransportSecurityProtocolFactory Factory
        {
            get { return (SessionSymmetricTransportSecurityProtocolFactory)this.SecurityProtocolFactory; }
        }

        public bool ReturnCorrelationState
        {
            get
            {
                return false;
            }
            set
            {
            }
        }

        public void SetSessionTokenAuthenticator(UniqueId sessionId, SecurityTokenAuthenticator sessionTokenAuthenticator, SecurityTokenResolver sessionTokenResolver)
        {
            this.CommunicationObject.ThrowIfDisposedOrImmutable();
            this.sessionId = sessionId;
            this.sessionTokenResolver = sessionTokenResolver;
            Collection<SecurityTokenResolver> tmp = new Collection<SecurityTokenResolver>();
            tmp.Add(this.sessionTokenResolver);
            this.sessionTokenResolverList = new ReadOnlyCollection<SecurityTokenResolver>(tmp);
            this.sessionTokenAuthenticator = sessionTokenAuthenticator;
            SupportingTokenAuthenticatorSpecification spec = new SupportingTokenAuthenticatorSpecification(this.sessionTokenAuthenticator, this.sessionTokenResolver, SecurityTokenAttachmentMode.Endorsing, this.Factory.SecurityTokenParameters);
            this.sessionTokenAuthenticatorSpecificationList = new Collection<SupportingTokenAuthenticatorSpecification>();
            this.sessionTokenAuthenticatorSpecificationList.Add(spec);
        }

        public SecurityToken GetOutgoingSessionToken()
        {
            return this.outgoingSessionToken;
        }

        public void SetOutgoingSessionToken(SecurityToken token)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }
            this.outgoingSessionToken = token;
        }

        protected override void VerifyIncomingMessageCore(ref Message message, TimeSpan timeout)
        {
            string actor = string.Empty; // message.Version.Envelope.UltimateDestinationActor;
            ReceiveSecurityHeader securityHeader = this.Factory.StandardsManager.TryCreateReceiveSecurityHeader(message, actor,
                this.Factory.IncomingAlgorithmSuite, MessageDirection.Input);
            securityHeader.RequireMessageProtection = false;
            securityHeader.ReaderQuotas = this.Factory.SecurityBindingElement.ReaderQuotas;
            IList<SupportingTokenAuthenticatorSpecification> supportingAuthenticators = GetSupportingTokenAuthenticatorsAndSetExpectationFlags(this.Factory, message, securityHeader);
            ReadOnlyCollection<SecurityTokenResolver> mergedTokenResolvers = MergeOutOfBandResolvers(supportingAuthenticators, this.sessionTokenResolverList);
            if (supportingAuthenticators != null && supportingAuthenticators.Count > 0)
            {
                supportingAuthenticators = new List<SupportingTokenAuthenticatorSpecification>(supportingAuthenticators);
                supportingAuthenticators.Insert(0, this.sessionTokenAuthenticatorSpecificationList[0]);
            }
            else
            {
                supportingAuthenticators = this.sessionTokenAuthenticatorSpecificationList;
            }
            securityHeader.ConfigureTransportBindingServerReceiveHeader(supportingAuthenticators);
            securityHeader.ConfigureOutOfBandTokenResolver(mergedTokenResolvers);
            securityHeader.ExpectEndorsingTokens = true;
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            
            securityHeader.ReplayDetectionEnabled = this.Factory.DetectReplays;
            securityHeader.SetTimeParameters(this.Factory.NonceCache, this.Factory.ReplayWindow, this.Factory.MaxClockSkew);
            // do not enforce key derivation requirement for Cancel messages due to WSE interop
            securityHeader.EnforceDerivedKeyRequirement = (message.Headers.Action != this.Factory.StandardsManager.SecureConversationDriver.CloseAction.Value);
            securityHeader.Process(timeoutHelper.RemainingTime(), SecurityUtils.GetChannelBindingFromMessage(message), this.Factory.ExtendedProtectionPolicy);
            if (securityHeader.Timestamp == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.RequiredTimestampMissingInSecurityHeader)));
            }
            bool didSessionSctEndorse = false;
            if (securityHeader.EndorsingSupportingTokens != null)
            {
                for (int i = 0; i < securityHeader.EndorsingSupportingTokens.Count; ++i)
                {
                    SecurityContextSecurityToken signingSct = (securityHeader.EndorsingSupportingTokens[i] as SecurityContextSecurityToken);
                    if (signingSct != null && signingSct.ContextId == this.sessionId)
                    {
                        didSessionSctEndorse = true;
                        break;
                    }
                }
            }
            if (!didSessionSctEndorse)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.NoSessionTokenPresentInMessage)));
            }
            message = securityHeader.ProcessedMessage;
            AttachRecipientSecurityProperty(message, securityHeader.BasicSupportingTokens, securityHeader.EndorsingSupportingTokens,
                securityHeader.SignedEndorsingSupportingTokens, securityHeader.SignedSupportingTokens, securityHeader.SecurityTokenAuthorizationPoliciesMapping);
            base.OnIncomingMessageVerified(message);
        }
    }
}
