// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Security
{
    public sealed class WindowsServiceCredential
    {
        private bool _allowAnonymousLogons = SspiSecurityTokenProvider.DefaultAllowUnauthenticatedCallers;
        private bool _includeWindowsGroups = SspiSecurityTokenProvider.DefaultExtractWindowsGroupClaims;
        private bool _isReadOnly;
        private LdapSettings _ldapSettings;

        internal WindowsServiceCredential()
        {
            // empty
        }

        internal WindowsServiceCredential(WindowsServiceCredential other)
        {
            _allowAnonymousLogons = other._allowAnonymousLogons;
            _includeWindowsGroups = other._includeWindowsGroups;
            _isReadOnly = other._isReadOnly;
            _ldapSettings = other._ldapSettings;
        }

        public bool AllowAnonymousLogons
        {
            get
            {
                return _allowAnonymousLogons;
            }
            set
            {
                ThrowIfImmutable();
                _allowAnonymousLogons = value;
            }
        }

        public bool IncludeWindowsGroups
        {
            get
            {
                return _includeWindowsGroups;
            }
            set
            {
                ThrowIfImmutable();
                _includeWindowsGroups = value;
            }
        }

        public LdapSettings LdapSetting
        {
            get
            {
                return _ldapSettings;
            }
            set
            {
                ThrowIfImmutable();
                value.Validate();
                _ldapSettings = value;
            }
        }


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
