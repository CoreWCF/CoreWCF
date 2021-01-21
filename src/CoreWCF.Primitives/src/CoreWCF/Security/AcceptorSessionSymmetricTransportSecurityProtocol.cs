// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal sealed class AcceptorSessionSymmetricTransportSecurityProtocol : TransportSecurityProtocol, IAcceptorSecuritySessionProtocol
    {
        private SecurityToken outgoingSessionToken;
        private SecurityTokenAuthenticator sessionTokenAuthenticator;
        private SecurityTokenResolver sessionTokenResolver;
        private ReadOnlyCollection<SecurityTokenResolver> sessionTokenResolverList;
        private UniqueId sessionId;
        private Collection<SupportingTokenAuthenticatorSpecification> sessionTokenAuthenticatorSpecificationList;
        private readonly bool requireDerivedKeys;

        public AcceptorSessionSymmetricTransportSecurityProtocol(SessionSymmetricTransportSecurityProtocolFactory factory) : base(factory, null, null)
        {
            if (factory.ActAsInitiator == true)
            {
                Fx.Assert("This protocol can only be used at the recipient.");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ProtocolMustBeRecipient, GetType().ToString())));
            }
            requireDerivedKeys = factory.SecurityTokenParameters.RequireDerivedKeys;
        }

        private SessionSymmetricTransportSecurityProtocolFactory Factory
        {
            get { return (SessionSymmetricTransportSecurityProtocolFactory)SecurityProtocolFactory; }
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
            CommunicationObject.ThrowIfDisposedOrImmutable();
            this.sessionId = sessionId;
            this.sessionTokenResolver = sessionTokenResolver;
            Collection<SecurityTokenResolver> tmp = new Collection<SecurityTokenResolver>();
            tmp.Add(this.sessionTokenResolver);
            sessionTokenResolverList = new ReadOnlyCollection<SecurityTokenResolver>(tmp);
            this.sessionTokenAuthenticator = sessionTokenAuthenticator;
            SupportingTokenAuthenticatorSpecification spec = new SupportingTokenAuthenticatorSpecification(this.sessionTokenAuthenticator, this.sessionTokenResolver, SecurityTokenAttachmentMode.Endorsing, Factory.SecurityTokenParameters);
            sessionTokenAuthenticatorSpecificationList = new Collection<SupportingTokenAuthenticatorSpecification>();
            sessionTokenAuthenticatorSpecificationList.Add(spec);
        }

        public SecurityToken GetOutgoingSessionToken()
        {
            return outgoingSessionToken;
        }

        public void SetOutgoingSessionToken(SecurityToken token)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }
            outgoingSessionToken = token;
        }

        protected override void VerifyIncomingMessageCore(ref Message message, TimeSpan timeout)
        {
            string actor = string.Empty; // message.Version.Envelope.UltimateDestinationActor;
            ReceiveSecurityHeader securityHeader = Factory.StandardsManager.TryCreateReceiveSecurityHeader(message, actor,
                Factory.IncomingAlgorithmSuite, MessageDirection.Input);
            securityHeader.RequireMessageProtection = false;
            securityHeader.ReaderQuotas = Factory.SecurityBindingElement.ReaderQuotas;
            IList<SupportingTokenAuthenticatorSpecification> supportingAuthenticators = GetSupportingTokenAuthenticatorsAndSetExpectationFlags(Factory, message, securityHeader);
            ReadOnlyCollection<SecurityTokenResolver> mergedTokenResolvers = MergeOutOfBandResolvers(supportingAuthenticators, sessionTokenResolverList);
            if (supportingAuthenticators != null && supportingAuthenticators.Count > 0)
            {
                supportingAuthenticators = new List<SupportingTokenAuthenticatorSpecification>(supportingAuthenticators);
                supportingAuthenticators.Insert(0, sessionTokenAuthenticatorSpecificationList[0]);
            }
            else
            {
                supportingAuthenticators = sessionTokenAuthenticatorSpecificationList;
            }
            securityHeader.ConfigureTransportBindingServerReceiveHeader(supportingAuthenticators);
            securityHeader.ConfigureOutOfBandTokenResolver(mergedTokenResolvers);
            securityHeader.ExpectEndorsingTokens = true;
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);

            securityHeader.ReplayDetectionEnabled = Factory.DetectReplays;
            securityHeader.SetTimeParameters(Factory.NonceCache, Factory.ReplayWindow, Factory.MaxClockSkew);
            // do not enforce key derivation requirement for Cancel messages due to WSE interop
            securityHeader.EnforceDerivedKeyRequirement = (message.Headers.Action != Factory.StandardsManager.SecureConversationDriver.CloseAction.Value);
            securityHeader.Process(timeoutHelper.RemainingTime(), SecurityUtils.GetChannelBindingFromMessage(message), Factory.ExtendedProtectionPolicy);
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
                    if (signingSct != null && signingSct.ContextId == sessionId)
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
