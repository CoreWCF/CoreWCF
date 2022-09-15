// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security.Claims
{
    /// <summary>
    /// Defines the keys for properties contained in <see cref="Claim.Properties"/>.
    /// </summary>
    public static class ClaimProperties
    {
        public const string Namespace = "http://schemas.xmlsoap.org/ws/2005/05/identity/claimproperties";

        public const string SamlAttributeDisplayName            = Namespace + "/displayname";
        public const string SamlAttributeNameFormat             = Namespace + "/attributename";
        public const string SamlNameIdentifierFormat            = Namespace + "/format";
        public const string SamlNameIdentifierNameQualifier     = Namespace + "/namequalifier";
        public const string SamlNameIdentifierSPNameQualifier   = Namespace + "/spnamequalifier";
        public const string SamlNameIdentifierSPProvidedId      = Namespace + "/spprovidedid";
    }
}
