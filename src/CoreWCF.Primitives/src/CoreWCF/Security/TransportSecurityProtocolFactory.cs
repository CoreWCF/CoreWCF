
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

        public override bool SupportsDuplex => true;

        public override bool SupportsReplayDetection => false;

        internal override SecurityProtocol OnCreateSecurityProtocol(EndpointAddress target, Uri via, TimeSpan timeout)
        {
            return new TransportSecurityProtocol(this, target, via);
        }
    }
}
