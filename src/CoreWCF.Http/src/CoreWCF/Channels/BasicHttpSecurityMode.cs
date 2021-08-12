// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    /// <summary>
    /// Enum BasicHttpSecurityMode
    /// </summary>
    public enum BasicHttpSecurityMode
    {
        /// <summary>
        /// No security is applied during transfer. (Default)
        /// </summary>
        None,

        /// <summary>
        /// Transfer security is provided using HTTPS. The server is authenticated via its SSL certificate. The client authentication is determined by ClientCredentialType.
        /// </summary>
        Transport,

        /// <summary>
        /// The message
        /// </summary>
        Message,

        /// <summary>
        /// Transfer security is provided using HTTPS. The server is authenticated via its SSL certificate. The client authentication is included directly in the message.
        /// </summary>
        /// <remarks>See https://docs.microsoft.com/en-us/dotnet/framework/wcf/samples/ws-transport-with-message-credential for more details.</remarks>
        TransportWithMessageCredential,

        /// <summary>
        /// TransportCredentialOnly will send the credentials in plain text and unencrypted and is only recommended for testing or when the transfer security is being provided by other means (such as IPSec).
        /// </summary>
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
