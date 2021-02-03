// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
