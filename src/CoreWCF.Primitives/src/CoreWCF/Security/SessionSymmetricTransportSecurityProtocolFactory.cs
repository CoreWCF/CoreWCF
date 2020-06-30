using CoreWCF.Security.Tokens;
using CoreWCF;
using System;
using CoreWCF.Security;

namespace CoreWCF.Security
{
    class SessionSymmetricTransportSecurityProtocolFactory : TransportSecurityProtocolFactory
    {
        SecurityTokenParameters securityTokenParameters;
        SessionDerivedKeySecurityTokenParameters derivedKeyTokenParameters;

        public SessionSymmetricTransportSecurityProtocolFactory()
            : base()
        {
        }

        public override bool SupportsReplayDetection
        {
            get
            {
                return true;
            }
        }

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

        internal override SecurityProtocol OnCreateSecurityProtocol(EndpointAddress target, Uri via, object listenerSecurityState, TimeSpan timeout)
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

        //public override void OnOpen(TimeSpan timeout)
        //{
        //    base.OnOpen(timeout);
           
        //}

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
