// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    public class SupportingTokenAuthenticatorSpecification
    {
        public SupportingTokenAuthenticatorSpecification(SecurityTokenAuthenticator tokenAuthenticator, SecurityTokenResolver securityTokenResolver, SecurityTokenAttachmentMode attachmentMode, SecurityTokenParameters tokenParameters)
            : this(tokenAuthenticator, securityTokenResolver, attachmentMode, tokenParameters, false)
        {
        }

        internal SupportingTokenAuthenticatorSpecification(SecurityTokenAuthenticator tokenAuthenticator, SecurityTokenResolver securityTokenResolver, SecurityTokenAttachmentMode attachmentMode, SecurityTokenParameters tokenParameters, bool isTokenOptional)
        {
            if (tokenAuthenticator == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("tokenAuthenticator");
            }

            SecurityTokenAttachmentModeHelper.Validate(attachmentMode);

            if (tokenParameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("tokenParameters");
            }
            TokenAuthenticator = tokenAuthenticator;
            TokenResolver = securityTokenResolver;
            SecurityTokenAttachmentMode = attachmentMode;
            TokenParameters = tokenParameters;
            IsTokenOptional = isTokenOptional;
        }

        public SecurityTokenAuthenticator TokenAuthenticator { get; }

        public SecurityTokenResolver TokenResolver { get; }

        public SecurityTokenAttachmentMode SecurityTokenAttachmentMode { get; }

        public SecurityTokenParameters TokenParameters { get; }

        internal bool IsTokenOptional { get; set; }
    }
}
