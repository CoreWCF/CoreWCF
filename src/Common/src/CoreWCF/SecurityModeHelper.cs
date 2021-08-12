// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    internal static class SecurityModeHelper
    {
        internal static bool IsDefined(SecurityMode value)
        {
            return (value == SecurityMode.None ||
                value == SecurityMode.Transport ||
                value == SecurityMode.Message ||
                value == SecurityMode.TransportWithMessageCredential);
        }
    }
}
