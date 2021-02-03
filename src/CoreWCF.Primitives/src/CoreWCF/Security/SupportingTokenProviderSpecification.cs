// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    public class SupportingTokenProviderSpecification
    {
        public SupportingTokenProviderSpecification(SecurityTokenProvider tokenProvider, SecurityTokenAttachmentMode attachmentMode, SecurityTokenParameters tokenParameters)
        {
            SecurityTokenAttachmentModeHelper.Validate(attachmentMode);
            TokenProvider = tokenProvider ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenProvider));
            SecurityTokenAttachmentMode = attachmentMode;
            TokenParameters = tokenParameters ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenParameters));
        }

        public SecurityTokenProvider TokenProvider { get; }

        public SecurityTokenAttachmentMode SecurityTokenAttachmentMode { get; }

        public SecurityTokenParameters TokenParameters { get; }
    }
}
