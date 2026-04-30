// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class SessionSymmetricTransportSecurityProtocolFactory : TransportSecurityProtocolFactory
    {
        private SecurityTokenParameters _securityTokenParameters;
        private SessionDerivedKeySecurityTokenParameters _derivedKeyTokenParameters;

        public SessionSymmetricTransportSecurityProtocolFactory() : base()
        {
        }

        public override bool SupportsReplayDetection => true;

        public SecurityTokenParameters SecurityTokenParameters
        {
            get
            {
                return _securityTokenParameters;
            }
            set
            {
                ThrowIfImmutable();
                _securityTokenParameters = value;
            }
        }

        internal override SecurityProtocol OnCreateSecurityProtocol(EndpointAddress target, Uri via, TimeSpan timeout)
        {
            if (SecurityTokenParameters == null)
            {
                OnPropertySettingsError(nameof(SecurityTokenParameters), true);
            }
            if (SecurityTokenParameters.RequireDerivedKeys)
            {
                ExpectKeyDerivation = true;
                _derivedKeyTokenParameters = new SessionDerivedKeySecurityTokenParameters();
            }
            return new AcceptorSessionSymmetricTransportSecurityProtocol(this);
        }

        public override async Task OnOpenAsync(CancellationToken token)
        {
            await base.OnOpenAsync(token);
            if (SecurityTokenParameters == null)
            {
                OnPropertySettingsError(nameof(SecurityTokenParameters), true);
            }
            if (SecurityTokenParameters.RequireDerivedKeys)
            {
                ExpectKeyDerivation = true;
                _derivedKeyTokenParameters = new SessionDerivedKeySecurityTokenParameters();
            }
        }

        internal SecurityTokenParameters GetTokenParameters()
        {
            if (_derivedKeyTokenParameters != null)
            {
                return _derivedKeyTokenParameters;
            }
            else
            {
                return _securityTokenParameters;
            }
        }
    }
}
