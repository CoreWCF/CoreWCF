// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security.Tokens
{
    public delegate void IssuedSecurityTokenHandler(SecurityToken issuedToken, EndpointAddress tokenRequestor);
    public delegate void RenewedSecurityTokenHandler(SecurityToken newSecurityToken, SecurityToken oldSecurityToken);

    public interface IIssuanceSecurityTokenAuthenticator
    {
        IssuedSecurityTokenHandler IssuedSecurityTokenHandler { get; set; }
        RenewedSecurityTokenHandler RenewedSecurityTokenHandler { get; set; }
    }
}
