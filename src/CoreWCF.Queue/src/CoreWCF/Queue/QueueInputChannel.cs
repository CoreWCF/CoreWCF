using System;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Queue.Common
{
    public class QueueInputChannel : IChannel
    {
        public IServiceChannelDispatcher ChannelDispatcher { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public CommunicationState State => throw new NotImplementedException();

        public event EventHandler Closed;
        public event EventHandler Closing;
        public event EventHandler Faulted;
        public event EventHandler Opened;
        public event EventHandler Opening;

        public void Abort() => throw new NotImplementedException();
        public Task CloseAsync() => throw new NotImplementedException();
        public Task CloseAsync(CancellationToken token) => throw new NotImplementedException();
        public virtual T GetProperty<T>() where T : class => throw new NotImplementedException();
        public Task OpenAsync() => throw new NotImplementedException();
        public Task OpenAsync(CancellationToken token) => throw new NotImplementedException();
    }
}

