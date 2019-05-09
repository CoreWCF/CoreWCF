using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Runtime;
using Microsoft.ServiceModel.Channels;
using SessionIdleManager = Microsoft.ServiceModel.Channels.ServiceChannel.SessionIdleManager;

namespace Microsoft.ServiceModel.Dispatcher
{
    class ListenerHandler : CommunicationObject //, ISessionThrottleNotification
    {
        readonly ErrorHandlingAcceptor acceptor;
        readonly ChannelDispatcher channelDispatcher;
        ListenerChannel channel;
        SessionIdleManager idleManager;
        bool acceptedNull;
        bool doneAccepting;
        EndpointDispatcherTable endpoints;
        readonly ServiceHostBase host;
        readonly IListenerBinder listenerBinder;
        //readonly ServiceThrottle throttle;
        IDefaultCommunicationTimeouts timeouts;
        /*WrappedTransaction wrappedTransaction;*/
        CancellationTokenSource closingTokenSource;

        internal ListenerHandler(IListenerBinder listenerBinder, ChannelDispatcher channelDispatcher, ServiceHostBase host, /*ServiceThrottle throttle,*/ IDefaultCommunicationTimeouts timeouts)
        {
            this.listenerBinder = listenerBinder;
            if (!((this.listenerBinder != null)))
            {
                Fx.Assert("ListenerHandler.ctor: (this.listenerBinder != null)");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(listenerBinder));
            }

            this.channelDispatcher = channelDispatcher;
            if (!((this.channelDispatcher != null)))
            {
                Fx.Assert("ListenerHandler.ctor: (this.channelDispatcher != null)");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(channelDispatcher));
            }

