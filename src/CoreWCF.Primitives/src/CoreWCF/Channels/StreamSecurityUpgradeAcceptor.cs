using System;
using System.Collections.Generic;
using System.Text;
using CoreWCF.Security;

namespace CoreWCF.Channels
{
    public abstract class StreamSecurityUpgradeAcceptor : StreamUpgradeAcceptor
    {
        protected StreamSecurityUpgradeAcceptor()
        {
        }

        public abstract SecurityMessageProperty GetRemoteSecurity(); // works after call to AcceptUpgrade
    }
}
