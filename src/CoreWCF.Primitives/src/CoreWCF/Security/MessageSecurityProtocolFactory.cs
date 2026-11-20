// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
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
        private bool _applyIntegrity = true;
        private bool _applyConfidentiality = true;
        private bool _doRequestSignatureConfirmation = defaultDoRequestSignatureConfirmation;
        private IdentityVerifier _identityVerifier;
        private MessageProtectionOrder _messageProtectionOrder = defaultMessageProtectionOrder;
        private bool _requireIntegrity = true;
        private bool _requireConfidentiality = true;

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

            _applyIntegrity = factory._applyIntegrity;
            _applyConfidentiality = factory._applyConfidentiality;
            _identityVerifier = factory._identityVerifier;
            ProtectionRequirements = new ChannelProtectionRequirements(factory.ProtectionRequirements);
            _messageProtectionOrder = factory._messageProtectionOrder;
            _requireIntegrity = factory._requireIntegrity;
            _requireConfidentiality = factory._requireConfidentiality;
            _doRequestSignatureConfirmation = factory._doRequestSignatureConfirmation;
        }

        public bool ApplyConfidentiality
        {
            get
            {
                return _applyConfidentiality;
            }
            set
            {
                ThrowIfImmutable();
                _applyConfidentiality = value;
            }
        }

        public bool ApplyIntegrity
        {
            get
            {
                return _applyIntegrity;
            }
            set
            {
                ThrowIfImmutable();
                _applyIntegrity = value;
            }
        }

        public bool DoRequestSignatureConfirmation
        {
            get
            {
                return _doRequestSignatureConfirmation;
            }
            set
            {
                ThrowIfImmutable();
                _doRequestSignatureConfirmation = value;
            }
        }

        public IdentityVerifier IdentityVerifier
        {
            get
            {
                return _identityVerifier;
            }
            set
            {
                ThrowIfImmutable();
                _identityVerifier = value;
            }
        }

        public ChannelProtectionRequirements ProtectionRequirements { get; } = new ChannelProtectionRequirements();

        public MessageProtectionOrder MessageProtectionOrder
        {
            get
            {
                return _messageProtectionOrder;
            }
            set
            {
                ThrowIfImmutable();
                _messageProtectionOrder = value;
            }
        }

        public bool RequireIntegrity
        {
            get
            {
                return _requireIntegrity;
            }
            set
            {
                ThrowIfImmutable();
                _requireIntegrity = value;
            }
        }

        public bool RequireConfidentiality
        {
            get
            {
                return _requireConfidentiality;
            }
            set
            {
                ThrowIfImmutable();
                _requireConfidentiality = value;
            }
        }

        internal List<SecurityTokenAuthenticator> WrappedKeySecurityTokenAuthenticator { get; private set; }

        protected virtual void ValidateCorrelationSecuritySettings()
        {
        }

        public override async Task OnOpenAsync(CancellationToken token)
        {
            await base.OnOpenAsync(token);
            ProtectionRequirements.MakeReadOnly();

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

            WrappedKeySecurityTokenAuthenticator = new List<SecurityTokenAuthenticator>(1);
            SecurityTokenAuthenticator authenticator = new NonValidatingSecurityTokenAuthenticator<WrappedKeySecurityToken>();
            WrappedKeySecurityTokenAuthenticator.Add(authenticator);

            ValidateCorrelationSecuritySettings();
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
                    return ExtractMessageParts(action, ProtectionRequirements.IncomingEncryptionParts, false);
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
                    return ExtractMessageParts(action, ProtectionRequirements.IncomingSignatureParts, true);
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
                return ExtractMessageParts(action, ProtectionRequirements.OutgoingEncryptionParts, false);
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
                return ExtractMessageParts(action, ProtectionRequirements.OutgoingSignatureParts, true);
            }
            else
            {
                return MessagePartSpecification.NoParts;
            }
        }
    }
}
