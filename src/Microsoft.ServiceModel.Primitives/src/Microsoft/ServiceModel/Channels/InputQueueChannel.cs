using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Runtime;
using Microsoft.ServiceModel.Diagnostics;

namespace Microsoft.ServiceModel.Channels
{
    public abstract class InputQueueChannel<TDisposable> : ChannelBase
        where TDisposable : class, IDisposable
    {
        InputQueue<TDisposable> inputQueue;

        protected InputQueueChannel(ChannelManagerBase channelManager)
            : base(channelManager)
        {
            inputQueue = new InputQueue<TDisposable>();
        }

        public int InternalPendingItems
        {
            get
            {
                return inputQueue.PendingCount;
            }
        }

        public int PendingItems
        {
            get
            {
                ThrowIfDisposedOrNotOpen();
                return InternalPendingItems;
            }
        }

        public void EnqueueAndDispatch(TDisposable item)
        {
            EnqueueAndDispatch(item, null);
        }

        public void EnqueueAndDispatch(TDisposable item, Action dequeuedCallback, bool canDispatchOnThisThread)
        {
            OnEnqueueItem(item);

            // NOTE: don't need to check IsDisposed here: InputQueue will handle dispose
            inputQueue.EnqueueAndDispatch(item, dequeuedCallback, canDispatchOnThisThread);
        }

        public void EnqueueAndDispatch(Exception exception, Action dequeuedCallback, bool canDispatchOnThisThread)
        {
            // NOTE: don't need to check IsDisposed here: InputQueue will handle dispose
            inputQueue.EnqueueAndDispatch(exception, dequeuedCallback, canDispatchOnThisThread);
        }

        public void EnqueueAndDispatch(TDisposable item, Action dequeuedCallback)
        {
            OnEnqueueItem(item);

            // NOTE: don't need to check IsDisposed here: InputQueue will handle dispose
            inputQueue.EnqueueAndDispatch(item, dequeuedCallback);
        }

        public bool EnqueueWithoutDispatch(Exception exception, Action dequeuedCallback)
        {
            // NOTE: don't need to check IsDisposed here: InputQueue will handle dispose
            return inputQueue.EnqueueWithoutDispatch(exception, dequeuedCallback);
        }

        public bool EnqueueWithoutDispatch(TDisposable item, Action dequeuedCallback)
        {
            OnEnqueueItem(item);

            // NOTE: don't need to check IsDisposed here: InputQueue will handle dispose
            return inputQueue.EnqueueWithoutDispatch(item, dequeuedCallback);
        }

        public void Dispatch()
        {
            // NOTE: don't need to check IsDisposed here: InputQueue will handle dispose
            inputQueue.Dispatch();
        }

        public void Shutdown()
        {
            inputQueue.Shutdown();
        }

        protected override void OnFaulted()
        {
            base.OnFaulted();
            inputQueue.Shutdown(() => GetPendingException());
        }

        protected virtual void OnEnqueueItem(TDisposable item)
        {
        }

        protected async Task<TryAsyncResult<TDisposable>> DequeueAsync(CancellationToken token)
        {
            ThrowIfNotOpened();
            var result = await inputQueue.TryDequeueAsync(token);
            bool dequeued = result.Success;

            if (result.Result == null)
            {
                ThrowIfFaulted();
                ThrowIfAborted();
            }

            return result;
        }

        protected async Task<bool> WaitForItemAsync(CancellationToken token)
        {
            ThrowIfNotOpened();
            bool dequeued = await inputQueue.WaitForItemAsync(token);

            ThrowIfFaulted();
            ThrowIfAborted();

            return dequeued;
        }

        protected override void OnClosing()
        {
            base.OnClosing();
            inputQueue.Shutdown(() => GetPendingException());
        }

        protected override void OnAbort()
        {
            inputQueue.Close();
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            inputQueue.Close();
            return Task.CompletedTask;
        }
    }
}