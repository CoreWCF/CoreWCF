// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security
{
    [System.Obsolete("SamlSerializer a été déplacé vers CoreWCF.IdentityModel.Tokens.SamlSerializer. Veuillez utiliser le nouveau namespace.")]
    public class SamlSerializer : CoreWCF.IdentityModel.Tokens.SamlSerializer
    {
        public SamlSerializer() : base() { }
    }
}
