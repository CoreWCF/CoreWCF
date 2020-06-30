using CoreWCF.IdentityModel.Claims;
using CoreWCF;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;
using CoreWCF.Diagnostics;

namespace CoreWCF.Security
{
    class SecurityProtocolCorrelationState
    {
        SecurityToken token;
        SignatureConfirmations signatureConfirmations;
       // ServiceModelActivity activity;

        public SecurityProtocolCorrelationState(SecurityToken token)
        {
            this.token = token;
          //  this.activity = DiagnosticUtility.ShouldUseActivity ? ServiceModelActivity.Current : null;
        }

        public SecurityToken Token
        {
            get { return this.token; }
        }

        internal SignatureConfirmations SignatureConfirmations
        {
            get { return this.signatureConfirmations; }
            set { this.signatureConfirmations = value; }
        }

        //internal ServiceModelActivity Activity
        //{
        //    get { return this.activity; }
        //}
    }
}
