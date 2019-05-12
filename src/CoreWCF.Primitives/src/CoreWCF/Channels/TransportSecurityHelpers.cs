using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security;
using CoreWCF.Security.Tokens;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    internal static class TransportSecurityHelpers
    {
        static async Task<T> GetTokenAsync<T>(SecurityTokenProvider tokenProvider, CancellationToken token)
            where T : SecurityToken
        {
            SecurityToken result = await tokenProvider.GetTokenAsync(token);
            if ((result != null) && !(result is T))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(
                    SR.InvalidTokenProvided, tokenProvider.GetType(), typeof(T))));
            }
            return result as T;
        }

        // used by server WindowsStream security (from Open)
        public static async Task<(NetworkCredential, bool)> GetSspiCredentialAsync(SecurityTokenManager credentialProvider,
            SecurityTokenRequirement sspiTokenRequirement, CancellationToken token)
        {
            bool extractGroupsForWindowsAccounts = TransportDefaults.ExtractGroupsForWindowsAccounts;
            NetworkCredential result = null;

            if (credentialProvider != null)
            {
                SecurityTokenProvider tokenProvider = credentialProvider.CreateSecurityTokenProvider(sspiTokenRequirement);
                if (tokenProvider != null)
                {
                    await SecurityUtils.OpenTokenProviderIfRequiredAsync(tokenProvider, token);
                    bool success = false;
                    try
                    {
                        TokenImpersonationLevel dummyImpersonationLevel;
                        bool dummyAllowNtlm;
                        (result, extractGroupsForWindowsAccounts, dummyImpersonationLevel, dummyAllowNtlm) = await GetSspiCredentialAsync((SspiSecurityTokenProvider)tokenProvider, token);

                        success = true;
                    }
                    finally
                    {
                        if (!success)
                        {
                            SecurityUtils.AbortTokenProviderIfRequired(tokenProvider);
                        }
                    }
                    await SecurityUtils.CloseTokenProviderIfRequiredAsync(tokenProvider, token);
                }
            }

            return (result, extractGroupsForWindowsAccounts);
        }

        // core Cred lookup code
        static async Task<(NetworkCredential, bool, TokenImpersonationLevel, bool)> GetSspiCredentialAsync(SspiSecurityTokenProvider tokenProvider, CancellationToken cancellationToken)
        {
            NetworkCredential credential = null;
            bool extractGroupsForWindowsAccounts = TransportDefaults.ExtractGroupsForWindowsAccounts;
            TokenImpersonationLevel impersonationLevel = TokenImpersonationLevel.Identification;
            bool allowNtlm = ConnectionOrientedTransportDefaults.AllowNtlm;

            if (tokenProvider != null)
            {
                SspiSecurityToken token = await TransportSecurityHelpers.GetTokenAsync<SspiSecurityToken>(tokenProvider, cancellationToken);
                if (token != null)
                {
                    extractGroupsForWindowsAccounts = token.ExtractGroupsForWindowsAccounts;
                    impersonationLevel = token.ImpersonationLevel;
                    allowNtlm = token.AllowNtlm;
                    if (token.NetworkCredential != null)
                    {
                        credential = token.NetworkCredential;
                        SecurityUtils.FixNetworkCredential(ref credential);
                    }
                }
            }

            // Initialize to the default value if no token provided. A partial trust app should not have access to the
            // default network credentials but should be able to provide credentials. The DefaultNetworkCredentials
            // getter will throw under partial trust.
            if (credential == null)
            {
                credential = CredentialCache.DefaultNetworkCredentials;
            }

            return (credential, extractGroupsForWindowsAccounts, impersonationLevel, allowNtlm);
        }

        public static SecurityTokenRequirement CreateSspiTokenRequirement(string transportScheme, Uri listenUri)
        {
            RecipientServiceModelSecurityTokenRequirement tokenRequirement = new RecipientServiceModelSecurityTokenRequirement();
            tokenRequirement.TransportScheme = transportScheme;
            tokenRequirement.RequireCryptographicToken = false;
            tokenRequirement.ListenUri = listenUri;
            tokenRequirement.TokenType = ServiceModelSecurityTokenTypes.SspiCredential;
            return tokenRequirement;
        }

        public static SecurityTokenAuthenticator GetCertificateTokenAuthenticator(SecurityTokenManager tokenManager, string transportScheme, Uri listenUri)
        {
            RecipientServiceModelSecurityTokenRequirement clientAuthRequirement = new RecipientServiceModelSecurityTokenRequirement();
            clientAuthRequirement.TokenType = SecurityTokenTypes.X509Certificate;
            clientAuthRequirement.RequireCryptographicToken = true;
            clientAuthRequirement.KeyUsage = SecurityKeyUsage.Signature;
            clientAuthRequirement.TransportScheme = transportScheme;
            clientAuthRequirement.ListenUri = listenUri;
            SecurityTokenResolver dummy;
            return tokenManager.CreateSecurityTokenAuthenticator(clientAuthRequirement, out dummy);
        }

        public static Uri GetListenUri(Uri baseAddress, string relativeAddress)
        {
            Uri fullUri = baseAddress;

            // Ensure that baseAddress Path does end with a slash if we have a relative address
            if (!string.IsNullOrEmpty(relativeAddress))
            {
                if (!baseAddress.AbsolutePath.EndsWith("/", StringComparison.Ordinal))
                {
                    UriBuilder uriBuilder = new UriBuilder(baseAddress);
                    FixIpv6Hostname(uriBuilder, baseAddress);
                    uriBuilder.Path = uriBuilder.Path + "/";
                    baseAddress = uriBuilder.Uri;
                }

                fullUri = new Uri(baseAddress, relativeAddress);
            }

            return fullUri;
        }

        // Moved from TcpChannelListener
        internal static void FixIpv6Hostname(UriBuilder uriBuilder, Uri originalUri)
        {
            if (originalUri.HostNameType == UriHostNameType.IPv6)
            {
                string ipv6Host = originalUri.DnsSafeHost;
                uriBuilder.Host = string.Concat("[", ipv6Host, "]");
            }
        }
    }
}
