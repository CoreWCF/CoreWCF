// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using CoreWCF.Channels;
using CoreWCF.Description;

namespace CoreWCF.Security
{
    internal abstract class SecurityHeader : MessageHeader
    {
        private readonly string _actor;
        private bool _encryptedKeyContainsReferenceList = true;
        private readonly bool _mustUnderstand;
        private readonly bool _relay;
        private bool _requireMessageProtection = true;
        private bool _processingStarted;
        private bool _maintainSignatureConfirmationState;
        private SecurityHeaderLayout _layout = SecurityHeaderLayout.Strict;

        public SecurityHeader(Message message,
            string actor, bool mustUnderstand, bool relay,
            SecurityStandardsManager standardsManager
            , SecurityAlgorithmSuite algorithmSuite,
            MessageDirection transferDirection)
        {
            Message = message ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            _actor = actor ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(actor));
            _mustUnderstand = mustUnderstand;
            _relay = relay;
            StandardsManager = standardsManager ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(standardsManager));
            AlgorithmSuite = algorithmSuite ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(algorithmSuite));
            MessageDirection = transferDirection;
        }

        public override string Actor => _actor;

        public SecurityAlgorithmSuite AlgorithmSuite { get; }

        public bool EncryptedKeyContainsReferenceList
        {
            get { return _encryptedKeyContainsReferenceList; }
            set
            {
                ThrowIfProcessingStarted();
                _encryptedKeyContainsReferenceList = value;
            }
        }

        public bool RequireMessageProtection
        {
            get { return _requireMessageProtection; }
            set
            {
                ThrowIfProcessingStarted();
                _requireMessageProtection = value;
            }
        }

        public bool MaintainSignatureConfirmationState
        {
            get { return _maintainSignatureConfirmationState; }
            set
            {
                ThrowIfProcessingStarted();
                _maintainSignatureConfirmationState = value;
            }
        }

        protected Message Message { get; set; }

        public override bool MustUnderstand => _mustUnderstand;

        public override bool Relay => _relay;

        public SecurityHeaderLayout Layout
        {
            get
            {
                return _layout;
            }
            set
            {
                ThrowIfProcessingStarted();
                _layout = value;
            }
        }

        public SecurityStandardsManager StandardsManager { get; }

        public MessageDirection MessageDirection { get; }

        protected MessageVersion Version => Message.Version;

        protected void SetProcessingStarted()
        {
            _processingStarted = true;
        }

        protected void ThrowIfProcessingStarted()
        {
            if (_processingStarted)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.OperationCannotBeDoneAfterProcessingIsStarted));
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}(Actor = '{1}')", GetType().Name, Actor);
        }
    }
}
