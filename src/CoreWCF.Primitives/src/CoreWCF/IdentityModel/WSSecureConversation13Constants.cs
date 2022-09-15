// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.IdentityModel
{
    /// <summary>
    /// Defines constants used in WS-SecureConversation standard schema.
    /// </summary>
    internal static class WSSecureConversation13Constants
    {
        public const string Namespace = "http://docs.oasis-open.org/ws-sx/ws-secureconversation/200512";
        public static readonly Uri NamespaceUri = new Uri( Namespace );
        public const string Prefix = "sc";
        public const string TokenTypeURI = "http://docs.oasis-open.org/ws-sx/ws-secureconversation/200512/sct";
        public const int DefaultDerivedKeyLength = 32;

        public static class ElementNames
        {
            public const string Name = "SecurityContextToken";
            public const string Identifier = "Identifier";
            public const string Instance = "Instance";
        }

        public static class Attributes
        {
            public const string Length = "Length";
            public const string Nonce  = "Nonce";
            public const string Instance = "Instance";
        }
    }
}
