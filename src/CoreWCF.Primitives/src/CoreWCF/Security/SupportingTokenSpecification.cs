using System.Collections.ObjectModel;
using CoreWCF;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    public class SupportingTokenSpecification : SecurityTokenSpecification
    {
        SecurityTokenAttachmentMode tokenAttachmentMode;
        SecurityTokenParameters tokenParameters;

        public SupportingTokenSpecification(SecurityToken token, ReadOnlyCollection<IAuthorizationPolicy> tokenPolicies, SecurityTokenAttachmentMode attachmentMode)
            : this(token, tokenPolicies, attachmentMode, null)
        { }

        public SupportingTokenSpecification(SecurityToken token, ReadOnlyCollection<IAuthorizationPolicy> tokenPolicies, SecurityTokenAttachmentMode attachmentMode, SecurityTokenParameters tokenParameters)
            : base(token, tokenPolicies)
        {
            SecurityTokenAttachmentModeHelper.Validate(attachmentMode);
            this.tokenAttachmentMode = attachmentMode;
            this.tokenParameters = tokenParameters;
        }

        public SecurityTokenAttachmentMode SecurityTokenAttachmentMode
        {
            get { return this.tokenAttachmentMode; }
        }

        internal SecurityTokenParameters SecurityTokenParameters
        {
            get { return this.tokenParameters; }
        }
    }
}
