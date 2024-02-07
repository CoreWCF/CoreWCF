// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class SessionSymmetricMessageSecurityProtocolFactory : MessageSecurityProtocolFactory
    {
        private SecurityTokenParameters securityTokenParameters;
        private SessionDerivedKeySecurityTokenParameters derivedKeyTokenParameters;

        public SessionSymmetricMessageSecurityProtocolFactory()
            : base()
        {
        }

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

        public override EndpointIdentity GetIdentityOfSelf()
        {
            if (SecurityTokenManager is IEndpointIdentityProvider)
            {
                SecurityTokenRequirement requirement = CreateRecipientSecurityTokenRequirement();
                SecurityTokenParameters.InitializeSecurityTokenRequirement(requirement);
                return ((IEndpointIdentityProvider)SecurityTokenManager).GetIdentityOfSelf(requirement);
            }
            else
            {
                return base.GetIdentityOfSelf();
            }
        }

        protected SecurityProtocol OnCreateSecurityProtocol(EndpointAddress target, Uri via, object listenerSecurityState, TimeSpan timeout)
        {
            if (ActAsInitiator)
            {
                throw new NotImplementedException("Server code");
                //return new InitiatorSessionSymmetricMessageSecurityProtocol(this, target, via);
            }
            else
            {
                return new AcceptorSessionSymmetricMessageSecurityProtocol(this, null);
            }
        }

        public override async Task OnOpenAsync(TimeSpan timeout)
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
            await base.OnOpenAsync(timeout);
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

        internal override SecurityProtocol OnCreateSecurityProtocol(EndpointAddress target, Uri via, TimeSpan timeout) => OnCreateSecurityProtocol(target, via, null,timeout);
    }
}
