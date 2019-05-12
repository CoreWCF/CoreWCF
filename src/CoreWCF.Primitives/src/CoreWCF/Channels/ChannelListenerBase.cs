using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    public abstract class ChannelListenerBase : ChannelManagerBase, IChannelListener
    {
        TimeSpan closeTimeout = ServiceDefaults.CloseTimeout;
        TimeSpan openTimeout = ServiceDefaults.OpenTimeout;
        TimeSpan receiveTimeout = ServiceDefaults.ReceiveTimeout;
        TimeSpan sendTimeout = ServiceDefaults.SendTimeout;

        protected ChannelListenerBase()
        {
        }

        protected ChannelListenerBase(IDefaultCommunicationTimeouts timeouts)
        {
            if (timeouts != null)
            {
                closeTimeout = timeouts.CloseTimeout;
                openTimeout = timeouts.OpenTimeout;
                receiveTimeout = timeouts.ReceiveTimeout;
                sendTimeout = timeouts.SendTimeout;
            }
        }

        protected override TimeSpan DefaultCloseTimeout
        {
            get { return closeTimeout; }
        }

        protected override TimeSpan DefaultOpenTimeout
        {
            get { return openTimeout; }
        }

        protected override TimeSpan DefaultReceiveTimeout
        {
            get { return receiveTimeout; }
        }

        protected override TimeSpan DefaultSendTimeout
        {
            get { return sendTimeout; }
        }

        public abstract Uri Uri { get; }

        public virtual T GetProperty<T>()
            where T : class
        {
            if (typeof(T) == typeof(IChannelListener))
            {
                return (T)(object)this;
            }

            return default(T);
        }

        //public bool WaitForChannel(TimeSpan timeout)
        //{
        //    this.ThrowIfNotOpened();
        //    this.ThrowPending();
        //    return this.OnWaitForChannel(timeout);
        //}

        //public IAsyncResult BeginWaitForChannel(TimeSpan timeout, AsyncCallback callback, object state)
        //{
        //    this.ThrowIfNotOpened();
        //    this.ThrowPending();
        //    return this.OnBeginWaitForChannel(timeout, callback, state);
        //}

        //public bool EndWaitForChannel(IAsyncResult result)
        //{
        //    return this.OnEndWaitForChannel(result);
        //}

        //protected abstract bool OnWaitForChannel(TimeSpan timeout);
        //protected abstract IAsyncResult OnBeginWaitForChannel(TimeSpan timeout, AsyncCallback callback, object state);
        //protected abstract bool OnEndWaitForChannel(IAsyncResult result);

    }

    public abstract class ChannelListenerBase<TChannel> : ChannelListenerBase, IChannelListener<TChannel>
        where TChannel : class, IChannel
    {
        //private AcceptChannelDelegate _acceptHandler;
        //private ExceptionHandlerDelegate _exceptionHandler;
        //private bool _acceptLoopStarted = false;

        protected ChannelListenerBase()
        {
        }

        protected ChannelListenerBase(IDefaultCommunicationTimeouts timeouts)
            : base(timeouts)
        {
        }

        protected abstract Task<TChannel> OnAcceptChannelAsync(CancellationToken token);

        //protected abstract IAsyncResult OnBeginAcceptChannel(TimeSpan timeout, AsyncCallback callback, object state);
        //protected abstract TChannel OnEndAcceptChannel(IAsyncResult result);

        public Task<TChannel> AcceptChannelAsync()
        {
            var helper = new TimeoutHelper(InternalReceiveTimeout);
            return AcceptChannelAsync(helper.GetCancellationToken());
        }

        public Task<TChannel> AcceptChannelAsync(CancellationToken token)
        {
            ThrowIfNotOpened();
            ThrowPending();
            return OnAcceptChannelAsync(token);
        }


//#pragma warning disable RECS0165 // Asynchronous methods should return a Task instead of void
//        public async void AcceptChannelLoopAsync(CancellationToken token)
//#pragma warning restore RECS0165 // Asynchronous methods should return a Task instead of void
//        {
//            if (_acceptLoopStarted)
//                return;
//            this.ThrowIfNotOpened();
//            this.ThrowPending();
//            do
//            {
//                try
//                {
//                    TChannel channel = await OnAcceptChannelAsync(token);
//                    await _acceptHandler(channel);
//                }
//                catch (Exception e)
//                {
//                    if (_exceptionHandler != null)
//                        await _exceptionHandler(e);
//                }
//            } while (!IsDisposed);
//        }

//        public virtual void UseHandler(AcceptChannelDelegate handler)
//        {
//            // TODO: work out how multiple waiters should work.
//            _acceptHandler = handler;
//            AcceptChannelLoopAsync(CancellationToken.None);
//        }

        //public virtual void UseExceptionHandler(ExceptionHandlerDelegate handler)
        //{
        //    _exceptionHandler = handler;
        //}
    }
}