// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF.Security;

namespace CoreWCF.Channels
{
    public abstract class StreamSecurityUpgradeAcceptor : StreamUpgradeAcceptor
    {
        protected StreamSecurityUpgradeAcceptor()
        {
        }

        public abstract Task<SecurityMessageProperty> GetRemoteSecurityAsync(); // works after call to AcceptUpgrade
    }
}
