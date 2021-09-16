// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.IdentityModel
{
    /// <summary>
    /// Defines constants from WS-Utility specification
    /// </summary>
    internal static class WSUtilityConstants
    {
        public const string NamespaceURI = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
        public const string Prefix = "wsu";

        public static class Attributes
        {
            public const string IdAttribute = "Id";            
        }

        public static class ElementNames
        {
            public const string Created = "Created";
            public const string Expires = "Expires";
        }
    }
}
