// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    public enum BasicHttpSecurityMode
    {
        None,
        Transport,
        Message,
        TransportWithMessageCredential,
        TransportCredentialOnly
    }

    internal static class BasicHttpSecurityModeHelper
    {
        internal static bool IsDefined(BasicHttpSecurityMode value)
        {
            return (value == BasicHttpSecurityMode.None ||
                value == BasicHttpSecurityMode.Transport ||
                value == BasicHttpSecurityMode.Message ||
                value == BasicHttpSecurityMode.TransportWithMessageCredential ||
                value == BasicHttpSecurityMode.TransportCredentialOnly);
        }

        internal static BasicHttpSecurityMode ToSecurityMode(UnifiedSecurityMode value)
        {
            switch (value)
            {
                case UnifiedSecurityMode.None:
                    return BasicHttpSecurityMode.None;
                case UnifiedSecurityMode.Transport:
                    return BasicHttpSecurityMode.Transport;
                case UnifiedSecurityMode.Message:
                    return BasicHttpSecurityMode.Message;
                case UnifiedSecurityMode.TransportWithMessageCredential:
                    return BasicHttpSecurityMode.TransportWithMessageCredential;
                case UnifiedSecurityMode.TransportCredentialOnly:
                    return BasicHttpSecurityMode.TransportCredentialOnly;
                default:
                    return (BasicHttpSecurityMode)value;
            }
        }
    }
}
