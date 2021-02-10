// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
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
