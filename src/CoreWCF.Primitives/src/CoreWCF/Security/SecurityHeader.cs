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
        private MessageDirection transferDirection;
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

        public override string Actor => this.actor;

        public SecurityAlgorithmSuite AlgorithmSuite => this.algorithmSuite;

        public bool EncryptedKeyContainsReferenceList
        {
            get { return this.encryptedKeyContainsReferenceList; }
            set
            {
                ThrowIfProcessingStarted();
                this.encryptedKeyContainsReferenceList = value;
            }
        }

        public bool RequireMessageProtection
        {
            get { return this.requireMessageProtection; }
            set
            {
                ThrowIfProcessingStarted();
                this.requireMessageProtection = value;
            }
        }

        public bool MaintainSignatureConfirmationState
        {
            get { return this.maintainSignatureConfirmationState; }
            set
            {
                ThrowIfProcessingStarted();
                this.maintainSignatureConfirmationState = value;
            }
        }

        protected Message Message
        {
            get { return this.message; }
            set { this.message = value; }
        }

        public override bool MustUnderstand => this.mustUnderstand;

        public override bool Relay => this.relay;

        public SecurityHeaderLayout Layout
        {
            get
            {
                return this.layout;
            }
            set
            {
                ThrowIfProcessingStarted();
                this.layout = value;
            }
        }

        public SecurityStandardsManager StandardsManager => this.standardsManager;

        public MessageDirection MessageDirection => this.transferDirection;

        protected MessageVersion Version => this.message.Version;

        protected void SetProcessingStarted()
        {
            this.processingStarted = true;
        }

        protected void ThrowIfProcessingStarted()
        {
            if (this.processingStarted)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.OperationCannotBeDoneAfterProcessingIsStarted));
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}(Actor = '{1}')", GetType().Name, this.Actor);
        }
    }
}
