namespace CoreWCF.Security.Tokens
{
    interface ISecurityContextSecurityTokenCacheProvider
    {
        ISecurityContextSecurityTokenCache TokenCache { get; }
    }
}
