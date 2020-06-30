
using System;

namespace CoreWCF.Security
{
    internal class TransportSecurityProtocolFactory : SecurityProtocolFactory
    {
        public TransportSecurityProtocolFactory() : base()
        {
        }

        internal TransportSecurityProtocolFactory(TransportSecurityProtocolFactory factory) : base(factory)
        {
        }

        public override bool SupportsDuplex
        {
            get
            {
                return true;
            }
        }

        public override bool SupportsReplayDetection
        {
            get
            {
                return false;
            }
        }

        internal override SecurityProtocol OnCreateSecurityProtocol(EndpointAddress target, Uri via, object listenerSecurityState, TimeSpan timeout)
        {
            return new TransportSecurityProtocol(this, target, via);
        }
    }
}
