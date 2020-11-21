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
