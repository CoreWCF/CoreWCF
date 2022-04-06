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
        public static bool IsDefined(SecurityKeyType value)
        {
            return (value == SecurityKeyType.SymmetricKey
                || value == SecurityKeyType.AsymmetricKey
                || value == SecurityKeyType.BearerKey);
        }

        public static void Validate(SecurityKeyType value)
        {
            if (!IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException(nameof(value), (int)value,
                    typeof(SecurityKeyType)));
            }
        }
    }
}
