


using CoreWCF;
using System.Xml;
using System;

namespace CoreWCF.Security
{
    internal abstract class SecureConversationDriver
    {
        public virtual XmlDictionaryString CloseAction
        {
            get
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SecureConversationDriverVersionDoesNotSupportSession));
            }
        }

        public virtual XmlDictionaryString CloseResponseAction
        {
            get
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SecureConversationDriverVersionDoesNotSupportSession));
            }
        }

        public virtual bool IsSessionSupported
        {
            get
            {
                return false;
            }
        }

        public abstract XmlDictionaryString IssueAction { get; }

        public abstract XmlDictionaryString IssueResponseAction { get; }

        public virtual XmlDictionaryString RenewAction
        {
            get
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SecureConversationDriverVersionDoesNotSupportSession));
            }
        }

        public virtual XmlDictionaryString RenewResponseAction
        {
            get
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SecureConversationDriverVersionDoesNotSupportSession));
            }
        }

        public abstract XmlDictionaryString Namespace { get; }

        public abstract XmlDictionaryString RenewNeededFaultCode { get; }

        public abstract XmlDictionaryString BadContextTokenFaultCode { get; }

        public abstract string TokenTypeUri { get; }

        public abstract UniqueId GetSecurityContextTokenId(XmlDictionaryReader reader);
        public abstract bool IsAtSecurityContextToken(XmlDictionaryReader reader);
    }
}
