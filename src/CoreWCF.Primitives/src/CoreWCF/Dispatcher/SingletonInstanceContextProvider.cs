using System;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal class SingletonInstanceContextProvider : InstanceContextProviderBase
    {
        InstanceContext singleton;
        object thisLock;

        internal SingletonInstanceContextProvider(DispatchRuntime dispatchRuntime)
            : base(dispatchRuntime)
        {
            thisLock = new object();
        }

        internal InstanceContext SingletonInstance
        {
            get
            {
                if (singleton == null)
                {
                    lock (thisLock)
                    {
                        if (singleton == null)
                        {
                            InstanceContext instanceContext = DispatchRuntime.SingletonInstanceContext;

                            if (instanceContext == null)
                            {
                                instanceContext = new InstanceContext(DispatchRuntime.ChannelDispatcher.Host, false);
                                instanceContext.OpenAsync().GetAwaiter().GetResult();
                            }
                            else if (instanceContext.State != CommunicationState.Opened)
                            {
                                // we need to lock against the instance context for open since two different endpoints could
                                // share the same instance context, but different providers. So the provider lock does not guard
                                // the open process
                                lock (instanceContext.ThisLock)
                                {
                                    if (instanceContext.State != CommunicationState.Opened)
                                    {
                                        instanceContext.OpenAsync().GetAwaiter().GetResult();
                                    }
                                }
                            }

                            //Set the IsUsercreated flag to false for singleton mode even in cases when users create their own runtime.
                            instanceContext.IsUserCreated = false;

                            //Delay assigning the potentially newly created InstanceContext (till after its opened) to this.Singleton 
                            //to ensure that it is opened only once.
                            singleton = instanceContext;
                        }
                    }
                }
                return singleton;
            }
        }

        #region IInstanceContextProvider Members

        public override InstanceContext GetExistingInstanceContext(Message message, IContextChannel channel)
        {
            ServiceChannel serviceChannel = GetServiceChannelFromProxy(channel);
            if (serviceChannel != null && serviceChannel.HasSession)
            {
                SingletonInstance.BindIncomingChannel(serviceChannel);
            }
            return SingletonInstance;
        }

        public override void InitializeInstanceContext(InstanceContext instanceContext, Message message, IContextChannel channel)
        {
            //no-op
        }

        public override bool IsIdle(InstanceContext instanceContext)
        {
            //By default return false
            return false;
        }

        public override void NotifyIdle(Action<InstanceContext> callback, InstanceContext instanceContext)
        {
            //no-op
        }

        #endregion
    }

}