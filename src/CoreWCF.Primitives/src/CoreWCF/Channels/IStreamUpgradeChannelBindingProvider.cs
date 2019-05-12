using System;
using System.Collections.Generic;
using System.Security.Authentication.ExtendedProtection;
using System.Text;

namespace CoreWCF.Channels
{
    interface IStreamUpgradeChannelBindingProvider : IChannelBindingProvider
    {
        ChannelBinding GetChannelBinding(StreamUpgradeAcceptor upgradeAcceptor, ChannelBindingKind kind);
    }
}
