using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.ServiceModel.Channels
{
    public abstract class StreamSecurityUpgradeProvider : StreamUpgradeProvider
    {
        protected StreamSecurityUpgradeProvider()
            : base()
        {
        }

        protected StreamSecurityUpgradeProvider(IDefaultCommunicationTimeouts timeouts)
            : base(timeouts)
        {
        }

        public abstract EndpointIdentity Identity
        {
            get;
        }
    }
}
