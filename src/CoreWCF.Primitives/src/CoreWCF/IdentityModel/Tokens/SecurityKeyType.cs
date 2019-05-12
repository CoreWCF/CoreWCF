using CoreWCF;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace CoreWCF.IdentityModel.Tokens
{
    internal enum SecurityKeyType
    {
        SymmetricKey,
        AsymmetricKey,
        BearerKey
    }

    internal static class SecurityKeyTypeHelper
    {
        internal static bool IsDefined(SecurityKeyType value)
        {
            return (value == SecurityKeyType.SymmetricKey
                || value == SecurityKeyType.AsymmetricKey
                || value == SecurityKeyType.BearerKey);
        }

        internal static void Validate(SecurityKeyType value)
        {
            if (!IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException("value", (int)value,
                    typeof(SecurityKeyType)));
            }
        }


    }
}
