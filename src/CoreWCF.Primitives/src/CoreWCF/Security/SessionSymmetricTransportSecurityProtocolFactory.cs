using CoreWCF.Security.Tokens;
using System;
using System.Threading.Tasks;

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
                return this.securityTokenParameters;
            }
            set
            {
                ThrowIfImmutable();
                this.securityTokenParameters = value;
            }
        }

        internal override SecurityProtocol OnCreateSecurityProtocol(EndpointAddress target, Uri via, TimeSpan timeout)
        {
            if (this.ActAsInitiator)
            {
                throw new NotImplementedException(""); // Not needed for server side
               // return new InitiatorSessionSymmetricTransportSecurityProtocol(this, target, via);
            }
            else
            {
                if (this.SecurityTokenParameters == null)
                {
                    OnPropertySettingsError("SecurityTokenParameters", true);
                }
                if (this.SecurityTokenParameters.RequireDerivedKeys)
                {
                    this.ExpectKeyDerivation = true;
                    this.derivedKeyTokenParameters = new SessionDerivedKeySecurityTokenParameters(this.ActAsInitiator);
                }
                return new AcceptorSessionSymmetricTransportSecurityProtocol(this);
            }
        }

        public override Task OpenAsync(TimeSpan timeout)
        {
            base.OpenAsync(timeout);
            if (this.SecurityTokenParameters == null)
            {
                OnPropertySettingsError("SecurityTokenParameters", true);
            }
            if (this.SecurityTokenParameters.RequireDerivedKeys)
            {
                this.ExpectKeyDerivation = true;
                this.derivedKeyTokenParameters = new SessionDerivedKeySecurityTokenParameters(this.ActAsInitiator);
            }
            return Task.CompletedTask;
        }

        internal SecurityTokenParameters GetTokenParameters()
        {
            if (this.derivedKeyTokenParameters != null)
            {
                return this.derivedKeyTokenParameters;
            }
            else
            {
                return this.securityTokenParameters;
            }
        }
    }
}
