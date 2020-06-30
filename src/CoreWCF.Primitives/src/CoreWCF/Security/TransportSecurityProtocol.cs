using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using System.Runtime;
using CoreWCF.Runtime;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Description;
using System;
using System.Threading.Tasks;
using CoreWCF.IdentityModel;

namespace CoreWCF.Security
{
     class TransportSecurityProtocol : SecurityProtocol
    {
        public TransportSecurityProtocol(TransportSecurityProtocolFactory factory, EndpointAddress target, Uri via)
            : base(factory, target, via)
        {
        }

        public override async Task<Message> SecureOutgoingMessageAsync(Message message)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }
           
            //TODO communication object
          //  CommunicationObject.ThrowIfClosedOrNotOpen();
            string actor = string.Empty; // message.Version.Envelope.UltimateDestinationActor;
            try
            {
                if (SecurityProtocolFactory.ActAsInitiator)
                {
                    //serverside no need to worry
                  //  message = await SecureOutgoingMessageAtInitiatorAsync(message, actor, timeout);
                }
                else
                {
                    message = await SecureOutgoingMessageAtResponderAsync(message, actor);
                }
                base.OnOutgoingMessageSecured(message);
            }
            catch
            {
                base.OnSecureOutgoingMessageFailure(message);
                throw;
            }
            
