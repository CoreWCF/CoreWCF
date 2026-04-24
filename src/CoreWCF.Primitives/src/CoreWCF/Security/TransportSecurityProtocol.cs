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

namespace CoreWCF.Security
{
    internal class TransportSecurityProtocol : SecurityProtocol
    {
        public TransportSecurityProtocol(TransportSecurityProtocolFactory factory, EndpointAddress target, Uri via) : base(factory, target, via)
        {
        }

        public override void SecureOutgoingMessage(ref Message message)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }
            CommunicationObject.ThrowIfClosedOrNotOpen();
            string actor = string.Empty; // message.Version.Envelope.UltimateDestinationActor;
            try
            {
                SecureOutgoingMessageAtResponder(ref message, actor);
                base.OnOutgoingMessageSecured(message);
            }
            catch
            {
                base.OnSecureOutgoingMessageFailure(message);
                throw;
            }
        }

        protected virtual void SecureOutgoingMessageAtResponder(ref Message message, string actor)
        {
            if (SecurityProtocolFactory.AddTimestamp && !SecurityProtocolFactory.SecurityBindingElement.EnableUnsecuredResponse)
            {
                SendSecurityHeader securityHeader = CreateSendSecurityHeaderForTransportProtocol(message, actor, SecurityProtocolFactory);
                message = securityHeader.SetupExecution();
            }
        }

        public sealed override async ValueTask<Message> VerifyIncomingMessageAsync(Message message)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }
            CommunicationObject.ThrowIfClosedOrNotOpen();
            try
            {
                Message verifiedMessage = await VerifyIncomingMessageCoreAsync(message);
                return verifiedMessage;

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

        protected virtual async ValueTask<Message> VerifyIncomingMessageCoreAsync(Message message)
        {
            TransportSecurityProtocolFactory factory = (TransportSecurityProtocolFactory)SecurityProtocolFactory;
            string actor = string.Empty; // message.Version.Envelope.UltimateDestinationActor;

            ReceiveSecurityHeader securityHeader = factory.StandardsManager.TryCreateReceiveSecurityHeader(message, actor,
                factory.IncomingAlgorithmSuite, MessageDirection.Input);
            IList<SupportingTokenAuthenticatorSpecification> supportingAuthenticators = factory.GetSupportingTokenAuthenticators(message.Headers.Action,
                out bool expectSignedTokens, out bool expectBasicTokens, out bool expectEndorsingTokens);
            if (securityHeader == null)
            {
                bool expectSupportingTokens = expectEndorsingTokens || expectSignedTokens || expectBasicTokens;
                if (!factory.AddTimestamp && !expectSupportingTokens)
                {
                    return message;
                }
                else
                {
                    if (string.IsNullOrEmpty(actor))
                    {
                        throw Diagnostics.TraceUtility.ThrowHelperError(new MessageSecurityException(
                            SR.Format(SR.UnableToFindSecurityHeaderInMessageNoActor)), message);
                    }
                    else
                    {
                        throw Diagnostics.TraceUtility.ThrowHelperError(new MessageSecurityException(
                            SR.Format(SR.UnableToFindSecurityHeaderInMessage, actor)), message);
                    }
                }
            }

            securityHeader.RequireMessageProtection = false;
            securityHeader.ExpectBasicTokens = expectBasicTokens;
            securityHeader.ExpectSignedTokens = expectSignedTokens;
            securityHeader.ExpectEndorsingTokens = expectEndorsingTokens;
            securityHeader.MaxReceivedMessageSize = factory.SecurityBindingElement.MaxReceivedMessageSize;
            securityHeader.ReaderQuotas = factory.SecurityBindingElement.ReaderQuotas;

            // This was behind an app setting on WCF. If this breaks someone, it's because they are setting SecurityHeaderLayout and it
            // wasn't being applied. The customer fix is to not set the SecurityHeaderLayout as that's what they were effectively running with.
             securityHeader.Layout = factory.SecurityHeaderLayout;

            securityHeader.ConfigureTransportBindingServerReceiveHeader(supportingAuthenticators);
            securityHeader.ConfigureOutOfBandTokenResolver(MergeOutOfBandResolvers(supportingAuthenticators, EmptyReadOnlyCollection<SecurityTokenResolver>.Instance));
            if (factory.ExpectKeyDerivation)
            {
                securityHeader.DerivedTokenAuthenticator = factory.DerivedKeyTokenAuthenticator;
            }
            securityHeader.ReplayDetectionEnabled = factory.DetectReplays;
            securityHeader.SetTimeParameters(factory.NonceCache, factory.ReplayWindow, factory.MaxClockSkew);
            await securityHeader.ProcessAsync(SecurityUtils.GetChannelBindingFromMessage(message), factory.ExtendedProtectionPolicy);
            Message processedMessage = securityHeader.ProcessedMessage;
                AttachRecipientSecurityProperty(processedMessage, securityHeader.BasicSupportingTokens, securityHeader.EndorsingSupportingTokens, securityHeader.SignedEndorsingSupportingTokens,
                    securityHeader.SignedSupportingTokens, securityHeader.SecurityTokenAuthorizationPoliciesMapping);
            base.OnIncomingMessageVerified(processedMessage);

            return processedMessage;
        }
    }
}
