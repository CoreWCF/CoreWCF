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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("message");
            }
            if (actor == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("actor");
            }
            if (standardsManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("standardsManager");
            }
            if (algorithmSuite == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("algorithmSuite");
            }

            this.message = message;
            this.actor = actor;
            this.mustUnderstand = mustUnderstand;
            this.relay = relay;
            this.standardsManager = standardsManager;
            this.algorithmSuite = algorithmSuite;
            this.transferDirection = transferDirection;
        }

        public override string Actor
        {
            get { return this.actor; }
        }

        public SecurityAlgorithmSuite AlgorithmSuite
        {
            get { return this.algorithmSuite; }
        }

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

        public override bool MustUnderstand
        {
            get { return this.mustUnderstand; }
        }

        public override bool Relay
        {
            get { return this.relay; }
        }

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

        public SecurityStandardsManager StandardsManager
        {
            get { return this.standardsManager; }
        }

        public MessageDirection MessageDirection
        {
            get { return this.transferDirection; }
        }

        protected MessageVersion Version
        {
            get { return this.message.Version; }
        }

        protected void SetProcessingStarted()
        {
            this.processingStarted = true;
        }

        protected void ThrowIfProcessingStarted()
        {
            if (this.processingStarted)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.OperationCannotBeDoneAfterProcessingIsStarted)));
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}(Actor = '{1}')", GetType().Name, this.Actor);
        }
    }
}
