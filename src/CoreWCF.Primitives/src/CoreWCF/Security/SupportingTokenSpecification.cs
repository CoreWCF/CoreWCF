// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    public class SupportingTokenSpecification : SecurityTokenSpecification
    {
        private readonly SecurityTokenParameters tokenParameters;

        public SupportingTokenSpecification(SecurityToken token, ReadOnlyCollection<IAuthorizationPolicy> tokenPolicies, SecurityTokenAttachmentMode attachmentMode)
            : this(token, tokenPolicies, attachmentMode, null)
        { }

        public SupportingTokenSpecification(SecurityToken token, ReadOnlyCollection<IAuthorizationPolicy> tokenPolicies, SecurityTokenAttachmentMode attachmentMode, SecurityTokenParameters tokenParameters)
            : base(token, tokenPolicies)
        {
            SecurityTokenAttachmentModeHelper.Validate(attachmentMode);
            SecurityTokenAttachmentMode = attachmentMode;
            this.tokenParameters = tokenParameters;
        }

        public SecurityTokenAttachmentMode SecurityTokenAttachmentMode { get; }

        internal SecurityTokenParameters SecurityTokenParameters
        {
            get { return tokenParameters; }
        }
    }
}
