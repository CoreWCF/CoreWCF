// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Principal;
using System.Threading.Tasks;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security;
using MSSaml = Microsoft.IdentityModel.Tokens.Saml;

namespace CoreWCF.IdentityModel.Selectors
{
    public class SamlSecurityTokenAuthenticator : SecurityTokenAuthenticator
    {
        private readonly List<SecurityTokenAuthenticator> _supportingAuthenticators;
        private AudienceUriMode _audienceUriMode;
        private TimeSpan _maxClockSkew;

        public SamlSecurityTokenAuthenticator(IList<SecurityTokenAuthenticator> supportingAuthenticators)
            : this(supportingAuthenticators, TimeSpan.Zero)
        { }

        public SamlSecurityTokenAuthenticator(IList<SecurityTokenAuthenticator> supportingAuthenticators, TimeSpan maxClockSkew)
        {
            _supportingAuthenticators = new List<SecurityTokenAuthenticator>(supportingAuthenticators ??
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(supportingAuthenticators)));
            _maxClockSkew = maxClockSkew;
            _audienceUriMode = AudienceUriMode.Always;
        }

        public AudienceUriMode AudienceUriMode
        {
            get { return _audienceUriMode; }
            set
            {
                AudienceUriModeValidationHelper.Validate(_audienceUriMode);
                _audienceUriMode = value;
            }
        }

        public IList<string> AllowedAudienceUris { get; } = new Collection<string>();

        protected override bool CanValidateTokenCore(SecurityToken token)
        {
            return token is SamlSecurityToken;
        }

        protected override async ValueTask<ReadOnlyCollection<IAuthorizationPolicy>> ValidateTokenCoreAsync(SecurityToken token)
        {
            if (token == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));


