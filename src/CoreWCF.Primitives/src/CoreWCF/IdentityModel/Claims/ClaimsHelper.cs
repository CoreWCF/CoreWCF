// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Claims;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.IdentityModel.Claims
{
    internal static class ClaimsHelper
    {
        public static string FindUpn(ClaimsIdentity claimsIdentity)
        {
            string upn = null;
            foreach (System.Security.Claims.Claim claim in claimsIdentity.Claims)
            {
                if (StringComparer.Ordinal.Equals(ClaimTypes.Upn, claim.Type))
                {
                    // Complain if we already found a UPN claim
                    if (upn != null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.ID1053)));
                    }
                    upn = claim.Value;
                }
            }

            if (string.IsNullOrEmpty(upn))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.ID1054)));
            }
            return upn;
        }
    }
}
