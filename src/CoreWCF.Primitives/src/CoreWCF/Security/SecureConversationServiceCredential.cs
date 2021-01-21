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
        private readonly Collection<Type> securityContextClaimTypes;
        private bool isReadOnly;

        internal SecureConversationServiceCredential()
        {
            securityStateEncoder = defaultSecurityStateEncoder;
            securityContextClaimTypes = new Collection<Type>();
            // SamlAssertion.AddSamlClaimTypes(securityContextClaimTypes);
        }

        internal SecureConversationServiceCredential(SecureConversationServiceCredential other)
        {
            securityStateEncoder = other.securityStateEncoder;
            securityContextClaimTypes = new Collection<Type>();
            for (int i = 0; i < other.securityContextClaimTypes.Count; ++i)
            {
                securityContextClaimTypes.Add(other.securityContextClaimTypes[i]);
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

        public Collection<Type> SecurityContextClaimTypes
        {
            get { return securityContextClaimTypes; }
        }

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
