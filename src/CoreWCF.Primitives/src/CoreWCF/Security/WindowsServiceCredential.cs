// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Security
{
    public sealed class WindowsServiceCredential
    {
        bool allowAnonymousLogons = SspiSecurityTokenProvider.DefaultAllowUnauthenticatedCallers;
        bool includeWindowsGroups = SspiSecurityTokenProvider.DefaultExtractWindowsGroupClaims;
        bool isReadOnly;

        internal WindowsServiceCredential()
        {
            // empty
        }

        internal WindowsServiceCredential(WindowsServiceCredential other)
        {
            allowAnonymousLogons = other.allowAnonymousLogons;
            includeWindowsGroups = other.includeWindowsGroups;
            isReadOnly = other.isReadOnly;
        }

        public bool AllowAnonymousLogons
        {
            get
            {
                return allowAnonymousLogons;
            }
            set
            {
                ThrowIfImmutable();
                allowAnonymousLogons = value;
            }
        }

        public bool IncludeWindowsGroups
        {
            get
            {
                return includeWindowsGroups;
            }
            set
            {
                ThrowIfImmutable();
                includeWindowsGroups = value;
            }
        }

        internal void MakeReadOnly()
        {
            isReadOnly = true;
        }

        void ThrowIfImmutable()
        {
            if (isReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }
        }
    }

}
