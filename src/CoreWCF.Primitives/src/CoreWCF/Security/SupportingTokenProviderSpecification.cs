using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Security.Tokens;


namespace CoreWCF.Security
{
    public class SupportingTokenProviderSpecification
    {
        SecurityTokenParameters tokenParameters;

        public SupportingTokenProviderSpecification(SecurityTokenProvider tokenProvider, SecurityTokenAttachmentMode attachmentMode, SecurityTokenParameters tokenParameters)
        {
            if (tokenProvider == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("tokenProvider");
            }
            SecurityTokenAttachmentModeHelper.Validate(attachmentMode);
            if (tokenParameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("tokenParameters");
            }
            this.TokenProvider = tokenProvider;
            this.SecurityTokenAttachmentMode = attachmentMode;
            this.tokenParameters = tokenParameters;
        }

        public SecurityTokenProvider TokenProvider { get; }

        public SecurityTokenAttachmentMode SecurityTokenAttachmentMode { get; }

        public SecurityTokenParameters TokenParameters
        {
            get { return this.tokenParameters; }
        }
    }
}
