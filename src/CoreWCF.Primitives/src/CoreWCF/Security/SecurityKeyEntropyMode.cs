// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace CoreWCF.Security
{
    public enum SecurityKeyEntropyMode
    {
        ClientEntropy,
        ServerEntropy,
        CombinedEntropy
    }

    internal sealed class SecurityKeyEntropyModeHelper
    {
        internal static bool IsDefined(SecurityKeyEntropyMode value)
        {
            return (value == SecurityKeyEntropyMode.ClientEntropy
                || value == SecurityKeyEntropyMode.ServerEntropy
                || value == SecurityKeyEntropyMode.CombinedEntropy);
        }

        internal static void Validate(SecurityKeyEntropyMode value)
        {
            if (!IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException(nameof(value), (int)value,
                    typeof(SecurityKeyEntropyMode)));
            }
        }
    }
}
