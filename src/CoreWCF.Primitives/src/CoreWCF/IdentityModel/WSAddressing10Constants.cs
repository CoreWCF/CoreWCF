// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.IdentityModel
{
    /// <summary>
    /// Defines constants used in WS-Addressing 1.0 standard schema.
    /// </summary>
    internal static class WSAddressing10Constants
    {
        public const string Prefix = "wsa";
        public const string NamespaceUri = "http://www.w3.org/2005/08/addressing";

        public static class Elements
        {
            public const string Action = "Action";
            public const string Address = "Address";
            public const string ReplyTo = "ReplyTo";
            public const string EndpointReference = "EndpointReference";
        }
    }
}
