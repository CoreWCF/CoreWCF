using System;
using System.Reflection;
using CoreWCF.Runtime;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;

namespace CoreWCF.Dispatcher
{
    internal class InstanceBehavior
    {
        const BindingFlags DefaultBindingFlags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

        IInstanceContextInitializer[] initializers;
        IInstanceContextProvider instanceContextProvider;
        IInstanceProvider provider;
        InstanceContext singleton;
        //bool transactionAutoCompleteOnSessionClose;
        //bool releaseServiceInstanceOnTransactionComplete = true;
        bool isSynchronized;
        ImmutableDispatchRuntime immutableRuntime;

        internal InstanceBehavior(DispatchRuntime dispatch, ImmutableDispatchRuntime immutableRuntime)
        {
            this.immutableRuntime = immutableRuntime;
            initializers = EmptyArray<IInstanceContextInitializer>.ToArray(dispatch.InstanceContextInitializers);
            provider = dispatch.InstanceProvider;
            singleton = dispatch.SingletonInstanceContext;
        //    this.transactionAutoCompleteOnSessionClose = dispatch.TransactionAutoCompleteOnSessionClose;
        //    this.releaseServiceInstanceOnTransactionComplete = dispatch.ReleaseServiceInstanceOnTransactionComplete;
            isSynchronized = (dispatch.ConcurrencyMode != ConcurrencyMode.Multiple);
            instanceContextProvider = dispatch.InstanceContextProvider;

            if (provider == null)
            {
                ConstructorInfo constructor = null;
                if (dispatch.Type != null)
                {
                    constructor = InstanceBehavior.GetConstructor(dispatch.Type);
                }

                if (singleton == null)
                {
                    if (dispatch.Type != null && (dispatch.Type.GetTypeInfo().IsAbstract || dispatch.Type.GetTypeInfo().IsInterface))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxServiceTypeNotCreatable));
                    }

