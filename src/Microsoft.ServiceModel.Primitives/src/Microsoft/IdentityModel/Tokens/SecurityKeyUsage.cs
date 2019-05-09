using Microsoft.ServiceModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Microsoft.IdentityModel.Tokens
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
