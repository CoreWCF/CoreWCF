// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security.Tokens
{
    using System.ComponentModel;

    public enum SecurityTokenReferenceStyle
    {
        Internal = 0,
        External = 1,
    }

    internal static class TokenReferenceStyleHelper
    {
        public static bool IsDefined(SecurityTokenReferenceStyle value)
        {
            return (value == SecurityTokenReferenceStyle.External || value == SecurityTokenReferenceStyle.Internal);
        }

        public static void Validate(SecurityTokenReferenceStyle value)
        {
            if (!IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException(nameof(value), (int)value,
                    typeof(SecurityTokenReferenceStyle)));
            }
        }
    }
}

