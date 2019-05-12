using System;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    internal class HttpReplyChannel : IReplyChannel
    {
        private EndpointAddress endpointAddress;

        public HttpReplyChannel(EndpointAddress endpointAddress)
        {
            this.endpointAddress = endpointAddress;
        }

        public EndpointAddress LocalAddress => throw new NotImplementedException();

        public CommunicationState State => throw new NotImplementedException();

        public event EventHandler Closed;
        public event EventHandler Closing;
        public event EventHandler Faulted;
        public event EventHandler Opened;
        public event EventHandler Opening;

        public void Abort()
        {
            throw new NotImplementedException();
        }

        public Task CloseAsync()
        {
            throw new NotImplementedException();
        }

        public Task CloseAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public T GetProperty<T>() where T : class
        {
            throw new NotImplementedException();
        }

        public Task OpenAsync()
        {
            throw new NotImplementedException();
        }

        public Task OpenAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task<RequestContext> ReceiveRequestAsync()
        {
            throw new NotImplementedException();
        }

        public Task<RequestContext> ReceiveRequestAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task<TryAsyncResult<RequestContext>> TryReceiveRequestAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task<bool> WaitForRequestAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}