// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;

namespace CoreWCF.Security
{
    public sealed class SecureConversationServiceCredential
    {
        private static readonly SecurityStateEncoder defaultSecurityStateEncoder = new DataProtectionSecurityStateEncoder();
        private SecurityStateEncoder securityStateEncoder;
        private bool isReadOnly;

        internal SecureConversationServiceCredential()
        {
            securityStateEncoder = defaultSecurityStateEncoder;
            SecurityContextClaimTypes = new Collection<Type>();
            // SamlAssertion.AddSamlClaimTypes(securityContextClaimTypes);
        }

        internal SecureConversationServiceCredential(SecureConversationServiceCredential other)
        {
            securityStateEncoder = other.securityStateEncoder;
            SecurityContextClaimTypes = new Collection<Type>();
            for (int i = 0; i < other.SecurityContextClaimTypes.Count; ++i)
            {
                SecurityContextClaimTypes.Add(other.SecurityContextClaimTypes[i]);
            }
            isReadOnly = other.isReadOnly;
        }

        public SecurityStateEncoder SecurityStateEncoder
        {
            get
            {
                return securityStateEncoder;
            }
            set
            {
                ThrowIfImmutable();
                securityStateEncoder = value;
            }
        }

        public Collection<Type> SecurityContextClaimTypes { get; }

        internal void MakeReadOnly()
        {
            isReadOnly = true;
        }

        private void ThrowIfImmutable()
        {
            if (isReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }
        }
    }
}
