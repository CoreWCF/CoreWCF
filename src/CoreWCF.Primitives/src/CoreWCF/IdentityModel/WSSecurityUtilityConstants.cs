// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.IdentityModel
{
    /// <summary>
    /// Defines constants used in WS-SecureUtility standard schema.
    /// </summary>
    internal static class WSSecurityUtilityConstants
    {
        public const string Namespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
        public const string Prefix    = "wsu";

        public static class Attributes
        {
            public const string Id = "Id";
        }
    }
}
