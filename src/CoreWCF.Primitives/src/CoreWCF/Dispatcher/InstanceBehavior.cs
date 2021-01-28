// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    internal class InstanceBehavior
    {
        private const BindingFlags DefaultBindingFlags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
        private readonly IInstanceContextInitializer[] _initializers;
        private readonly IInstanceProvider _provider;
        private readonly InstanceContext _singleton;
        private readonly bool _isSynchronized;
        private readonly ImmutableDispatchRuntime _immutableRuntime;

        internal InstanceBehavior(DispatchRuntime dispatch, ImmutableDispatchRuntime immutableRuntime)
        {
            _immutableRuntime = immutableRuntime;
            _initializers = EmptyArray<IInstanceContextInitializer>.ToArray(dispatch.InstanceContextInitializers);
            _provider = dispatch.InstanceProvider;
            _singleton = dispatch.SingletonInstanceContext;
            _isSynchronized = (dispatch.ConcurrencyMode != ConcurrencyMode.Multiple);
            InstanceContextProvider = dispatch.InstanceContextProvider;

            if (_provider == null)
            {
                ConstructorInfo constructor = null;
                if (dispatch.Type != null)
                {
                    constructor = InstanceBehavior.GetConstructor(dispatch.Type);
                }

                if (_singleton == null)
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
                    if (_singleton == null || !_singleton.IsWellKnown)
                    {
                        InvokerUtil util = new InvokerUtil();
                        CreateInstanceDelegate creator = util.GenerateCreateInstanceDelegate(dispatch.Type, constructor);
                        _provider = new InstanceProvider(creator);
                    }
                }
            }

            if (_singleton != null)
            {
                _singleton.Behavior = this;
            }
        }

        internal IInstanceContextProvider InstanceContextProvider { get; }

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
                    context.UnbindRpc(rpc);
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
            if (InstanceContextProviderBase.IsProviderSingleton(InstanceContextProvider))
            {
                return false;
            }

            if (InstanceContextProviderBase.IsProviderPerCall(InstanceContextProvider) ||
                InstanceContextProviderBase.IsProviderSessionful(InstanceContextProvider))
            {
                return true;
            }

            //User provided InstanceContextProvider. Call the provider to check for idle.
            if (!InstanceContextProvider.IsIdle(instanceContext))
            {
                InstanceContextProvider.NotifyIdle(InstanceContext.NotifyIdleCallback, instanceContext);
                return false;
            }
            return true;
        }

        internal async Task EnsureInstanceContextAsync(MessageRpc rpc)
        {
            if (rpc.InstanceContext == null)
            {
                rpc.InstanceContext = new InstanceContext(rpc.Host, false);
                rpc.InstanceContext.ServiceThrottle = rpc.channelHandler.InstanceContextServiceThrottle;
                rpc.MessageRpcOwnsInstanceContextThrottle = false;
            }

            rpc.OperationContext.SetInstanceContext(rpc.InstanceContext);
            rpc.InstanceContext.Behavior = this;

            if (rpc.InstanceContext.State == CommunicationState.Created)
            {
                Task openTask = null;
                lock (rpc.InstanceContext.ThisLock)
                {
                    if (rpc.InstanceContext.State == CommunicationState.Created)
                    {
                        var helper = new TimeoutHelper(rpc.Channel.CloseTimeout);
                        // awaiting the task outside the lock is safe as OpenAsync will transition the state away from Created before
                        // it returns an uncompleted Task.
                        openTask = rpc.InstanceContext.OpenAsync(helper.GetCancellationToken());
                        Fx.Assert(rpc.InstanceContext.State != CommunicationState.Created, "InstanceContext.OpenAsync should transition away from Created before returning a Task");
                    }
                }

                await openTask;
            }

            rpc.InstanceContext.BindRpc(rpc);
        }

        private static ConstructorInfo GetConstructor(Type type)
        {
            foreach (var constructor in type.GetConstructors(DefaultBindingFlags))
            {
                if (constructor.GetParameters().Length == 0)
                {
                    return constructor;
                }
            }
            return null;
        }

        internal object GetInstance(InstanceContext instanceContext)
        {
            if (_provider == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxNoDefaultConstructor));
            }

            return _provider.GetInstance(instanceContext);
        }

        internal object GetInstance(InstanceContext instanceContext, Message request)
        {
            if (_provider == null)
            {
                throw TraceUtility.ThrowHelperError(new InvalidOperationException(SR.SFxNoDefaultConstructor), request);
            }

            return _provider.GetInstance(instanceContext, request);
        }

        internal void Initialize(InstanceContext instanceContext)
        {
            OperationContext current = OperationContext.Current;
            Message message = (current != null) ? current.IncomingMessage : null;

            if (current != null && current.InternalServiceChannel != null)
            {
                IContextChannel transparentProxy = (IContextChannel)current.InternalServiceChannel.Proxy;
                InstanceContextProvider.InitializeInstanceContext(instanceContext, message, transparentProxy);
            }

            for (int i = 0; i < _initializers.Length; i++)
            {
                _initializers[i].Initialize(instanceContext, message);
            }
        }

        internal void EnsureServiceInstance(MessageRpc rpc)
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
            if (_provider != null)
            {
                try
                {
                    _provider.ReleaseInstance(instanceContext, instance);
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }
                    _immutableRuntime.ErrorBehavior.HandleError(e);
                }
            }
        }
    }

    internal class InstanceProvider : IInstanceProvider
    {
        private readonly CreateInstanceDelegate _creator;

        internal InstanceProvider(CreateInstanceDelegate creator)
        {
            if (creator == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(creator));
            }

            _creator = creator;
        }

        public object GetInstance(InstanceContext instanceContext)
        {
            return _creator();
        }

        public object GetInstance(InstanceContext instanceContext, Message message)
        {
            return _creator();
        }

        public void ReleaseInstance(InstanceContext instanceContext, object instance)
        {
            IDisposable dispose = instance as IDisposable;
            if (dispose != null)
            {
                dispose.Dispose();
            }
        }
    }
}