// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace CoreWCF.IdentityModel.Tokens
{
    internal enum SecurityKeyUsage
    {
        Exchange,
        Signature
    }

    internal static class SecurityKeyUsageHelper
    {
        internal static bool IsDefined(SecurityKeyUsage value)
        {
            return (value == SecurityKeyUsage.Exchange
                || value == SecurityKeyUsage.Signature);
        }

        internal static void Validate(SecurityKeyUsage value)
        {
            if (!IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException(nameof(value), (int)value,
                    typeof(SecurityKeyUsage)));
            }
        }
    }
}
