// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Principal;
using System.Threading.Tasks;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Security;
using MSIdentitySAML = Microsoft.IdentityModel.Tokens.Saml;

namespace CoreWCF.IdentityModel.Tokens
{
    internal class InternalSamlSubjectStatement
    {
        private readonly MSIdentitySAML.SamlSubjectStatement _samlSubjectStatement;
        private readonly MSIdentitySAML.SamlSubject _samlSubject;
        readonly SecurityKeyIdentifier _securityKeyIdentifier;
        private readonly SecurityToken _subjectToken;
        private IIdentity _identity;
        private ClaimSet _subjectKeyClaimset;
        private IAuthorizationPolicy _policy;

        public InternalSamlSubjectStatement(MSIdentitySAML.SamlSubjectStatement samlSubjectStatement,
            SecurityKeyIdentifier securityKeyIdentifier, SecurityToken securityToken)
        {
            _samlSubjectStatement = samlSubjectStatement;
            _samlSubject = _samlSubjectStatement.Subject;
            _securityKeyIdentifier = securityKeyIdentifier;
            _subjectToken = securityToken;
        }

        //https://github.com/microsoft/referencesource/blob/4e6dea7a9c7cbb4e6b000b05a099e7168d1b6960/System.IdentityModel/System/IdentityModel/Tokens/SamlSubjectStatement.cs#L61
        internal async ValueTask<IAuthorizationPolicy> CreatePolicyAsync(ClaimSet issuer, SamlSecurityTokenAuthenticator samlAuthenticator)
        {
            if (issuer == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(issuer));

            if (_policy == null)
            {
                List<ClaimSet> claimSets = new List<ClaimSet>();
                ClaimSet subjectKeyClaimset = await ExtractSubjectKeyClaimSetAsync(samlAuthenticator);
                if (subjectKeyClaimset != null)
                    claimSets.Add(subjectKeyClaimset);

                List<Claim> claims = new List<Claim>();
                ReadOnlyCollection<Claim> subjectClaims = ExtractClaims();
                for (int i = 0; i < subjectClaims.Count; ++i)
                {
                    claims.Add(subjectClaims[i]);
                }

                AddClaimsToList(claims);
                claimSets.Add(new DefaultClaimSet(issuer, claims));
                _policy = new UnconditionalPolicy(_identity, claimSets.AsReadOnly(), SecurityUtils.MaxUtcDateTime);
            }

            return _policy;
        }

        private void AddClaimsToList(List<Claim> claims)
        {
            if (_samlSubjectStatement is MSIdentitySAML.SamlAuthorizationDecisionStatement samlAuthorizationStatement)
            {
                //https://github.com/microsoft/referencesource/blob/4e6dea7a9c7cbb4e6b000b05a099e7168d1b6960/System.IdentityModel/System/IdentityModel/Tokens/SamlAuthorizationDecisionStatement.cs#L132
                foreach (MSIdentitySAML.SamlAction action in samlAuthorizationStatement.Actions)
                {
                    Enum.TryParse(samlAuthorizationStatement.Decision, out SamlAccessDecision decision);
                    claims.Add(new Claim(ClaimTypes.AuthorizationDecision, new SamlAuthorizationDecisionClaimResource(samlAuthorizationStatement.Resource, decision, action.Namespace.AbsoluteUri, action.Value), Rights.PossessProperty));
                }
            }
            else if (_samlSubjectStatement is MSIdentitySAML.SamlAuthenticationStatement)
            {
                //https://github.com/microsoft/referencesource/blob/4e6dea7a9c7cbb4e6b000b05a099e7168d1b6960/System.IdentityModel/System/IdentityModel/Tokens/SamlAuthenticationStatement.cs#L144
                MSIdentitySAML.SamlAuthenticationStatement samlAuthStatement = _samlSubjectStatement as MSIdentitySAML.SamlAuthenticationStatement;
                claims.Add(new Claim(ClaimTypes.Authentication, new SamlAuthenticationClaimResource(samlAuthStatement.AuthenticationInstant, samlAuthStatement.AuthenticationMethod, samlAuthStatement.DnsAddress, samlAuthStatement.IPAddress, samlAuthStatement.AuthorityBindings), Rights.PossessProperty));
            }
            else if (_samlSubjectStatement is MSIdentitySAML.SamlAttributeStatement attributeStatement)
            {
                foreach (MSIdentitySAML.SamlAttribute attribute in attributeStatement.Attributes)
                {
                    foreach (string attrVal in attribute.Values)
                    {
                        claims.Add(new Claim(attribute.ClaimType, attrVal, Rights.PossessProperty));
                    }
                }
            }
        }

        //https://github.com/microsoft/referencesource/blob/4e6dea7a9c7cbb4e6b000b05a099e7168d1b6960/System.IdentityModel/System/IdentityModel/Tokens/SamlSubject.cs#L219
        private async ValueTask<ClaimSet> ExtractSubjectKeyClaimSetAsync(SamlSecurityTokenAuthenticator samlAuthenticator)
        {
            if (_samlSubject.KeyInfo != null)
            {
                if (samlAuthenticator == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(samlAuthenticator));

                if (_subjectToken != null)
                {
                    _subjectKeyClaimset = samlAuthenticator.ResolveClaimSet(_securityKeyIdentifier);
                    _identity = await samlAuthenticator.ResolveIdentityAsync(_subjectToken);
                    if ((_identity == null) && (_subjectKeyClaimset != null))
                    {
                        Claim identityClaim = null;
                        foreach (Claim claim in _subjectKeyClaimset.FindClaims(null, Rights.Identity))
                        {
                            identityClaim = claim;
                            break;
                        }

                        if (identityClaim != null)
                        {
                            _identity = SecurityUtils.CreateIdentity(identityClaim.Resource.ToString(), GetType().Name);
                        }
                    }
                }

                if (_subjectKeyClaimset == null)
                {
                    // Add the type of the primary claim as the Identity claim.
                    _subjectKeyClaimset = samlAuthenticator.ResolveClaimSet(_securityKeyIdentifier);
                    _identity = samlAuthenticator.ResolveIdentity(_securityKeyIdentifier);
                }
            }

            return _subjectKeyClaimset;
        }

        //https://github.com/microsoft/referencesource/blob/4e6dea7a9c7cbb4e6b000b05a099e7168d1b6960/System.IdentityModel/System/IdentityModel/Tokens/SamlAttribute.cs#L185
        private ReadOnlyCollection<Claim> ExtractClaims()
        {
            var claims = new List<Claim>();
            if (!string.IsNullOrEmpty(_samlSubject.Name))
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, new SamlNameIdentifierClaimResource(_samlSubject.Name, _samlSubject.NameQualifier, _samlSubject.NameFormat), Rights.Identity));
                claims.Add(new Claim(ClaimTypes.NameIdentifier, new SamlNameIdentifierClaimResource(_samlSubject.Name, _samlSubject.NameQualifier, _samlSubject.NameFormat), Rights.PossessProperty));
            }

            return claims.AsReadOnly();
        }
    }
}
