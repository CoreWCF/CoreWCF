// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using CoreWCF.IdentityModel.Selectors;

namespace CoreWCF.IdentityModel.Tokens
{
    public class SamlSecurityTokenRequirement
    {
        private X509CertificateValidator _certificateValidator;

        /// <summary>
        /// Creates an instance of <see cref="SamlSecurityTokenRequirement"/>
        /// </summary>
        public SamlSecurityTokenRequirement()
        {
        }

        /// <summary>
        /// Gets/sets the X509CertificateValidator associated with this token requirement
        /// </summary>
        public X509CertificateValidator CertificateValidator
        {
            get
            {
                return _certificateValidator;
            }
            set
            {
                _certificateValidator = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        /// <summary>
        /// Gets or sets the Claim Type that will be used to generate the 
        /// FederatedIdentity.Name property.
        /// </summary>
        public string NameClaimType { get; set; } = ClaimsIdentity.DefaultNameClaimType;

        /// <summary>
        /// Gets the Claim Types that are used to generate the
        /// FederatedIdentity.Roles property.
        /// </summary>
        public string RoleClaimType { get; set; } = ClaimTypes.Role;

        /// <summary>
        /// Checks if Audience Enforcement checks are required for the given token 
        /// based on this SamlSecurityTokenRequirement settings.
        /// </summary>
        /// <param name="audienceUriMode">
        /// The <see cref="AudienceUriMode"/> defining the audience requirement.
        /// </param>
        /// <param name="token">The Security token to be tested for Audience 
        /// Enforcement.</param>
        /// <returns>True if Audience Enforcement should be applied.</returns>
        /// <exception cref="ArgumentNullException">The input argument 'token' is null.</exception>
        public virtual bool ShouldEnforceAudienceRestriction(AudienceUriMode audienceUriMode, SecurityToken token)
        {
            if (null == token)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }

            switch (audienceUriMode)
            {
                case AudienceUriMode.Always:
                    return true;

                case AudienceUriMode.Never:
                    return false;

                case AudienceUriMode.BearerKeyOnly:
                    return (null == token.SecurityKeys || 0 == token.SecurityKeys.Count);

                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ID4025, audienceUriMode)));
            }
        }

        /// <summary>
        /// Checks the given list of Audience URIs with the AllowedAudienceUri list.
        /// </summary>
        /// <param name="allowedAudienceUris">Collection of AudienceUris.</param>
        /// <param name="tokenAudiences">Collection of audience URIs the token applies to.</param>
        /// <exception cref="ArgumentNullException">The input argument 'allowedAudienceUris' is null.</exception>
        /// <exception cref="ArgumentNullException">The input argument 'tokenAudiences' is null.</exception>
        /// <exception cref="AudienceUriValidationFailedException">Either the input argument 'tokenAudiences' or the configured
        /// 'AudienceUris' collection is empty.</exception>
        public virtual void ValidateAudienceRestriction(IList<Uri> allowedAudienceUris, IList<Uri> tokenAudiences)
        {
            if (null == allowedAudienceUris)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(allowedAudienceUris));
            }

            if (null == tokenAudiences)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenAudiences));
            }

            if (0 == tokenAudiences.Count)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new AudienceUriValidationFailedException(
                    SR.Format(SR.ID1036)));
            }

            if (0 == allowedAudienceUris.Count)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new AudienceUriValidationFailedException(
                    SR.Format(SR.ID1043)));
            }

            bool found = false;
            foreach (Uri audience in tokenAudiences)
            {
                if (audience != null)
                {
                    // Strip off any query string or fragment. This is necessary because the 
                    // CardSpace uses the raw Request-URI to form the audience when issuing 
                    // tokens for personal cards, but we clearly don't want things like the 
                    // ReturnUrl parameter affecting the audience matching.
                    Uri audienceLeftPart;
                    if (audience.IsAbsoluteUri)
                    {
                        audienceLeftPart = new Uri(audience.GetLeftPart(UriPartial.Path));
                    }
                    else
                    {
                        Uri baseUri = new Uri("http://www.example.com");
                        Uri resolved = new Uri(baseUri, audience);
                        audienceLeftPart = baseUri.MakeRelativeUri(new Uri(resolved.GetLeftPart(UriPartial.Path)));
                    }

                    if (allowedAudienceUris.Contains(audienceLeftPart))
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                if (1 == tokenAudiences.Count || null != tokenAudiences[0])
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new AudienceUriValidationFailedException(
                        SR.Format(SR.ID1038, tokenAudiences[0].OriginalString)));
                }
                else
                {
                    StringBuilder sb = new StringBuilder(SR.Format(SR.ID8007));
                    bool first = true;

                    foreach (Uri a in tokenAudiences)
                    {
                        if (a != null)
                        {
                            if (first)
                            {
                                first = false;
                            }
                            else
                            {
                                sb.Append(", ");
                            }

                            sb.Append(a.OriginalString);
                        }
                    }
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new AudienceUriValidationFailedException(SR.Format(SR.ID1037)));
                }
            }
        }
    }
}