            if (!(token is SamlSecurityToken samlToken))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.SamlTokenAuthenticatorCanOnlyProcessSamlTokens, token.GetType().ToString())));

            if (samlToken.Assertion.Signature == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.SamlTokenMissingSignature)));

            if (!IsCurrentlyTimeEffective(samlToken))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.SAMLTokenTimeInvalid, DateTime.UtcNow.ToUniversalTime(), samlToken.ValidFrom.ToString(), samlToken.ValidTo.ToString())));

            if (samlToken.SigningToken == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.SamlSigningTokenMissing)));

            // Build the Issuer ClaimSet for this Saml token.
            ClaimSet issuer = null;
            bool canBeValidated = false;
            for (int i = 0; i < _supportingAuthenticators.Count; ++i)
            {
                canBeValidated = _supportingAuthenticators[i].CanValidateToken(samlToken.SigningToken);
                if (canBeValidated)
                    break;
            }
            if (!canBeValidated)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.SamlInvalidSigningToken)));

            issuer = (await ResolveClaimSetAsyc(samlToken.SigningToken)) ?? ClaimSet.Anonymous;

            List<IAuthorizationPolicy> policies = new List<IAuthorizationPolicy>();
            foreach (InternalSamlSubjectStatement subject in samlToken.SamlStatements)
            {
                policies.Add(await subject.CreatePolicyAsync(issuer, this));
            }

            if ((_audienceUriMode == AudienceUriMode.Always)
             || (_audienceUriMode == AudienceUriMode.BearerKeyOnly) && (samlToken.SecurityKeys.Count < 1))
            {
                // throws if not found.
                bool foundAudienceCondition = false;
                if (AllowedAudienceUris == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.SAMLAudienceUrisNotFound)));
                }

                foreach (MSSaml.SamlCondition samlCondition in samlToken.Assertion.Conditions.Conditions)
                {
                    if (samlCondition is MSSaml.SamlAudienceRestrictionCondition condition)
                    {
                        foundAudienceCondition = true;
                        if (!ValidateAudienceRestriction(condition))
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.SAMLAudienceUriValidationFailed)));
                        }
                    }
                }

                if (!foundAudienceCondition)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.SAMLAudienceUriValidationFailed)));
            }

            return policies.AsReadOnly();
        }

        protected virtual bool ValidateAudienceRestriction(MSSaml.SamlAudienceRestrictionCondition audienceRestrictionCondition)
        {
            foreach (Uri audienceUri in audienceRestrictionCondition.Audiences)
            {
                if (audienceUri == null)
                    continue;

                for (int j = 0; j < AllowedAudienceUris.Count; j++)
                {
                    if (StringComparer.Ordinal.Compare(audienceUri.AbsoluteUri, AllowedAudienceUris[j]) == 0)
                        return true;
                    else if (Uri.IsWellFormedUriString(AllowedAudienceUris[j], UriKind.Absolute))
                    {
                        Uri uri = new Uri(AllowedAudienceUris[j]);
                        if (audienceUri.Equals(uri))
                            return true;
                    }
                }
            }

            return false;
        }

        public virtual async ValueTask<ClaimSet> ResolveClaimSetAsyc(SecurityToken token)
        {
            if (token == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));

            foreach (var authenticator in _supportingAuthenticators)
            {
                if (authenticator.CanValidateToken(token))
                {
                    var authorizationPolicies = await authenticator.ValidateTokenAsync(token); ;
                    AuthorizationContext authContext = AuthorizationContext.CreateDefaultAuthorizationContext(authorizationPolicies);
                    if (authContext.ClaimSets.Count > 0)
                    {
                        return authContext.ClaimSets[0];
                    }
                }
            }

            return null;
        }

        public virtual ClaimSet ResolveClaimSet(SecurityKeyIdentifier keyIdentifier)
        {
            if (keyIdentifier == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifier));

            if (keyIdentifier.TryFind(out RsaKeyIdentifierClause rsaKeyIdentifierClause))
            {
                return new DefaultClaimSet(new Claim(ClaimTypes.Rsa, rsaKeyIdentifierClause.Rsa, Rights.PossessProperty));
            }
            else if (keyIdentifier.TryFind(out EncryptedKeyIdentifierClause encryptedKeyIdentifierClause))
            {
                return new DefaultClaimSet(Claim.CreateHashClaim(encryptedKeyIdentifierClause.GetBuffer()));
            }

            return null;
        }

        public virtual async ValueTask<IIdentity> ResolveIdentityAsync(SecurityToken token)
        {
            if (token == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));

            for (int i = 0; i < _supportingAuthenticators.Count; ++i)
            {
                if (_supportingAuthenticators[i].CanValidateToken(token))
                {
                    ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies = await _supportingAuthenticators[i].ValidateTokenAsync(token);
                    if (authorizationPolicies != null && authorizationPolicies.Count != 0)
                    {
                        for (int j = 0; j < authorizationPolicies.Count; ++j)
                        {
                            IAuthorizationPolicy policy = authorizationPolicies[j];
                            if (policy is UnconditionalPolicy policy1)
                            {
                                return policy1.PrimaryIdentity;
                            }
                        }
                    }
                }
            }

            return null;
        }

        public virtual IIdentity ResolveIdentity(SecurityKeyIdentifier keyIdentifier)
        {
            if (keyIdentifier == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifier));

            if (keyIdentifier.TryFind(out RsaKeyIdentifierClause rsaKeyIdentifierClause))
            {
                return SecurityUtils.CreateIdentity(rsaKeyIdentifierClause.Rsa.ToXmlString(false), GetType().Name);
            }

            return null;
        }

        private bool IsCurrentlyTimeEffective(SamlSecurityToken token)
        {
            if (token.Assertion.Conditions != null)
            {
                return SecurityUtils.IsCurrentlyTimeEffective(token.Assertion.Conditions.NotBefore, token.Assertion.Conditions.NotOnOrAfter, _maxClockSkew);
            }

            // If SAML Condition is not present then the assertion is valid at any given time.
            return true;
        }

    }

}
