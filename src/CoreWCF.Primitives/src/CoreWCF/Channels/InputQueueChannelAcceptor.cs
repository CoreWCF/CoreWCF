using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using CoreWCF.Diagnostics;

namespace CoreWCF.Channels
{
    // TODO: Consider making common code compiled into each assembly. These are private on the full framework and are now public.
    public class InputQueueChannelAcceptor<TChannel> : ChannelAcceptor<TChannel>
        where TChannel : class, IChannel
    {
        InputQueue<TChannel> channelQueue;

        public InputQueueChannelAcceptor(ChannelManagerBase channelManager)
            : base(channelManager)
        {
            channelQueue = new InputQueue<TChannel>()
            {
                DisposeItemCallback = value =>
                {
                    if (value is ICommunicationObject)
                    {
                        ((ICommunicationObject)value).Abort();
                    }
                }
            };
        }

        public int PendingCount
        {
            get { return channelQueue.PendingCount; }
        }

        public override Task<TChannel> AcceptChannelAsync(CancellationToken token)
        {
            ThrowIfNotOpened();
            return channelQueue.DequeueAsync(token);
        }

        public void Dispatch()
        {
            channelQueue.Dispatch();
        }

        public void EnqueueAndDispatch(TChannel channel)
        {
            channelQueue.EnqueueAndDispatch(channel);
        }

        public void EnqueueAndDispatch(TChannel channel, Action dequeuedCallback)
        {
            channelQueue.EnqueueAndDispatch(channel, dequeuedCallback);
        }

        public bool EnqueueWithoutDispatch(TChannel channel, Action dequeuedCallback)
        {
            return channelQueue.EnqueueWithoutDispatch(channel, dequeuedCallback);
        }

        public virtual bool EnqueueWithoutDispatch(Exception exception, Action dequeuedCallback)
        {
            return channelQueue.EnqueueWithoutDispatch(exception, dequeuedCallback);
        }

        public void EnqueueAndDispatch(TChannel channel, Action dequeuedCallback, bool canDispatchOnThisThread)
        {
            channelQueue.EnqueueAndDispatch(channel, dequeuedCallback, canDispatchOnThisThread);
        }

        public virtual void EnqueueAndDispatch(Exception exception, Action dequeuedCallback, bool canDispatchOnThisThread)
        {
            channelQueue.EnqueueAndDispatch(exception, dequeuedCallback, canDispatchOnThisThread);
        }

        public void FaultQueue()
        {
            Fault();
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            channelQueue.Dispose();
        }

        protected override void OnFaulted()
        {
            channelQueue.Shutdown(() => ChannelManager.GetPendingException());
            base.OnFaulted();
        }

        public override Task<bool> WaitForChannelAsync(CancellationToken token)
        {
            ThrowIfNotOpened();
            return channelQueue.WaitForItemAsync(token);
        }
    }
}