// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    public enum WSFederationHttpSecurityMode
    {
        None,
        Message,
        TransportWithMessageCredential
    }

    static class WSFederationHttpSecurityModeHelper
    {
        internal static bool IsDefined(WSFederationHttpSecurityMode value)
        {
            return (value == WSFederationHttpSecurityMode.None ||
                value == WSFederationHttpSecurityMode.Message ||
                value == WSFederationHttpSecurityMode.TransportWithMessageCredential);
        }
    }
}
