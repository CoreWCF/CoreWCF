using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal interface IListenerBinder
    {
        IChannelListener Listener { get; }
        MessageVersion MessageVersion { get; }
        Task<IChannelBinder> AcceptAsync(CancellationToken token);
    }
}