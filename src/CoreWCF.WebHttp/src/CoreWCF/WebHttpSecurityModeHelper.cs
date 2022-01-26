// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    internal static class WebHttpSecurityModeHelper
    {
        internal static bool IsDefined(WebHttpSecurityMode value)
        {
            return (value == WebHttpSecurityMode.None ||
                value == WebHttpSecurityMode.Transport ||
                value == WebHttpSecurityMode.TransportCredentialOnly);
        }
    }
}
