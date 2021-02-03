// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Principal;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security
{
    internal static class SecurityUtils
    {
        public static void ValidateAnonymityConstraint(WindowsIdentity identity, bool allowUnauthenticatedCallers)
        {
            if (!allowUnauthenticatedCallers && identity.User.IsWellKnown(WellKnownSidType.AnonymousSid))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(
                    new SecurityTokenValidationException(SR.AnonymousLogonsAreNotAllowed));
            }
        }
    }
}
