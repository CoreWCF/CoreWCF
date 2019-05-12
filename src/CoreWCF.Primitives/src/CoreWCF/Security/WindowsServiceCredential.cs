using System;
using System.Collections.Generic;
using System.Text;

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
