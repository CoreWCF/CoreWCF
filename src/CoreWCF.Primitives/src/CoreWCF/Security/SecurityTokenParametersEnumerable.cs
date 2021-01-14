using System;
using System.Collections.Generic;
using CoreWCF.Channels;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class SecurityTokenParametersEnumerable : IEnumerable<SecurityTokenParameters>
    {
        private SecurityBindingElement sbe;
        private bool clientTokensOnly;

        public SecurityTokenParametersEnumerable(SecurityBindingElement sbe)
            : this(sbe, false) { }

        public SecurityTokenParametersEnumerable(SecurityBindingElement sbe, bool clientTokensOnly)
        {
            if (sbe == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("sbe");
            this.sbe = sbe;
            this.clientTokensOnly = clientTokensOnly;
        }

        public IEnumerator<SecurityTokenParameters> GetEnumerator()
        {
            if (this.sbe is SymmetricSecurityBindingElement)
            {
                SymmetricSecurityBindingElement ssbe = (SymmetricSecurityBindingElement)sbe;
                if (ssbe.ProtectionTokenParameters != null && (!this.clientTokensOnly || !ssbe.ProtectionTokenParameters.HasAsymmetricKey))
                    yield return ssbe.ProtectionTokenParameters;
            }
            // TODO
           /* else if (this.sbe is AsymmetricSecurityBindingElement)
            {
                AsymmetricSecurityBindingElement asbe = (AsymmetricSecurityBindingElement)sbe;
                if (asbe.InitiatorTokenParameters != null)
                    yield return asbe.InitiatorTokenParameters;
                if (asbe.RecipientTokenParameters != null && !this.clientTokensOnly)
                    yield return asbe.RecipientTokenParameters;
            }*/
            foreach (SecurityTokenParameters stp in this.sbe.EndpointSupportingTokenParameters.Endorsing)
                if (stp != null)
                    yield return stp;
            foreach (SecurityTokenParameters stp in this.sbe.EndpointSupportingTokenParameters.SignedEncrypted)
                if (stp != null)
                    yield return stp;
            foreach (SecurityTokenParameters stp in this.sbe.EndpointSupportingTokenParameters.SignedEndorsing)
                if (stp != null)
                    yield return stp;
            foreach (SecurityTokenParameters stp in this.sbe.EndpointSupportingTokenParameters.Signed)
                if (stp != null)
                    yield return stp;
            foreach (SupportingTokenParameters str in this.sbe.OperationSupportingTokenParameters.Values)
                if (str != null)
                {
                    foreach (SecurityTokenParameters stp in str.Endorsing)
                        if (stp != null)
                            yield return stp;
                    foreach (SecurityTokenParameters stp in str.SignedEncrypted)
                        if (stp != null)
                            yield return stp;
                    foreach (SecurityTokenParameters stp in str.SignedEndorsing)
                        if (stp != null)
                            yield return stp;
                    foreach (SecurityTokenParameters stp in str.Signed)
                        if (stp != null)
                            yield return stp;
                }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }
    }
}