            this.host = host;
            if (!((this.host != null)))
            {
                Fx.Assert("ListenerHandler.ctor: (this.host != null)");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(host));
            }

            //this.throttle = throttle;
            //if (!((this.throttle != null)))
            //{
            //    Fx.Assert("ListenerHandler.ctor: (this.throttle != null)");
            //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(throttle));
            //}

            this.timeouts = timeouts;

            //endpoints = channelDispatcher.EndpointDispatcherTable;
            acceptor = new ErrorHandlingAcceptor(listenerBinder, channelDispatcher);
            closingTokenSource = new CancellationTokenSource();
        }

        internal ChannelDispatcher ChannelDispatcher
        {
            get { return channelDispatcher; }
        }

        internal ListenerChannel Channel
        {
            get { return channel; }
        }

        protected override TimeSpan DefaultCloseTimeout
        {
            get { return host.CloseTimeout; }
        }

        protected override TimeSpan DefaultOpenTimeout
        {
            get { return host.OpenTimeout; }
        }

        internal EndpointDispatcherTable Endpoints
        {
            get { return endpoints; }
            set { endpoints = value; }
        }

        internal ServiceHostBase Host
        {
            get { return host; }
        }

        new internal object ThisLock
        {
            get { return base.ThisLock; }
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        protected override void OnOpened()
        {
            base.OnOpened();
            //channelDispatcher.Channels.IncrementActivityCount();
            //if (this.channelDispatcher.IsTransactedReceive && this.channelDispatcher.ReceiveContextEnabled && this.channelDispatcher.MaxTransactedBatchSize > 0)
            //{
            //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.IncompatibleBehaviors));
            //}
            NewChannelPump();
        }

        internal void NewChannelPump()
        {
            using (TaskHelpers.RunTaskContinuationsOnOurThreads())
            {
                ChannelPumpAsync();
            }
        }

        static void InitiateChannelPump(object state)
        {
            ListenerHandler listenerHandler = state as ListenerHandler;
            listenerHandler.ChannelPumpAsync();
        }

        async void ChannelPumpAsync()
        {
            IChannelListener listener = listenerBinder.Listener;

            for (;;)
            {
                if (acceptedNull || (listener.State == CommunicationState.Faulted))
                {
                    DoneAccepting();
                    break;
                }

                if (!await AcceptAndAcquireThrottleAsync())
                {
                    break;
                }

                Dispatch();
            }
        }

        void AbortChannels()
        {
            IChannel[] channels = Array.Empty<IChannel>();// channelDispatcher.Channels.ToArray();
            for (int index = 0; index < channels.Length; index++)
            {
                channels[index].Abort();
            }
        }

        async Task<bool> AcceptAndAcquireThrottleAsync()
        {
            var result = await acceptor.TryAcceptAsync(closingTokenSource.Token);
            if (result.Success)
            {
                var binder = result.Result;
                if (binder != null)
                {
                    channel = new ListenerChannel(binder);
                }
                else
                {
                    AcceptedNull();
                    channel = null;
                }
            }
            else
            {
                channel = null;
            }

            if (channel != null)
            {
                Fx.Assert(idleManager == null, "There cannot be an existing idle manager");
                //idleManager = SessionIdleManager.CreateIfNeeded(channel.Binder, channelDispatcher.DefaultCommunicationTimeouts.ReceiveTimeout);
            }
            else
            {
                DoneAccepting();
                return true;
            }

            return AcquireThrottle();
        }

        bool AcquireThrottle()
        {
            //if ((this.channel != null) && (this.throttle != null) && (this.channelDispatcher.Session))
            //{
            //    return this.throttle.AcquireSession(this);
            //}

            return true;
        }

        async Task CloseChannelAsync(IChannel channel, CancellationToken token)
        {
            try
            {
                if (channel.State != CommunicationState.Closing && channel.State != CommunicationState.Closed)
                {
                    CloseChannelState state = new CloseChannelState(this, channel);
                    if (channel is ISessionChannel<IDuplexSession>)
                    {
                        IDuplexSession duplexSession = ((ISessionChannel<IDuplexSession>)channel).Session;
                        await duplexSession.CloseOutputSessionAsync(token);
                    }
                    else
                    {
                        await channel.CloseAsync(token);
                    }
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                HandleError(e);

                if (channel is ISessionChannel<IDuplexSession>)
                {
                    channel.Abort();
                }
            }
        }

        public async Task CloseInputAsync(CancellationToken token)
        {
            closingTokenSource.Cancel();
            //TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            // Close all datagram channels
            IChannel[] channels = Array.Empty<IChannel>();// channelDispatcher.Channels.ToArray();
            for (int index = 0; index < channels.Length; index++)
            {
                IChannel channel = channels[index];
                if (!IsSessionChannel(channel))
                {
                    try
                    {
                        await channel.CloseAsync(token);
                    }
                    catch (Exception e)
                    {
                        if (Fx.IsFatal(e))
                        {
                            throw;
                        }
                        HandleError(e);
                    }
                }
            }
        }

        async Task CloseChannelsAsync(CancellationToken token)
        {
            IChannel[] channels = Array.Empty<IChannel>();// channelDispatcher.Channels.ToArray();
            Task[] tasks = new Task[channels.Length];
            for (int index = 0; index < channels.Length; index++)
                tasks[index] = CloseChannelAsync(channels[index], token);
            await Task.WhenAll(tasks);
        }

        void Dispatch()
        {
            ListenerChannel channel = this.channel;
            SessionIdleManager idleManager = this.idleManager;
            this.channel = null;
            this.idleManager = null;

            try
            {
                if (channel != null)
                {
                    ChannelHandler handler = new ChannelHandler(listenerBinder.MessageVersion, channel.Binder, /*this.throttle,*/ this, /*(channel.Throttle != null),*/ /*this.wrappedTransaction,*/ idleManager);

                    if (!channel.Binder.HasSession)
                    {
                        //channelDispatcher.Channels.Add(channel.Binder.Channel);
                    }

                    if (channel.Binder is DuplexChannelBinder)
                    {
                        DuplexChannelBinder duplexChannelBinder = channel.Binder as DuplexChannelBinder;
                        duplexChannelBinder.ChannelHandler = handler;
                        duplexChannelBinder.DefaultCloseTimeout = DefaultCloseTimeout;

                        if (timeouts == null)
                            duplexChannelBinder.DefaultSendTimeout = ServiceDefaults.SendTimeout;
                        else
                            duplexChannelBinder.DefaultSendTimeout = timeouts.SendTimeout;
                    }

                    ChannelHandler.Register(handler);
                    channel = null;
                    idleManager = null;
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                HandleError(e);
            }
            finally
            {
                if (channel != null)
                {
                    channel.Binder.Channel.Abort();
                    //if (this.throttle != null && this.channelDispatcher.Session)
                    //{
                    //    this.throttle.DeactivateChannel();
                    //}
                    if (idleManager != null)
                    {
                        idleManager.CancelTimer();
                    }
                }
            }
        }

        void AcceptedNull()
        {
            acceptedNull = true;
        }

        void DoneAccepting()
        {
            lock (ThisLock)
            {
                if (!doneAccepting)
                {
                    doneAccepting = true;
                    //channelDispatcher.Channels.DecrementActivityCount();
                }
            }
        }

        bool IsSessionChannel(IChannel channel)
        {
            return (channel is ISessionChannel<IDuplexSession> ||
                    channel is ISessionChannel<IInputSession> ||
                    channel is ISessionChannel<IOutputSession>);
        }

        void CancelPendingIdleManager()
        {
            SessionIdleManager idleManager = this.idleManager;
            if (idleManager != null)
            {
                idleManager.CancelTimer();
            }
        }

        protected override void OnAbort()
        {
            // if there's an idle manager that has not been transferred to the channel handler, cancel it
            CancelPendingIdleManager();

            // Start aborting incoming channels
            //channelDispatcher.Channels.CloseInput();

            // Abort existing channels
            AbortChannels();

            // Wait for channels to finish aborting
            //channelDispatcher.Channels.Abort();

        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            // if there's an idle manager that has not been cancelled, cancel it
            CancelPendingIdleManager();

            // Start aborting incoming channels
            //channelDispatcher.Channels.CloseInput();

            // Start closing existing channels
            await CloseChannelsAsync(token);

            // Wait for channels to finish closing
            //await channelDispatcher.Channels.CloseAsync(token);
        }

        bool HandleError(Exception e)
        {
            return channelDispatcher.HandleError(e);
        }

        class CloseChannelState
        {
            ListenerHandler listenerHandler;
            IChannel channel;

            internal CloseChannelState(ListenerHandler listenerHandler, IChannel channel)
            {
                this.listenerHandler = listenerHandler;
                this.channel = channel;
            }

            internal ListenerHandler ListenerHandler
            {
                get { return listenerHandler; }
            }

            internal IChannel Channel
            {
                get { return channel; }
            }
        }
    }

    class ListenerChannel
    {
        IChannelBinder binder;
        //ServiceThrottle throttle;

        public ListenerChannel(IChannelBinder binder)
        {
            this.binder = binder;
        }

        public IChannelBinder Binder
        {
            get { return binder; }
        }

        //public ServiceThrottle Throttle
        //{
        //    get { return this.throttle; }
        //    set { this.throttle = value; }
        //}
    }

}