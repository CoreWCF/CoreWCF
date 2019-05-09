using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ServiceModel.Security;

namespace Microsoft.ServiceModel.Channels
{
    public abstract class StreamSecurityUpgradeAcceptor : StreamUpgradeAcceptor
    {
        protected StreamSecurityUpgradeAcceptor()
        {
        }

        public abstract SecurityMessageProperty GetRemoteSecurity(); // works after call to AcceptUpgrade
    }
}
