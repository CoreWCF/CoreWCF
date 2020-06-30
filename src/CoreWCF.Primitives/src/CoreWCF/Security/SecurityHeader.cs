using System.Globalization;
using CoreWCF.Channels;
using CoreWCF;
using CoreWCF.Description;
using System.Xml;
using System;

namespace CoreWCF.Security
{


  public abstract class SecurityHeader : MessageHeader
    {
        readonly string actor;
        readonly SecurityAlgorithmSuite algorithmSuite;
        bool encryptedKeyContainsReferenceList = true;
        Message message;
        readonly bool mustUnderstand;
        readonly bool relay;
        bool requireMessageProtection = true;
        bool processingStarted;
        bool maintainSignatureConfirmationState;
       readonly SecurityStandardsManager standardsManager;
        MessageDirection transferDirection;
        SecurityHeaderLayout layout = SecurityHeaderLayout.Strict;

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
