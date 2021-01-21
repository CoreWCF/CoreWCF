// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class SessionSymmetricTransportSecurityProtocolFactory : TransportSecurityProtocolFactory
    {
        private SecurityTokenParameters securityTokenParameters;
        private SessionDerivedKeySecurityTokenParameters derivedKeyTokenParameters;

        public SessionSymmetricTransportSecurityProtocolFactory() : base()
        {
        }

        public override bool SupportsReplayDetection => true;

        public SecurityTokenParameters SecurityTokenParameters
        {
            get
            {
                return securityTokenParameters;
            }
            set
            {
                ThrowIfImmutable();
                securityTokenParameters = value;
            }
        }

        internal override SecurityProtocol OnCreateSecurityProtocol(EndpointAddress target, Uri via, TimeSpan timeout)
        {
            if (SecurityTokenParameters == null)
            {
                OnPropertySettingsError("SecurityTokenParameters", true);
            }
            if (SecurityTokenParameters.RequireDerivedKeys)
            {
                ExpectKeyDerivation = true;
                derivedKeyTokenParameters = new SessionDerivedKeySecurityTokenParameters(ActAsInitiator);
            }
            return new AcceptorSessionSymmetricTransportSecurityProtocol(this);

        }

        public override Task OnOpenAsync(TimeSpan timeout)
        {
            base.OnOpenAsync(timeout);
            if (SecurityTokenParameters == null)
            {
                OnPropertySettingsError("SecurityTokenParameters", true);
            }
            if (SecurityTokenParameters.RequireDerivedKeys)
            {
                ExpectKeyDerivation = true;
                derivedKeyTokenParameters = new SessionDerivedKeySecurityTokenParameters(ActAsInitiator);
            }
            return Task.CompletedTask;
        }

        internal SecurityTokenParameters GetTokenParameters()
        {
            if (derivedKeyTokenParameters != null)
            {
                return derivedKeyTokenParameters;
            }
            else
            {
                return securityTokenParameters;
            }
        }
    }
}
