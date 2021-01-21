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
        private ChannelProtectionRequirements protectionRequirements = new ChannelProtectionRequirements();
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(factory));

            this.applyIntegrity = factory.applyIntegrity;
            this.applyConfidentiality = factory.applyConfidentiality;
            this.identityVerifier = factory.identityVerifier;
            this.protectionRequirements = new ChannelProtectionRequirements(factory.protectionRequirements);
            this.messageProtectionOrder = factory.messageProtectionOrder;
            this.requireIntegrity = factory.requireIntegrity;
            this.requireConfidentiality = factory.requireConfidentiality;
            this.doRequestSignatureConfirmation = factory.doRequestSignatureConfirmation;
        }

        public bool ApplyConfidentiality
        {
            get
            {
                return this.applyConfidentiality;
            }
            set
            {
                ThrowIfImmutable();
                this.applyConfidentiality = value;
            }
        }

        public bool ApplyIntegrity
        {
            get
            {
                return this.applyIntegrity;
            }
            set
            {
                ThrowIfImmutable();
                this.applyIntegrity = value;
            }
        }

        public bool DoRequestSignatureConfirmation
        {
            get
            {
                return this.doRequestSignatureConfirmation;
            }
            set
            {
                ThrowIfImmutable();
                this.doRequestSignatureConfirmation = value;
            }
        }

        public IdentityVerifier IdentityVerifier
        {
            get
            {
                return this.identityVerifier;
            }
            set
            {
                ThrowIfImmutable();
                this.identityVerifier = value;
            }
        }

        public ChannelProtectionRequirements ProtectionRequirements
        {
            get
            {
                return this.protectionRequirements;
            }
        }

        public MessageProtectionOrder MessageProtectionOrder
        {
            get
            {
                return this.messageProtectionOrder;
            }
            set
            {
                ThrowIfImmutable();
                this.messageProtectionOrder = value;
            }
        }

        public bool RequireIntegrity
        {
            get
            {
                return this.requireIntegrity;
            }
            set
            {
                ThrowIfImmutable();
                this.requireIntegrity = value;
            }
        }

        public bool RequireConfidentiality
        {
            get
            {
                return this.requireConfidentiality;
            }
            set
            {
                ThrowIfImmutable();
                this.requireConfidentiality = value;
            }
        }

        internal List<SecurityTokenAuthenticator> WrappedKeySecurityTokenAuthenticator
        {
            get
            {
                return this.wrappedKeyTokenAuthenticator;
            }
        }

        protected virtual void ValidateCorrelationSecuritySettings()
        {
            if (this.ActAsInitiator && this.SupportsRequestReply)
            {
                bool savesCorrelationTokenOnRequest = this.ApplyIntegrity || this.ApplyConfidentiality;
                bool needsCorrelationTokenOnReply = this.RequireIntegrity || this.RequireConfidentiality;
                if (!savesCorrelationTokenOnRequest && needsCorrelationTokenOnReply)
                {
                    OnPropertySettingsError("ApplyIntegrity", false);
                }
            }
        }

        public override Task OnOpenAsync(TimeSpan timeout)
        {
            base.OnOpenAsync(timeout);
            this.protectionRequirements.MakeReadOnly();

            if (this.DetectReplays && !this.RequireIntegrity)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(RequireIntegrity), SR.ForReplayDetectionToBeDoneRequireIntegrityMustBeSet);
            }

            if (this.DoRequestSignatureConfirmation)
            {
                if (!this.SupportsRequestReply)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.SignatureConfirmationRequiresRequestReply);
                }
                //TODO fix below
                //if (!this.StandardsManager.SecurityVersion.SupportsSignatureConfirmation)
                //{
                //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SecurityVersionDoesNotSupportSignatureConfirmation, this.StandardsManager.SecurityVersion));
                //}
            }

            this.wrappedKeyTokenAuthenticator = new List<SecurityTokenAuthenticator>(1);
            SecurityTokenAuthenticator authenticator = new NonValidatingSecurityTokenAuthenticator<WrappedKeySecurityToken>();
            this.wrappedKeyTokenAuthenticator.Add(authenticator);

            ValidateCorrelationSecuritySettings();
            return Task.CompletedTask;
        }

        private static MessagePartSpecification ExtractMessageParts(string action,
            ScopedMessagePartSpecification scopedParts, bool isForSignature)
        {
            MessagePartSpecification parts = null;

            if (scopedParts.TryGetParts(action, out parts))
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
            if (this.RequireConfidentiality)
            {
                if (this.IsDuplexReply)
                    return ExtractMessageParts(action, this.ProtectionRequirements.OutgoingEncryptionParts, false);
                else
                    return ExtractMessageParts(action, (this.ActAsInitiator) ? this.ProtectionRequirements.OutgoingEncryptionParts : this.ProtectionRequirements.IncomingEncryptionParts, false);
            }
            else
            {
                return MessagePartSpecification.NoParts;
            }
        }

        internal MessagePartSpecification GetIncomingSignatureParts(string action)
        {
            if (this.RequireIntegrity)
            {
                if (this.IsDuplexReply)
                    return ExtractMessageParts(action, this.ProtectionRequirements.OutgoingSignatureParts, true);
                else
                    return ExtractMessageParts(action, (this.ActAsInitiator) ? this.ProtectionRequirements.OutgoingSignatureParts : this.ProtectionRequirements.IncomingSignatureParts, true);
            }
            else
            {
                return MessagePartSpecification.NoParts;
            }
        }

        internal MessagePartSpecification GetOutgoingEncryptionParts(string action)
        {
            if (this.ApplyConfidentiality)
            {
                if (this.IsDuplexReply)
                    return ExtractMessageParts(action, this.ProtectionRequirements.OutgoingEncryptionParts, false);
                else
                    return ExtractMessageParts(action, (this.ActAsInitiator) ? this.ProtectionRequirements.IncomingEncryptionParts : this.ProtectionRequirements.OutgoingEncryptionParts, false);
            }
            else
            {
                return MessagePartSpecification.NoParts;
            }
        }

        internal MessagePartSpecification GetOutgoingSignatureParts(string action)
        {
            if (this.ApplyIntegrity)
            {
                if (this.IsDuplexReply)
                    return ExtractMessageParts(action, this.ProtectionRequirements.OutgoingSignatureParts, true);
                else
                    return ExtractMessageParts(action, (this.ActAsInitiator) ? this.ProtectionRequirements.IncomingSignatureParts : this.ProtectionRequirements.OutgoingSignatureParts, true);
            }
            else
            {
                return MessagePartSpecification.NoParts;
            }
        }
    }
}
