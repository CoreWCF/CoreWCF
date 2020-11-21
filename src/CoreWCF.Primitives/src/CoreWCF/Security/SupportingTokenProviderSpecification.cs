using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    public class SupportingTokenProviderSpecification
    {
        public SupportingTokenProviderSpecification(SecurityTokenProvider tokenProvider, SecurityTokenAttachmentMode attachmentMode, SecurityTokenParameters tokenParameters)
        {
            if (tokenProvider == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenProvider));
            }
            SecurityTokenAttachmentModeHelper.Validate(attachmentMode);
            if (tokenParameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenParameters));
            }
            this.TokenProvider = tokenProvider;
            this.SecurityTokenAttachmentMode = attachmentMode;
            this.TokenParameters = tokenParameters;
        }

        public SecurityTokenProvider TokenProvider { get; }

        public SecurityTokenAttachmentMode SecurityTokenAttachmentMode { get; }

        public SecurityTokenParameters TokenParameters { get; }
    }
}
