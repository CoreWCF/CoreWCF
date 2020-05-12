using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Security.Principal;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Primitives.CoreWCF.IdentityModel.Tokens
{
    public class GenericSecurityToken : SecurityToken
    {
        private string _id;
        private DateTime _effectiveTime;
        private DateTime _expirationTime;

        internal GenericSecurityToken(string servicePrincipalName, TokenImpersonationLevel tokenImpersonationLevel, NetworkCredential networkCredential, string id)
        {
            if (tokenImpersonationLevel != TokenImpersonationLevel.Identification && tokenImpersonationLevel != TokenImpersonationLevel.Impersonation)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(tokenImpersonationLevel),
                    SR.Format(SR.ImpersonationLevelNotSupported, tokenImpersonationLevel)));
            }

            ServicePrincipalName = servicePrincipalName ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(servicePrincipalName));
            if (networkCredential != null && networkCredential != CredentialCache.DefaultNetworkCredentials)
            {
                if (string.IsNullOrEmpty(networkCredential.UserName))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.ProvidedNetworkCredentialsHasInvalidUserName);
                }
            }
            _id = id ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(id));
            _effectiveTime = DateTime.UtcNow;
            _expirationTime = DateTime.UtcNow.AddHours(10);
        }

        public override string Id
        {
            get { return _id; }
        }

        public override ReadOnlyCollection<SecurityKey> SecurityKeys
        {
            get { return EmptyReadOnlyCollection<SecurityKey>.Instance; }
        }

        public override DateTime ValidFrom
        {
            get { return _effectiveTime; }
        }

        public override DateTime ValidTo
        {
            get { return _expirationTime; }
        }

        public string ServicePrincipalName { get; }
    }
}


