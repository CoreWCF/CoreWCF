using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.ServiceModel.Channels
{
    interface IConnectionOrientedTransportFactorySettings : ITransportFactorySettings, IConnectionOrientedConnectionSettings
    {
        int MaxBufferSize { get; }
        StreamUpgradeProvider Upgrade { get; }
        TransferMode TransferMode { get; }
        // Audit
        //ServiceSecurityAuditBehavior AuditBehavior { get; }
    }

    interface IConnectionOrientedConnectionSettings
    {
        int ConnectionBufferSize { get; }
        TimeSpan MaxOutputDelay { get; }
        TimeSpan IdleTimeout { get; }
    }

    interface IConnectionOrientedListenerSettings : IConnectionOrientedConnectionSettings
    {
        TimeSpan ChannelInitializationTimeout { get; }
        int MaxPendingConnections { get; }
        int MaxPendingAccepts { get; }
        int MaxPooledConnections { get; }
    }
}
