// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.IdentityModel
{
    /// <summary>
    /// Defines constants used in WS-Addressing standard schema.
    /// </summary>
    internal static class WSAddressing200408Constants
    {
        public const string Prefix = "wsa";
        public const string NamespaceUri = "http://schemas.xmlsoap.org/ws/2004/08/addressing";

        public static class Elements
        {
            public const string Action = "Action";
            public const string ReplyTo = "ReplyTo";
        }
    }
}
