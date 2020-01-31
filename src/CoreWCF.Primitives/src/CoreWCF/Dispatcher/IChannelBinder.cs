using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;

namespace CoreWCF.Dispatcher
{
    internal interface IChannelBinder : IServiceChannelDispatcher
    {
        IChannel Channel { get; }
        bool HasSession { get; }
        Uri ListenUri { get; }
        EndpointAddress LocalAddress { get; }
        EndpointAddress RemoteAddress { get; }
        void Abort();
        void CloseAfterFault(TimeSpan timeout);
        Task SendAsync(Message message, CancellationToken token);
        Task<Message> RequestAsync(Message message, CancellationToken token);
        RequestContext CreateRequestContext(Message message);
        void SetNextDispatcher(IServiceChannelDispatcher dispatcher);
    }
}