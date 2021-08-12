// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;

namespace CoreWCF.Security
{
    public sealed class SecureConversationServiceCredential
    {
        private static readonly SecurityStateEncoder s_defaultSecurityStateEncoder = new DataProtectionSecurityStateEncoder();
        private SecurityStateEncoder _securityStateEncoder;
        private bool _isReadOnly;

        internal SecureConversationServiceCredential()
        {
            _securityStateEncoder = s_defaultSecurityStateEncoder;
            SecurityContextClaimTypes = new Collection<Type>();
            // SamlAssertion.AddSamlClaimTypes(securityContextClaimTypes);
        }

        internal SecureConversationServiceCredential(SecureConversationServiceCredential other)
        {
            _securityStateEncoder = other._securityStateEncoder;
            SecurityContextClaimTypes = new Collection<Type>();
            for (int i = 0; i < other.SecurityContextClaimTypes.Count; ++i)
            {
                SecurityContextClaimTypes.Add(other.SecurityContextClaimTypes[i]);
            }
            _isReadOnly = other._isReadOnly;
        }

        public SecurityStateEncoder SecurityStateEncoder
        {
            get
            {
                return _securityStateEncoder;
            }
            set
            {
                ThrowIfImmutable();
                _securityStateEncoder = value;
            }
        }

        public Collection<Type> SecurityContextClaimTypes { get; }

        internal void MakeReadOnly()
        {
            _isReadOnly = true;
        }

        private void ThrowIfImmutable()
        {
            if (_isReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }
        }
    }
}
