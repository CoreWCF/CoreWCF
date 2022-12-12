// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    public enum NetNamedPipeSecurityMode
    {
        None,
        Transport
    }

    internal static class NetNamedPipeSecurityModeHelper
    {
        internal static bool IsDefined(NetNamedPipeSecurityMode value)
        {
            return
                value == NetNamedPipeSecurityMode.Transport ||
                value == NetNamedPipeSecurityMode.None;
        }
    }
}
