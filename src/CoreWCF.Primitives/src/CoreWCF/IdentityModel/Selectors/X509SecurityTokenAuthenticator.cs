using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;

namespace CoreWCF.IdentityModel.Selectors
{
    internal class X509SecurityTokenAuthenticator : SecurityTokenAuthenticator
    {
        X509CertificateValidator validator;
        bool mapToWindows;
        bool includeWindowsGroups;
        bool cloneHandle;

        public X509SecurityTokenAuthenticator()
            : this(X509CertificateValidator.ChainTrust)
        {
        }

        public X509SecurityTokenAuthenticator(X509CertificateValidator validator)
            : this(validator, false)
        {
        }

        public X509SecurityTokenAuthenticator(X509CertificateValidator validator, bool mapToWindows)
            : this(validator, mapToWindows, WindowsClaimSet.DefaultIncludeWindowsGroups)
        {
        }

        public X509SecurityTokenAuthenticator(X509CertificateValidator validator, bool mapToWindows, bool includeWindowsGroups)
            : this(validator, mapToWindows, includeWindowsGroups, true)
        {
        }

        internal X509SecurityTokenAuthenticator(X509CertificateValidator validator, bool mapToWindows, bool includeWindowsGroups, bool cloneHandle)
        {
            if (validator == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(validator));
            }

            this.validator = validator;
            this.mapToWindows = mapToWindows;
            this.includeWindowsGroups = includeWindowsGroups;
            this.cloneHandle = cloneHandle;
        }

        public bool MapCertificateToWindowsAccount
        {
            get { return mapToWindows; }
        }

        protected override bool CanValidateTokenCore(SecurityToken token)
        {
            return token is X509SecurityToken;
        }

        protected override ReadOnlyCollection<IAuthorizationPolicy> ValidateTokenCore(SecurityToken token)
        {
            X509SecurityToken x509Token = (X509SecurityToken)token;
            validator.Validate(x509Token.Certificate);

            X509CertificateClaimSet x509ClaimSet = new X509CertificateClaimSet(x509Token.Certificate, cloneHandle);
            if (!mapToWindows)
                return SecurityUtils.CreateAuthorizationPolicies(x509ClaimSet, x509Token.ValidTo);

            WindowsClaimSet windowsClaimSet;
            if (token is X509WindowsSecurityToken)
            {
                windowsClaimSet = new WindowsClaimSet(((X509WindowsSecurityToken)token).WindowsIdentity, SecurityUtils.AuthTypeCertMap, includeWindowsGroups, cloneHandle);
            }
            else
            {
                throw new PlatformNotSupportedException();
                // Ensure NT_AUTH chain policy for certificate account mapping
                //X509CertificateValidator.NTAuthChainTrust.Validate(x509Token.Certificate);

                //WindowsIdentity windowsIdentity = null;
                //windowsIdentity = KerberosCertificateLogon(x509Token.Certificate);


                //windowsClaimSet = new WindowsClaimSet(windowsIdentity, SecurityUtils.AuthTypeCertMap, this.includeWindowsGroups, false);
            }
            List<ClaimSet> claimSets = new List<ClaimSet>(2);
            claimSets.Add(windowsClaimSet);
            claimSets.Add(x509ClaimSet);

            List<IAuthorizationPolicy> policies = new List<IAuthorizationPolicy>(1);
            policies.Add(new UnconditionalPolicy(claimSets.AsReadOnly(), x509Token.ValidTo));
            return policies.AsReadOnly();
        }
    }
}
