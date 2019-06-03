using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal interface IListenerBinder
    {
        IChannelListener Listener { get; }
        MessageVersion MessageVersion { get; }
        Task<IChannelBinder> AcceptAsync(CancellationToken token);
    }
}