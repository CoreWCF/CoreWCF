// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;

namespace CoreWCF.Security
{
    internal abstract class SecureConversationDriver
    {
        public virtual XmlDictionaryString CloseAction => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SecureConversationDriverVersionDoesNotSupportSession));

        public virtual XmlDictionaryString CloseResponseAction => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SecureConversationDriverVersionDoesNotSupportSession));

        public virtual bool IsSessionSupported => false;

        public abstract XmlDictionaryString IssueAction { get; }

        public abstract XmlDictionaryString IssueResponseAction { get; }

        public virtual XmlDictionaryString RenewAction => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SecureConversationDriverVersionDoesNotSupportSession));

        public virtual XmlDictionaryString RenewResponseAction => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SecureConversationDriverVersionDoesNotSupportSession));

        public abstract XmlDictionaryString Namespace { get; }

        public abstract XmlDictionaryString RenewNeededFaultCode { get; }

        public abstract XmlDictionaryString BadContextTokenFaultCode { get; }

        public abstract string TokenTypeUri { get; }

        public abstract UniqueId GetSecurityContextTokenId(XmlDictionaryReader reader);
        public abstract bool IsAtSecurityContextToken(XmlDictionaryReader reader);
    }
}
