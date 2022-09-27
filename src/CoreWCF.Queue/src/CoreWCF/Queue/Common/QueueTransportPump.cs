// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Queue.Common
{
    public abstract class QueueTransportPump
    {
        public abstract Task StartPumpAsync(QueueTransportContext queueTransportContext, CancellationToken token);
        public abstract Task StopPumpAsync(CancellationToken token);

        public static QueueTransportPump CreateDefaultPump(IQueueTransport queueTransport)
        {
            return new DefaultQueueTransportPump(queueTransport);
        }
    }
}