            return message;
        }

        protected virtual async Task<Message> SecureOutgoingMessageAtResponderAsync(Message message, string actor)
        {
            if (this.SecurityProtocolFactory.AddTimestamp && !this.SecurityProtocolFactory.SecurityBindingElement.EnableUnsecuredResponse)
            {
                SendSecurityHeader securityHeader = CreateSendSecurityHeaderForTransportProtocol(message, actor, this.SecurityProtocolFactory);
                message = securityHeader.SetupExecution();
            }
            return message;
        }

        /*

        protected virtual async Task<Message> SecureOutgoingMessageAtInitiatorAsync(Message message, string actor, TimeSpan timeout)
        {
            IList<SupportingTokenSpecification> supportingTokens = await TryGetSupportingTokensAsync(SecurityProtocolFactory, Target, Via, message, timeout);
            SetUpDelayedSecurityExecution(ref message, actor, supportingTokens);
            return message;
        }

        internal void SetUpDelayedSecurityExecution(ref Message message, string actor,
            IList<SupportingTokenSpecification> supportingTokens)
        {
            SendSecurityHeader securityHeader = CreateSendSecurityHeaderForTransportProtocol(message, actor, SecurityProtocolFactory);
            AddSupportingTokens(securityHeader, supportingTokens);
            message = securityHeader.SetupExecution();
        }*/

        public sealed override void VerifyIncomingMessage(ref Message message, TimeSpan timeout)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }
            //CommunicationObject.ThrowIfClosedOrNotOpen();
            try
            {
                VerifyIncomingMessageCore(ref message, timeout);
            }
            catch (MessageSecurityException e)
            {
                base.OnVerifyIncomingMessageFailure(message, e);
                throw;
            }
            catch (Exception e)
            {
                // Always immediately rethrow fatal exceptions.
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                base.OnVerifyIncomingMessageFailure(message, e);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.MessageSecurityVerificationFailed, e));
            }
        }

        protected void AttachRecipientSecurityProperty(Message message, IList<SecurityToken> basicTokens, IList<SecurityToken> endorsingTokens,
           IList<SecurityToken> signedEndorsingTokens, IList<SecurityToken> signedTokens, Dictionary<SecurityToken, ReadOnlyCollection<IAuthorizationPolicy>> tokenPoliciesMapping)
        {
            SecurityMessageProperty security = SecurityMessageProperty.GetOrCreate(message);
            AddSupportingTokenSpecification(security, basicTokens, endorsingTokens, signedEndorsingTokens, signedTokens, tokenPoliciesMapping);
            security.ServiceSecurityContext = new ServiceSecurityContext(security.GetInitiatorTokenAuthorizationPolicies());
        }

        protected virtual void VerifyIncomingMessageCore(ref Message message, TimeSpan timeout)
        {
            TransportSecurityProtocolFactory factory = (TransportSecurityProtocolFactory)this.SecurityProtocolFactory;
            string actor = string.Empty; // message.Version.Envelope.UltimateDestinationActor;

            ReceiveSecurityHeader securityHeader = factory.StandardsManager.TryCreateReceiveSecurityHeader(message, actor,
                factory.IncomingAlgorithmSuite, (factory.ActAsInitiator) ? MessageDirection.Output : MessageDirection.Input);
            bool expectBasicTokens;
            bool expectEndorsingTokens;
            bool expectSignedTokens;
            IList<SupportingTokenAuthenticatorSpecification> supportingAuthenticators = factory.GetSupportingTokenAuthenticators(message.Headers.Action,
                out expectSignedTokens, out expectBasicTokens, out expectEndorsingTokens);
            if (securityHeader == null)
            {
                bool expectSupportingTokens = expectEndorsingTokens || expectSignedTokens || expectBasicTokens;
                if ((factory.ActAsInitiator && (!factory.AddTimestamp || factory.SecurityBindingElement.EnableUnsecuredResponse))
                    || (!factory.ActAsInitiator && !factory.AddTimestamp && !expectSupportingTokens))
                {
                    return;
                }
                else
                {
                    if (String.IsNullOrEmpty(actor))
                        throw Diagnostics.TraceUtility.ThrowHelperError(new MessageSecurityException(
                            SR.Format(SR.UnableToFindSecurityHeaderInMessageNoActor)), message);
                    else
                        throw Diagnostics.TraceUtility.ThrowHelperError(new MessageSecurityException(
                            SR.Format(SR.UnableToFindSecurityHeaderInMessage, actor)), message);
                }
            }

            securityHeader.RequireMessageProtection = false;
            securityHeader.ExpectBasicTokens = expectBasicTokens;
            securityHeader.ExpectSignedTokens = expectSignedTokens;
            securityHeader.ExpectEndorsingTokens = expectEndorsingTokens;
            securityHeader.MaxReceivedMessageSize = factory.SecurityBindingElement.MaxReceivedMessageSize;
            securityHeader.ReaderQuotas = factory.SecurityBindingElement.ReaderQuotas;

            // Due to compatibility, only honor this setting if this app setting is enabled
           // if (ServiceModelAppSettings.UseConfiguredTransportSecurityHeaderLayout)
           // {
            //    securityHeader.Layout = factory.SecurityHeaderLayout;
          //  }

            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            if (!factory.ActAsInitiator)
            {
                securityHeader.ConfigureTransportBindingServerReceiveHeader(supportingAuthenticators);
                securityHeader.ConfigureOutOfBandTokenResolver(MergeOutOfBandResolvers(supportingAuthenticators, EmptyReadOnlyCollection<SecurityTokenResolver>.Instance));
                if (factory.ExpectKeyDerivation)
                {
                    securityHeader.DerivedTokenAuthenticator = factory.DerivedKeyTokenAuthenticator;
                }
            }
            securityHeader.ReplayDetectionEnabled = factory.DetectReplays;
            securityHeader.SetTimeParameters(factory.NonceCache, factory.ReplayWindow, factory.MaxClockSkew);
            securityHeader.Process(timeoutHelper.RemainingTime(), SecurityUtils.GetChannelBindingFromMessage(message), factory.ExtendedProtectionPolicy);
            message = securityHeader.ProcessedMessage;
            if (!factory.ActAsInitiator)
            {
                AttachRecipientSecurityProperty(message, securityHeader.BasicSupportingTokens, securityHeader.EndorsingSupportingTokens, securityHeader.SignedEndorsingSupportingTokens,
                    securityHeader.SignedSupportingTokens, securityHeader.SecurityTokenAuthorizationPoliciesMapping);
            }

            base.OnIncomingMessageVerified(message);
        }
    }
}