                    if (constructor == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxNoDefaultConstructor));
                    }
                }

                if (constructor != null)
                {
                    if (singleton == null || !singleton.IsWellKnown)
                    {
                        InvokerUtil util = new InvokerUtil();
                        CreateInstanceDelegate creator = util.GenerateCreateInstanceDelegate(dispatch.Type, constructor);
                        provider = new InstanceProvider(creator);
                    }
                }
            }

            if (singleton != null)
            {
                singleton.Behavior = this;
            }
        }

        //internal bool TransactionAutoCompleteOnSessionClose
        //{
        //    get
        //    {
        //        return this.transactionAutoCompleteOnSessionClose;
        //    }
        //}

        //internal bool ReleaseServiceInstanceOnTransactionComplete
        //{
        //    get
        //    {
        //        return this.releaseServiceInstanceOnTransactionComplete;
        //    }
        //}

        internal IInstanceContextProvider InstanceContextProvider
        {
            get
            {
                return instanceContextProvider;
            }
        }

        internal void AfterReply(ref MessageRpc rpc, ErrorBehavior error)
        {
            InstanceContext context = rpc.InstanceContext;

            if (context != null)
            {
                try
                {
                    if (rpc.Operation.ReleaseInstanceAfterCall)
                    {
                        if (context.State == CommunicationState.Opened)
                        {
                            context.ReleaseServiceInstance();
                        }
                    }
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }
                    error.HandleError(e);
                }

                try
                {
                    context.UnbindRpc(ref rpc);
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }
                    error.HandleError(e);
                }
            }
        }

        internal bool CanUnload(InstanceContext instanceContext)
        {
            if (InstanceContextProviderBase.IsProviderSingleton(instanceContextProvider))
                return false;

            if (InstanceContextProviderBase.IsProviderPerCall(instanceContextProvider) ||
                InstanceContextProviderBase.IsProviderSessionful(instanceContextProvider))
                return true;

            //User provided InstanceContextProvider. Call the provider to check for idle.
            if (!instanceContextProvider.IsIdle(instanceContext))
            {
                instanceContextProvider.NotifyIdle(InstanceContext.NotifyIdleCallback, instanceContext);
                return false;
            }
            return true;
        }

        internal void EnsureInstanceContext(ref MessageRpc rpc)
        {
            if (rpc.InstanceContext == null)
            {
                rpc.InstanceContext = new InstanceContext(rpc.Host, false);
            }

            rpc.OperationContext.SetInstanceContext(rpc.InstanceContext);
            rpc.InstanceContext.Behavior = this;

            if (rpc.InstanceContext.State == CommunicationState.Created)
            {
                lock (rpc.InstanceContext.ThisLock)
                {
                    if (rpc.InstanceContext.State == CommunicationState.Created)
                    {
                        var helper = new TimeoutHelper(rpc.Channel.CloseTimeout);
                        rpc.InstanceContext.OpenAsync(helper.GetCancellationToken()).GetAwaiter().GetResult();
                    }
                }
            }
            rpc.InstanceContext.BindRpc(ref rpc);
        }

        static ConstructorInfo GetConstructor(Type type)
        {
            foreach (var constructor in type.GetConstructors(DefaultBindingFlags))
            {
                if (constructor.GetParameters().Length == 0)
                    return constructor;
            }
            return null;
        }

        internal object GetInstance(InstanceContext instanceContext)
        {
            if (provider == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxNoDefaultConstructor));
            }

            return provider.GetInstance(instanceContext);
        }

        internal object GetInstance(InstanceContext instanceContext, Message request)
        {
            if (provider == null)
            {
                throw TraceUtility.ThrowHelperError(new InvalidOperationException(SR.SFxNoDefaultConstructor), request);
            }

            return provider.GetInstance(instanceContext, request);
        }

        internal void Initialize(InstanceContext instanceContext)
        {
            OperationContext current = OperationContext.Current;
            Message message = (current != null) ? current.IncomingMessage : null;

            if (current != null && current.InternalServiceChannel != null)
            {
                IContextChannel transparentProxy = (IContextChannel)current.InternalServiceChannel.Proxy;
                instanceContextProvider.InitializeInstanceContext(instanceContext, message, transparentProxy);
            }

            for (int i = 0; i < initializers.Length; i++)
                initializers[i].Initialize(instanceContext, message);
        }

        internal void EnsureServiceInstance(ref MessageRpc rpc)
        {
            if (rpc.Operation.ReleaseInstanceBeforeCall)
            {
                rpc.InstanceContext.ReleaseServiceInstance();
            }

            //if (TD.GetServiceInstanceStartIsEnabled())
            //{
            //    TD.GetServiceInstanceStart(rpc.EventTraceActivity);
            //}

            rpc.Instance = rpc.InstanceContext.GetServiceInstance(rpc.Request);

            //if (TD.GetServiceInstanceStopIsEnabled())
            //{
            //    TD.GetServiceInstanceStop(rpc.EventTraceActivity);
            //}
        }

        internal void ReleaseInstance(InstanceContext instanceContext, object instance)
        {
            if (provider != null)
            {
                try
                {
                    provider.ReleaseInstance(instanceContext, instance);
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }
                    immutableRuntime.ErrorBehavior.HandleError(e);
                }
            }
        }
    }

    class InstanceProvider : IInstanceProvider
    {
        CreateInstanceDelegate creator;

        internal InstanceProvider(CreateInstanceDelegate creator)
        {
            if (creator == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(creator));

            this.creator = creator;
        }

        public object GetInstance(InstanceContext instanceContext)
        {
            return creator();
        }

        public object GetInstance(InstanceContext instanceContext, Message message)
        {
            return creator();
        }

        public void ReleaseInstance(InstanceContext instanceContext, object instance)
        {
            IDisposable dispose = instance as IDisposable;
            if (dispose != null)
                dispose.Dispose();
        }
    }

}