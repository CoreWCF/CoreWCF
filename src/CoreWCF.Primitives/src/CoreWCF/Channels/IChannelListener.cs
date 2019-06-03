using System;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    public interface IChannelListener : ICommunicationObject
    {
        Uri Uri { get; }
        T GetProperty<T>() where T : class;
        // I believe WaitForChannel is only used with the TransactedChannelPump
        //Task<bool> WaitForChannelAsync(CancellationToken token);
    }

    public interface IChannelListener<TChannel> : IChannelListener
        where TChannel : class, IChannel
    {
        Task<TChannel> AcceptChannelAsync();
        Task<TChannel> AcceptChannelAsync(CancellationToken token);
    }
}