using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel.Channels
{
    abstract class LayeredChannelListener<TChannel>
        : ChannelListenerBase<TChannel>
        where TChannel : class, IChannel
    {
        IChannelListener innerChannelListener;
        bool sharedInnerListener;
        EventHandler onInnerListenerFaulted;

        protected LayeredChannelListener(IDefaultCommunicationTimeouts timeouts, IChannelListener innerChannelListener)
            : this(false, timeouts, innerChannelListener)
        {
        }

        protected LayeredChannelListener(bool sharedInnerListener)
            : this(sharedInnerListener, null, null)
        {
        }

        protected LayeredChannelListener(bool sharedInnerListener, IDefaultCommunicationTimeouts timeouts)
            : this(sharedInnerListener, timeouts, null)
        {
        }

        protected LayeredChannelListener(bool sharedInnerListener, IDefaultCommunicationTimeouts timeouts, IChannelListener innerChannelListener)
            : base(timeouts)
        {
            this.sharedInnerListener = sharedInnerListener;
            this.innerChannelListener = innerChannelListener;
            onInnerListenerFaulted = new EventHandler(OnInnerListenerFaulted);
            if (this.innerChannelListener != null)
            {
                this.innerChannelListener.Faulted += onInnerListenerFaulted;
            }
        }

        internal virtual IChannelListener InnerChannelListener
        {
            get
            {
                return innerChannelListener;
            }
            set
            {
                lock (ThisLock)
                {
                    ThrowIfDisposedOrImmutable();
                    if (innerChannelListener != null)
                    {
                        innerChannelListener.Faulted -= onInnerListenerFaulted;
                    }
                    innerChannelListener = value;
                    if (innerChannelListener != null)
                    {
                        innerChannelListener.Faulted += onInnerListenerFaulted;
                    }
                }
            }
        }

        internal bool SharedInnerListener
        {
            get { return sharedInnerListener; }
        }

        public override Uri Uri
        {
            get { return GetInnerListenerSnapshot().Uri; }
        }

        public override T GetProperty<T>()
        {
            T baseProperty = base.GetProperty<T>();
            if (baseProperty != null)
            {
                return baseProperty;
            }

            IChannelListener channelListener = InnerChannelListener;
            if (channelListener != null)
            {
                return channelListener.GetProperty<T>();
            }
            else
            {
                return default(T);
            }
        }

        protected override void OnAbort()
        {
            lock (ThisLock)
            {
                OnCloseOrAbort();
            }
            IChannelListener channelListener = InnerChannelListener;
            if (channelListener != null && !sharedInnerListener)
            {
                channelListener.Abort();
            }
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            OnCloseOrAbort();
            if (InnerChannelListener != null && !sharedInnerListener)
            {
                return InnerChannelListener.CloseAsync(token);
            }

            return Task.CompletedTask;
        }

        void OnCloseOrAbort()
        {
            IChannelListener channelListener = InnerChannelListener;
            if (channelListener != null)
            {
                channelListener.Faulted -= onInnerListenerFaulted;
            }
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            if (InnerChannelListener != null && !sharedInnerListener)
                return InnerChannelListener.OpenAsync(token);

            return Task.CompletedTask;
        }

        protected override void OnOpening()
        {
            base.OnOpening();
            ThrowIfInnerListenerNotSet();
        }

        void OnInnerListenerFaulted(object sender, EventArgs e)
        {
            // if our inner listener faulted, we should fault as well
            Fault();
        }

        internal void ThrowIfInnerListenerNotSet()
        {
            if (InnerChannelListener == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.InnerListenerFactoryNotSet, GetType().ToString())));
            }
        }

        internal IChannelListener GetInnerListenerSnapshot()
        {
            IChannelListener innerChannelListener = InnerChannelListener;

            if (innerChannelListener == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.InnerListenerFactoryNotSet, GetType().ToString())));
            }

            return innerChannelListener;
        }
    }

    //abstract class LayeredChannelAcceptor<TChannel, TInnerChannel> : ChannelAcceptor<TChannel>
    //    where TChannel : class, IChannel
    //    where TInnerChannel : class, IChannel
    //{
    //    IChannelListener<TInnerChannel> innerListener;

    //    protected LayeredChannelAcceptor(ChannelManagerBase channelManager, IChannelListener<TInnerChannel> innerListener)
    //        : base(channelManager)
    //    {
    //        this.innerListener = innerListener;
    //    }

    //    protected abstract TChannel OnAcceptChannel(TInnerChannel innerChannel);

    //    public override async Task<TChannel> AcceptChannelAsync(CancellationToken token)
    //    {
    //        TInnerChannel innerChannel = await this.innerListener.AcceptChannelAsync(token);
    //        if (innerChannel == null)
    //            return null;
    //        else
    //            return OnAcceptChannel(innerChannel);
    //    }

    //    public override IAsyncResult BeginAcceptChannel(TimeSpan timeout, AsyncCallback callback, object state)
    //    {
    //        return this.innerListener.BeginAcceptChannel(timeout, callback, state);
    //    }

    //    public override TChannel EndAcceptChannel(IAsyncResult result)
    //    {
    //        TInnerChannel innerChannel = this.innerListener.EndAcceptChannel(result);
    //        if (innerChannel == null)
    //            return null;
    //        else
    //            return OnAcceptChannel(innerChannel);
    //    }

    //    public override bool WaitForChannel(TimeSpan timeout)
    //    {
    //        return this.innerListener.WaitForChannel(timeout);
    //    }

    //    public override IAsyncResult BeginWaitForChannel(TimeSpan timeout, AsyncCallback callback, object state)
    //    {
    //        return this.innerListener.BeginWaitForChannel(timeout, callback, state);
    //    }

    //    public override bool EndWaitForChannel(IAsyncResult result)
    //    {
    //        return this.innerListener.EndWaitForChannel(result);
    //    }
    //}

}