using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal interface IChannelBinder
    {
        IChannel Channel { get; }
        bool HasSession { get; }
        Uri ListenUri { get; }
        EndpointAddress LocalAddress { get; }
        EndpointAddress RemoteAddress { get; }
        void Abort();
        void CloseAfterFault(TimeSpan timeout);
        Task<TryAsyncResult<RequestContext>> TryReceiveAsync(CancellationToken token);
        Task SendAsync(Message message, CancellationToken token);
        Task<Message> RequestAsync(Message message, CancellationToken token);
        Task<bool> WaitForMessageAsync(CancellationToken token);
        RequestContext CreateRequestContext(Message message);
    }
}