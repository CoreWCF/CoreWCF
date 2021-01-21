// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace CoreWCF.Channels
{
    public enum SecurityHeaderLayout
    {
        Strict = 0,
        Lax = 1,
        LaxTimestampFirst = 2,
        LaxTimestampLast = 3
    }

    static class SecurityHeaderLayoutHelper
    {
        public static bool IsDefined(SecurityHeaderLayout value)
        {
            return (value == SecurityHeaderLayout.Lax
            || value == SecurityHeaderLayout.LaxTimestampFirst
            || value == SecurityHeaderLayout.LaxTimestampLast
            || value == SecurityHeaderLayout.Strict);
        }

        public static void Validate(SecurityHeaderLayout value)
        {
            if (!IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException(nameof(value), (int)value,
                    typeof(SecurityHeaderLayout)));
            }
        }
    }
}
