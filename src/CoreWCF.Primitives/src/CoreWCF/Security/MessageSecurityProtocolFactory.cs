// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal abstract class MessageSecurityProtocolFactory : SecurityProtocolFactory
    {
        internal const MessageProtectionOrder defaultMessageProtectionOrder = MessageProtectionOrder.SignBeforeEncrypt;
        internal const bool defaultDoRequestSignatureConfirmation = false;
        private bool applyIntegrity = true;
        private bool applyConfidentiality = true;
        private bool doRequestSignatureConfirmation = defaultDoRequestSignatureConfirmation;
        private IdentityVerifier identityVerifier;
        private readonly ChannelProtectionRequirements protectionRequirements = new ChannelProtectionRequirements();
        private MessageProtectionOrder messageProtectionOrder = defaultMessageProtectionOrder;
        private bool requireIntegrity = true;
        private bool requireConfidentiality = true;
        private List<SecurityTokenAuthenticator> wrappedKeyTokenAuthenticator;

        protected MessageSecurityProtocolFactory()
        {
        }

        internal MessageSecurityProtocolFactory(MessageSecurityProtocolFactory factory)
            : base(factory)
        {
            if (factory == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(factory));
            }

            applyIntegrity = factory.applyIntegrity;
            applyConfidentiality = factory.applyConfidentiality;
            identityVerifier = factory.identityVerifier;
            protectionRequirements = new ChannelProtectionRequirements(factory.protectionRequirements);
            messageProtectionOrder = factory.messageProtectionOrder;
            requireIntegrity = factory.requireIntegrity;
            requireConfidentiality = factory.requireConfidentiality;
            doRequestSignatureConfirmation = factory.doRequestSignatureConfirmation;
        }

        public bool ApplyConfidentiality
        {
            get
            {
                return applyConfidentiality;
            }
            set
            {
                ThrowIfImmutable();
                applyConfidentiality = value;
            }
        }

        public bool ApplyIntegrity
        {
            get
            {
                return applyIntegrity;
            }
            set
            {
                ThrowIfImmutable();
                applyIntegrity = value;
            }
        }

        public bool DoRequestSignatureConfirmation
        {
            get
            {
                return doRequestSignatureConfirmation;
            }
            set
            {
                ThrowIfImmutable();
                doRequestSignatureConfirmation = value;
            }
        }

        public IdentityVerifier IdentityVerifier
        {
            get
            {
                return identityVerifier;
            }
            set
            {
                ThrowIfImmutable();
                identityVerifier = value;
            }
        }

        public ChannelProtectionRequirements ProtectionRequirements
        {
            get
            {
                return protectionRequirements;
            }
        }

        public MessageProtectionOrder MessageProtectionOrder
        {
            get
            {
                return messageProtectionOrder;
            }
            set
            {
                ThrowIfImmutable();
                messageProtectionOrder = value;
            }
        }

        public bool RequireIntegrity
        {
            get
            {
                return requireIntegrity;
            }
            set
            {
                ThrowIfImmutable();
                requireIntegrity = value;
            }
        }

        public bool RequireConfidentiality
        {
            get
            {
                return requireConfidentiality;
            }
            set
            {
                ThrowIfImmutable();
                requireConfidentiality = value;
            }
        }

        internal List<SecurityTokenAuthenticator> WrappedKeySecurityTokenAuthenticator
        {
            get
            {
                return wrappedKeyTokenAuthenticator;
            }
        }

        protected virtual void ValidateCorrelationSecuritySettings()
        {
            if (ActAsInitiator && SupportsRequestReply)
            {
                bool savesCorrelationTokenOnRequest = ApplyIntegrity || ApplyConfidentiality;
                bool needsCorrelationTokenOnReply = RequireIntegrity || RequireConfidentiality;
                if (!savesCorrelationTokenOnRequest && needsCorrelationTokenOnReply)
                {
                    OnPropertySettingsError("ApplyIntegrity", false);
                }
            }
        }

        public override Task OnOpenAsync(TimeSpan timeout)
        {
            base.OnOpenAsync(timeout);
            protectionRequirements.MakeReadOnly();

            if (DetectReplays && !RequireIntegrity)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(RequireIntegrity), SR.ForReplayDetectionToBeDoneRequireIntegrityMustBeSet);
            }

            if (DoRequestSignatureConfirmation)
            {
                if (!SupportsRequestReply)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.SignatureConfirmationRequiresRequestReply);
                }
                //TODO fix below
                //if (!this.StandardsManager.SecurityVersion.SupportsSignatureConfirmation)
                //{
                //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SecurityVersionDoesNotSupportSignatureConfirmation, this.StandardsManager.SecurityVersion));
                //}
            }

            wrappedKeyTokenAuthenticator = new List<SecurityTokenAuthenticator>(1);
            SecurityTokenAuthenticator authenticator = new NonValidatingSecurityTokenAuthenticator<WrappedKeySecurityToken>();
            wrappedKeyTokenAuthenticator.Add(authenticator);

            ValidateCorrelationSecuritySettings();
            return Task.CompletedTask;
        }

        private static MessagePartSpecification ExtractMessageParts(string action,
            ScopedMessagePartSpecification scopedParts, bool isForSignature)
        {

            if (scopedParts.TryGetParts(action, out MessagePartSpecification parts))
            {
                return parts;
            }
            else if (scopedParts.TryGetParts(MessageHeaders.WildcardAction, out parts))
            {
                return parts;
            }

            // send back a fault indication that the action is unknown
            SecurityVersion wss = MessageSecurityVersion.Default.SecurityVersion;
            FaultCode subCode = new FaultCode(wss.InvalidSecurityFaultCode.Value, wss.HeaderNamespace.Value);
            FaultCode senderCode = FaultCode.CreateSenderFaultCode(subCode);
            FaultReason reason = new FaultReason(SR.Format(SR.InvalidOrUnrecognizedAction, action), System.Globalization.CultureInfo.CurrentCulture);
            MessageFault fault = MessageFault.CreateFault(senderCode, reason);
            if (isForSignature)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.NoSignaturePartsSpecified, action), null, fault));
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.NoEncryptionPartsSpecified, action), null, fault));
            }
        }

        internal MessagePartSpecification GetIncomingEncryptionParts(string action)
        {
            if (RequireConfidentiality)
            {
                if (IsDuplexReply)
                {
                    return ExtractMessageParts(action, ProtectionRequirements.OutgoingEncryptionParts, false);
                }
                else
                {
                    return ExtractMessageParts(action, (ActAsInitiator) ? ProtectionRequirements.OutgoingEncryptionParts : ProtectionRequirements.IncomingEncryptionParts, false);
                }
            }
            else
            {
                return MessagePartSpecification.NoParts;
            }
        }

        internal MessagePartSpecification GetIncomingSignatureParts(string action)
        {
            if (RequireIntegrity)
            {
                if (IsDuplexReply)
                {
                    return ExtractMessageParts(action, ProtectionRequirements.OutgoingSignatureParts, true);
                }
                else
                {
                    return ExtractMessageParts(action, (ActAsInitiator) ? ProtectionRequirements.OutgoingSignatureParts : ProtectionRequirements.IncomingSignatureParts, true);
                }
            }
            else
            {
                return MessagePartSpecification.NoParts;
            }
        }

        internal MessagePartSpecification GetOutgoingEncryptionParts(string action)
        {
            if (ApplyConfidentiality)
            {
                if (IsDuplexReply)
                {
                    return ExtractMessageParts(action, ProtectionRequirements.OutgoingEncryptionParts, false);
                }
                else
                {
                    return ExtractMessageParts(action, (ActAsInitiator) ? ProtectionRequirements.IncomingEncryptionParts : ProtectionRequirements.OutgoingEncryptionParts, false);
                }
            }
            else
            {
                return MessagePartSpecification.NoParts;
            }
        }

        internal MessagePartSpecification GetOutgoingSignatureParts(string action)
        {
            if (ApplyIntegrity)
            {
                if (IsDuplexReply)
                {
                    return ExtractMessageParts(action, ProtectionRequirements.OutgoingSignatureParts, true);
                }
                else
                {
                    return ExtractMessageParts(action, (ActAsInitiator) ? ProtectionRequirements.IncomingSignatureParts : ProtectionRequirements.OutgoingSignatureParts, true);
                }
            }
            else
            {
                return MessagePartSpecification.NoParts;
            }
        }
    }
}
