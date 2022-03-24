// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace CoreWCF.IdentityModel.Tokens
{
    public enum SecurityKeyType
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
