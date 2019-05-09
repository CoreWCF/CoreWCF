using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Runtime;
using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal class BufferedReceiveBinder : IChannelBinder
    {
        static Action<object> tryReceive = TryReceive;

        IChannelBinder channelBinder;
        InputQueue<RequestContextWrapper> inputQueue;

        
        int pendingOperationSemaphore;

        public BufferedReceiveBinder(IChannelBinder channelBinder)
        {
            this.channelBinder = channelBinder;
            inputQueue = new InputQueue<RequestContextWrapper>();
        }

        public IChannel Channel
        {
            get { return channelBinder.Channel; }
        }

        public bool HasSession
        {
            get { return channelBinder.HasSession; }
        }

        public Uri ListenUri
        {
            get { return channelBinder.ListenUri; }
        }

        public EndpointAddress LocalAddress
        {
            get { return channelBinder.LocalAddress; }
        }

        public EndpointAddress RemoteAddress
        {
            get { return channelBinder.RemoteAddress; }
        }

        public void Abort()
        {
            inputQueue.Close();
            channelBinder.Abort();
        }

        public void CloseAfterFault(TimeSpan timeout)
        {
            inputQueue.Close();
            channelBinder.CloseAfterFault(timeout);
        }

        // Locking:
        // Only 1 channelBinder operation call should be active at any given time. All future calls
        // will wait on the inputQueue. The semaphore is always released right before the Dispatch on the inputQueue.
        // This protects a new call racing with an existing operation that is just about to fully complete.

        public async Task<TryAsyncResult<RequestContext>> TryReceiveAsync(CancellationToken token)
        {
            if (Interlocked.CompareExchange(ref pendingOperationSemaphore, 1, 0) == 0)
            {
                ActionItem.Schedule(tryReceive, this);
            }

            var result = await inputQueue.TryDequeueAsync(token);
            bool success = result.Success;
            RequestContextWrapper wrapper = result.Result;

            if (success && wrapper != null)
            {
                return TryAsyncResult.FromResult(wrapper.RequestContext);
            }

            return TryAsyncResult<RequestContext>.FailedResult;

        }

        public RequestContext CreateRequestContext(Message message)
        {
            return channelBinder.CreateRequestContext(message);
        }

        public Task SendAsync(Message message, CancellationToken token)
        {
            return channelBinder.SendAsync(message, token);
        }

        public Task<Message> RequestAsync(Message message, CancellationToken token)
        {
            return channelBinder.RequestAsync(message, token);
        }

        public Task<bool> WaitForMessageAsync(CancellationToken token)
        {
            return channelBinder.WaitForMessageAsync(token);
        }

        internal void InjectRequest(RequestContext requestContext)
        {
            // Reuse the existing requestContext
            inputQueue.EnqueueAndDispatch(new RequestContextWrapper(requestContext));
        }

        //
        // TryReceive threads
        //

        static async void TryReceive(object state)
        {
            BufferedReceiveBinder binder = (BufferedReceiveBinder)state;

            bool requiresDispatch = false;
            try
            {
                var result = await binder.channelBinder.TryReceiveAsync(CancellationToken.None);
                if (result.Success)
                {
                    requiresDispatch = binder.inputQueue.EnqueueWithoutDispatch(new RequestContextWrapper(result.Result), null);
                }
            }
            catch (Exception exception)
            {
                if (Fx.IsFatal(exception))
                {
                    throw;
                }

                requiresDispatch = binder.inputQueue.EnqueueWithoutDispatch(exception, null);
            }
            finally
            {
                Interlocked.Exchange(ref binder.pendingOperationSemaphore, 0);
                if (requiresDispatch)
                {
                    binder.inputQueue.Dispatch();
                }
            }
        }

        // A RequestContext may be 'null' (some pieces of ChannelHandler depend on this) but the InputQueue
        // will not allow null items to be enqueued. Wrap the RequestContexts in another object to
        // facilitate this semantic
        class RequestContextWrapper
        {
            public RequestContextWrapper(RequestContext requestContext)
            {
                RequestContext = requestContext;
            }

            public RequestContext RequestContext
            {
                get;
                private set;
            }
        }
    }

}