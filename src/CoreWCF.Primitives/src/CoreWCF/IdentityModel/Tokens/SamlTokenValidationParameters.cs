// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using CoreWCF.IdentityModel.Selectors;
using Microsoft.IdentityModel.Tokens;
using MsIdentityTokens = Microsoft.IdentityModel.Tokens;

namespace CoreWCF.IdentityModel.Tokens
{
    internal class SamlTokenValidationParameters
    {
        private const string PROPERTY_BAG_ORIGIN = "CoreWCF.IdentityModel.Tokens.SamlTokenValidationParameters.";
        private const string SAML_SECURITY_TOKEN = PROPERTY_BAG_ORIGIN + "SamlSecurityToken";
        private const string SAML_SECURITY_TOKEN_REQ = PROPERTY_BAG_ORIGIN + "SamlSecurityTokenRequirement";
        private const string SECURITY_TOKEN_HANDLER_CONFIG = PROPERTY_BAG_ORIGIN + "SecurityTokenHandlerConfiguration";

        public SamlTokenValidationParameters()
        {
        }

        internal TokenValidationParameters ConvertToTokenValidationParameters
            (SecurityTokenHandlerConfiguration securityHandlerConfiguration, SecurityToken securityToken, SamlSecurityTokenRequirement securityTokenRequirement)
        {
            TokenValidationParameters tokenValidationParams = new TokenValidationParameters
            {
                PropertyBag = new Dictionary<string, object>()
            };
            tokenValidationParams.PropertyBag.Add(SECURITY_TOKEN_HANDLER_CONFIG, securityHandlerConfiguration);

            tokenValidationParams.ValidateAudience = securityTokenRequirement.ShouldEnforceAudienceRestriction
                (securityHandlerConfiguration.AudienceRestriction.AudienceMode, securityToken);

            if (tokenValidationParams.ValidateAudience)
            {
                if (securityHandlerConfiguration == null || securityHandlerConfiguration.AudienceRestriction.AllowedAudienceUris.Count == 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ID1032)));
                }

                tokenValidationParams.PropertyBag.Add(SAML_SECURITY_TOKEN_REQ, securityTokenRequirement);
                tokenValidationParams.AudienceValidator = AudienceValidator;
            }

            //start of tokenreplay
            if (securityHandlerConfiguration.DetectReplayedTokens)
            {
                tokenValidationParams.ValidateTokenReplay = true;
                tokenValidationParams.TokenReplayCache = securityHandlerConfiguration.Caches.TokenReplayCache;
            }

            //start of certificate validator
            tokenValidationParams.IssuerSigningKeyValidator = IssuerSigningKeyValidator;
            //start of Issuer validation
            tokenValidationParams.IssuerValidator = IssuerValidate;
            tokenValidationParams.PropertyBag.Add(SAML_SECURITY_TOKEN, securityToken);
            tokenValidationParams.SignatureValidator = SignValidator;
            tokenValidationParams.ClockSkew = securityHandlerConfiguration.MaxClockSkew;
            return tokenValidationParams;
        }

        private bool IssuerSigningKeyValidator(MsIdentityTokens.SecurityKey securityKey, MsIdentityTokens.SecurityToken securityToken, TokenValidationParameters validationParameters)
        {
            X509CertificateValidator validator = GetSecurityTokenHandler(validationParameters).CertificateValidator;
            ICollection<Microsoft.IdentityModel.Xml.X509Data> x509Datas = null;
            if (securityToken is MsIdentityTokens.Saml.SamlSecurityToken token)
            {
                x509Datas = token.Assertion.Signature.KeyInfo.X509Data;
            }
            else
            {
                x509Datas = ((MsIdentityTokens.Saml2.Saml2SecurityToken)securityToken).Assertion.Signature.KeyInfo.X509Data;
            }

            if(x509Datas == null || x509Datas.Count > 1)
            {
                return false;
            }

            byte[] data = Convert.FromBase64String(x509Datas.FirstOrDefault().Certificates.FirstOrDefault());
            X509Certificate2 cert = new X509Certificate2(data);
            validator.Validate(cert);
            return true;
        }

        private MsIdentityTokens.SecurityToken SignValidator(string token, TokenValidationParameters validationParameters)
        {
            SecurityToken samlToken = (SecurityToken)validationParameters.PropertyBag[SAML_SECURITY_TOKEN];
            if(samlToken is SamlSecurityToken)
            {
                return ((SamlSecurityToken)samlToken).WrappedSamlSecurityToken;
            }
            else
            {
                return ((Saml2SecurityToken)samlToken).WrappedSaml2SecurityToken;
            }
        }

        private string IssuerValidate(string issuer, MsIdentityTokens.SecurityToken securityToken, TokenValidationParameters validationParameters)
        {
            IssuerNameRegistry issuerNameRegistry = GetSecurityTokenHandler(validationParameters).IssuerNameRegistry;
            SecurityToken samlToken = (SecurityToken)validationParameters.PropertyBag[SAML_SECURITY_TOKEN];
            SecurityToken signingToken = null;

            if (samlToken is SamlSecurityToken)
            {
                signingToken = ((SamlSecurityToken)samlToken).SigningToken;
            }
            else
            {
                signingToken = ((Saml2SecurityToken)samlToken).SigningToken;
            }

            return issuerNameRegistry.GetIssuerName(signingToken, issuer);
        }

        private bool AudienceValidator(IEnumerable<string> audienceUris, MsIdentityTokens.SecurityToken securityToken, TokenValidationParameters validationParameters)
        {
            SamlSecurityTokenRequirement samlSecurityTokenRequirement = (SamlSecurityTokenRequirement)validationParameters.PropertyBag[SAML_SECURITY_TOKEN_REQ];
            List<Uri> uris = new List<Uri>();
            foreach(string audienceUri in audienceUris)
            {
                uris.Add(new Uri(audienceUri));
            }
            //if there is error, throw.
            samlSecurityTokenRequirement.ValidateAudienceRestriction(uris, GetSecurityTokenHandler(validationParameters).AudienceRestriction.AllowedAudienceUris);
            return true;
        }

        private SecurityTokenHandlerConfiguration GetSecurityTokenHandler(TokenValidationParameters validationParameters) => (SecurityTokenHandlerConfiguration)validationParameters.PropertyBag[SECURITY_TOKEN_HANDLER_CONFIG];

    }
}
