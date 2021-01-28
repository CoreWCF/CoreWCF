// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Channels;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class SecurityTokenParametersEnumerable : IEnumerable<SecurityTokenParameters>
    {
        private readonly SecurityBindingElement _sbe;
        private readonly bool _clientTokensOnly;

        public SecurityTokenParametersEnumerable(SecurityBindingElement sbe)
            : this(sbe, false) { }

        public SecurityTokenParametersEnumerable(SecurityBindingElement sbe, bool clientTokensOnly)
        {
            if (sbe == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("sbe");
            }

            _sbe = sbe;
            _clientTokensOnly = clientTokensOnly;
        }

        public IEnumerator<SecurityTokenParameters> GetEnumerator()
        {
            if (_sbe is SymmetricSecurityBindingElement)
            {
                SymmetricSecurityBindingElement ssbe = (SymmetricSecurityBindingElement)_sbe;
                if (ssbe.ProtectionTokenParameters != null && (!_clientTokensOnly || !ssbe.ProtectionTokenParameters.HasAsymmetricKey))
                {
                    yield return ssbe.ProtectionTokenParameters;
                }
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
            foreach (SecurityTokenParameters stp in _sbe.EndpointSupportingTokenParameters.Endorsing)
            {
                if (stp != null)
                {
                    yield return stp;
                }
            }

            foreach (SecurityTokenParameters stp in _sbe.EndpointSupportingTokenParameters.SignedEncrypted)
            {
                if (stp != null)
                {
                    yield return stp;
                }
            }

            foreach (SecurityTokenParameters stp in _sbe.EndpointSupportingTokenParameters.SignedEndorsing)
            {
                if (stp != null)
                {
                    yield return stp;
                }
            }

            foreach (SecurityTokenParameters stp in _sbe.EndpointSupportingTokenParameters.Signed)
            {
                if (stp != null)
                {
                    yield return stp;
                }
            }

            foreach (SupportingTokenParameters str in _sbe.OperationSupportingTokenParameters.Values)
            {
                if (str != null)
                {
                    foreach (SecurityTokenParameters stp in str.Endorsing)
                    {
                        if (stp != null)
                        {
                            yield return stp;
                        }
                    }

                    foreach (SecurityTokenParameters stp in str.SignedEncrypted)
                    {
                        if (stp != null)
                        {
                            yield return stp;
                        }
                    }

                    foreach (SecurityTokenParameters stp in str.SignedEndorsing)
                    {
                        if (stp != null)
                        {
                            yield return stp;
                        }
                    }

                    foreach (SecurityTokenParameters stp in str.Signed)
                    {
                        if (stp != null)
                        {
                            yield return stp;
                        }
                    }
                }
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }
    }
}
