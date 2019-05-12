using System;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    interface IChannelAcceptor<TChannel> : ICommunicationObject
        where TChannel : class, IChannel
    {
        Task<TChannel> AcceptChannelAsync(CancellationToken token);
        Task<bool> WaitForChannelAsync(CancellationToken token);
    }
}