// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.IdentityModel
{
    /// <summary>
    /// Defines constants used in WS-Security 1.1 standard schema.
    /// </summary>
    internal static class WSSecurity11Constants
    {
        public const string FragmentBaseAddress = "http://docs.oasis-open.org/wss/oasis-wss-soap-message-security-1.1";
        public const string Namespace           = "http://docs.oasis-open.org/wss/oasis-wss-wssecurity-secext-1.1.xsd";
        public const string Prefix              = "wsse11";

        public static class Attributes
        {
            public const string TokenType = "TokenType";
        }

        public static class KeyTypes
        {
            public const string CardSpaceV1Sha1Thumbprint = "http://docs.oasis-open.org/wss/2004/xx/oasis-2004xx-wss-soap-message-security-1.1#ThumbprintSHA1";
            public const string Sha1Thumbprint            = FragmentBaseAddress + "#ThumbprintSHA1";
        }
    }
}
