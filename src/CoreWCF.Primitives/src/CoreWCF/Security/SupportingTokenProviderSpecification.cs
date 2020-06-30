using CoreWCF.IdentityModel.Selectors;
using CoreWCF;
using CoreWCF.Security.Tokens;


namespace CoreWCF.Security
{

   public class SupportingTokenProviderSpecification
    {
        SecurityTokenAttachmentMode tokenAttachmentMode;
        SecurityTokenProvider tokenProvider;
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
            this.tokenProvider = tokenProvider;
            this.tokenAttachmentMode = attachmentMode;
            this.tokenParameters = tokenParameters;
        }

        public SecurityTokenProvider TokenProvider
        {
            get { return this.tokenProvider; }
        }

        public SecurityTokenAttachmentMode SecurityTokenAttachmentMode
        {
            get { return this.tokenAttachmentMode; }
        }

        public SecurityTokenParameters TokenParameters
        {
            get { return this.tokenParameters; }
        }
    }
}
