// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security
{
    internal class SecurityProtocolCorrelationState
    {
        private SignatureConfirmations signatureConfirmations;
        // ServiceModelActivity activity;

        public SecurityProtocolCorrelationState(SecurityToken token)
        {
            Token = token;
            //  this.activity = DiagnosticUtility.ShouldUseActivity ? ServiceModelActivity.Current : null;
        }

        public SecurityToken Token { get; }

        internal SignatureConfirmations SignatureConfirmations
        {
            get { return signatureConfirmations; }
            set { signatureConfirmations = value; }
        }

        //internal ServiceModelActivity Activity
        //{
        //    get { return this.activity; }
        //}
    }
}
