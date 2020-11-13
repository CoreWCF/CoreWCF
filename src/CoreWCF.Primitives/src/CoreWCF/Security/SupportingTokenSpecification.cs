using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;
using System.Collections.ObjectModel;

namespace CoreWCF.Security
{
    public class SupportingTokenSpecification : SecurityTokenSpecification
    {
        SecurityTokenParameters tokenParameters;

        public SupportingTokenSpecification(SecurityToken token, ReadOnlyCollection<IAuthorizationPolicy> tokenPolicies, SecurityTokenAttachmentMode attachmentMode)
            : this(token, tokenPolicies, attachmentMode, null)
        { }

        public SupportingTokenSpecification(SecurityToken token, ReadOnlyCollection<IAuthorizationPolicy> tokenPolicies, SecurityTokenAttachmentMode attachmentMode, SecurityTokenParameters tokenParameters)
            : base(token, tokenPolicies)
        {
            SecurityTokenAttachmentModeHelper.Validate(attachmentMode);
            this.SecurityTokenAttachmentMode = attachmentMode;
            this.tokenParameters = tokenParameters;
        }

        public SecurityTokenAttachmentMode SecurityTokenAttachmentMode { get; }

        internal SecurityTokenParameters SecurityTokenParameters
        {
            get { return this.tokenParameters; }
        }
    }
}
