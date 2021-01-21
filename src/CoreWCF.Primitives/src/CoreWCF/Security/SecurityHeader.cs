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
        private readonly string actor;
        private readonly SecurityAlgorithmSuite algorithmSuite;
        private bool encryptedKeyContainsReferenceList = true;
        private Message message;
        private readonly bool mustUnderstand;
        private readonly bool relay;
        private bool requireMessageProtection = true;
        private bool processingStarted;
        private bool maintainSignatureConfirmationState;
        private readonly SecurityStandardsManager standardsManager;
        private readonly MessageDirection transferDirection;
        private SecurityHeaderLayout layout = SecurityHeaderLayout.Strict;

        public SecurityHeader(Message message,
            string actor, bool mustUnderstand, bool relay,
            SecurityStandardsManager standardsManager
            , SecurityAlgorithmSuite algorithmSuite,
            MessageDirection transferDirection)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }
            if (actor == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(actor));
            }
            if (standardsManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(standardsManager));
            }
            if (algorithmSuite == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(algorithmSuite));
            }

            this.message = message;
            this.actor = actor;
            this.mustUnderstand = mustUnderstand;
            this.relay = relay;
            this.standardsManager = standardsManager;
            this.algorithmSuite = algorithmSuite;
            this.transferDirection = transferDirection;
        }

        public override string Actor => actor;

        public SecurityAlgorithmSuite AlgorithmSuite => algorithmSuite;

        public bool EncryptedKeyContainsReferenceList
        {
            get { return encryptedKeyContainsReferenceList; }
            set
            {
                ThrowIfProcessingStarted();
                encryptedKeyContainsReferenceList = value;
            }
        }

        public bool RequireMessageProtection
        {
            get { return requireMessageProtection; }
            set
            {
                ThrowIfProcessingStarted();
                requireMessageProtection = value;
            }
        }

        public bool MaintainSignatureConfirmationState
        {
            get { return maintainSignatureConfirmationState; }
            set
            {
                ThrowIfProcessingStarted();
                maintainSignatureConfirmationState = value;
            }
        }

        protected Message Message
        {
            get { return message; }
            set { message = value; }
        }

        public override bool MustUnderstand => mustUnderstand;

        public override bool Relay => relay;

        public SecurityHeaderLayout Layout
        {
            get
            {
                return layout;
            }
            set
            {
                ThrowIfProcessingStarted();
                layout = value;
            }
        }

        public SecurityStandardsManager StandardsManager => standardsManager;

        public MessageDirection MessageDirection => transferDirection;

        protected MessageVersion Version => message.Version;

        protected void SetProcessingStarted()
        {
            processingStarted = true;
        }

        protected void ThrowIfProcessingStarted()
        {
            if (processingStarted)
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
