using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using CoreWCF.Dispatcher;
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CoreWCF.Channels
{
    public class ServiceChannelProxy : DispatchProxy, ICommunicationObject, IChannel, IClientChannel, IOutputChannel, IRequestChannel, IServiceChannel, IDuplexContextChannel
    {
        private const string activityIdSlotName = "E2ETrace.ActivityID";
        private Type _proxiedType;
        private ServiceChannel _serviceChannel;
        private ImmutableClientRuntime _proxyRuntime;
        private MethodDataCache _methodDataCache;

        // ServiceChannelProxy serves 2 roles.  It is the TChannel proxy called by the client,
        // and it is also the handler of those calls that dispatches them to the appropriate service channel.
        // In .Net Remoting terms, it is conceptually the same as a RealProxy and a TransparentProxy combined.
        internal static TChannel CreateProxy<TChannel>(MessageDirection direction, ServiceChannel serviceChannel)
        {
            TChannel proxy = DispatchProxy.Create<TChannel, ServiceChannelProxy>();
            if (proxy == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.FailedToCreateTypedProxy, typeof(TChannel))));
            }

            ServiceChannelProxy channelProxy = (ServiceChannelProxy)(object)proxy;
            channelProxy._proxiedType = typeof(TChannel);
            channelProxy._serviceChannel = serviceChannel;
            channelProxy._proxyRuntime = serviceChannel.ClientRuntime.GetRuntime();
            channelProxy._methodDataCache = new MethodDataCache();
            return proxy;
        }

        //Workaround is to set the activityid in remoting call's LogicalCallContext

        // Override ToString() to reveal only the expected proxy type, not the generated one
        public override string ToString()
        {
            return _proxiedType.ToString();
        }

        private MethodData GetMethodData(MethodCall methodCall)
        {
            MethodData methodData;
            MethodBase method = methodCall.MethodBase;
            if (_methodDataCache.TryGetMethodData(method, out methodData))
            {
                return methodData;
            }

            bool canCacheMessageData;

            Type declaringType = method.DeclaringType;
            if (declaringType == typeof(object) && method == typeof(object).GetMethod("GetType"))
            {
                canCacheMessageData = true;
                methodData = new MethodData(method, MethodType.GetType);
            }
            else if (declaringType.IsAssignableFrom(_serviceChannel.GetType()))
            {
                canCacheMessageData = true;
                methodData = new MethodData(method, MethodType.Channel);
            }
            else
            {
                ProxyOperationRuntime operation = _proxyRuntime.GetOperation(method, methodCall.Args, out canCacheMessageData);

                if (operation == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.SFxMethodNotSupportedOnCallback1, method.Name)));
                }

                MethodType methodType;

                if (operation.IsTaskCall(methodCall))
                {
                    methodType = MethodType.TaskService;
                }
                else if (operation.IsSyncCall(methodCall))
                {
                    methodType = MethodType.Service;
                }
                else if (operation.IsBeginCall(methodCall))
                {
                    methodType = MethodType.BeginService;
                }
                else
                {
                    methodType = MethodType.EndService;
                }

                methodData = new MethodData(method, methodType, operation);
            }

            if (canCacheMessageData)
            {
                _methodDataCache.SetMethodData(methodData);
            }

            return methodData;
        }

        internal ServiceChannel GetServiceChannel()
        {
            return _serviceChannel;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (args == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(args));
            }

            if (targetMethod == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.InvalidTypedProxyMethodHandle, _proxiedType.Name)));
            }

            MethodCall methodCall = new MethodCall(targetMethod, args);
            MethodData methodData = GetMethodData(methodCall);

            switch (methodData.MethodType)
            {
                case MethodType.Service:
                    return InvokeService(methodCall, methodData.Operation);
                case MethodType.BeginService:
                    return InvokeBeginService(methodCall, methodData.Operation);
                case MethodType.EndService:
                    return InvokeEndService(methodCall, methodData.Operation);
                case MethodType.TaskService:
                    return InvokeTaskService(methodCall, methodData.Operation);
                case MethodType.Channel:
                    return InvokeChannel(methodCall);
                case MethodType.GetType:
                    return InvokeGetType(methodCall);
                default:
                    Fx.Assert("Invalid proxy method type");
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Invalid proxy method type")));
            }
        }

        internal static class TaskCreator
        {
            public static Task CreateTask(ServiceChannel channel, MethodCall methodCall, ProxyOperationRuntime operation)
            {
                if (operation.TaskTResult == ServiceReflector.VoidType)
                {
                    return TaskCreator.CreateTask(channel, operation, methodCall.Args);
                }
                return TaskCreator.CreateGenericTask(channel, operation, methodCall.Args);
            }

            private static Task CreateGenericTask(ServiceChannel channel, ProxyOperationRuntime operation, object[] inputParameters)
            {
                TaskCompletionSourceProxy tcsp = new TaskCompletionSourceProxy(operation.TaskTResult);
                Action<Task<object>, object> completeCallDelegate = (antecedent, obj) =>
                {
                    var tcsProxy = obj as TaskCompletionSourceProxy;
                    Contract.Assert(tcsProxy != null);
                    if (antecedent.IsFaulted) tcsProxy.TrySetException(antecedent.Exception.InnerException);
                    else if (antecedent.IsCanceled) tcsProxy.TrySetCanceled();
                    else tcsProxy.TrySetResult(antecedent.Result);
                };

                try
                {
                    channel.CallAsync(operation.Action, operation.IsOneWay, operation, inputParameters,
                    Array.Empty<object>()).ContinueWith(completeCallDelegate, tcsp);
                }
                catch (Exception e)
                {
                    tcsp.TrySetException(e);
                }

                return tcsp.Task;
            }

            private static Task CreateTask(ServiceChannel channel, ProxyOperationRuntime operation, object[] inputParameters)
            {
                TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();

                Action<Task<object>, object> completeCallDelegate = (antecedent, obj) =>
                {
                    var tcsObj = obj as TaskCompletionSource<object>;
                    Contract.Assert(tcsObj != null);
                    if (antecedent.IsFaulted) tcsObj.TrySetException(antecedent.Exception.InnerException);
                    else if (antecedent.IsCanceled) tcsObj.TrySetCanceled();
                    else tcsObj.TrySetResult(antecedent.Result);
                };


                try
                {
                    channel.CallAsync(operation.Action, operation.IsOneWay, operation, inputParameters,
                        Array.Empty<object>()).ContinueWith(completeCallDelegate, tcs);
                }
                catch (Exception e)
                {
                    tcs.TrySetException(e);
                }

                return tcs.Task;
            }
        }

        private class TaskCompletionSourceProxy
        {
            private TaskCompletionSourceInfo _tcsInfo;
            private object _tcsInstance;

            public TaskCompletionSourceProxy(Type resultType)
            {
                _tcsInfo = TaskCompletionSourceInfo.GetTaskCompletionSourceInfo(resultType);
                _tcsInstance = Activator.CreateInstance(_tcsInfo.GenericType);
            }

            public Task Task { get { return (Task)_tcsInfo.TaskProperty.GetValue(_tcsInstance); } }

            public bool TrySetResult(object result)
            {
                return (bool)_tcsInfo.TrySetResultMethod.Invoke(_tcsInstance, new object[] { result });
            }

            public bool TrySetException(Exception exception)
            {
                return (bool)_tcsInfo.TrySetExceptionMethod.Invoke(_tcsInstance, new object[] { exception });
            }

            public bool TrySetCanceled()
            {
                return (bool)_tcsInfo.TrySetCanceledMethod.Invoke(_tcsInstance, Array.Empty<object>());
            }
        }

        private class TaskCompletionSourceInfo
        {
            private static ConcurrentDictionary<Type, TaskCompletionSourceInfo> s_cache = new ConcurrentDictionary<Type, TaskCompletionSourceInfo>();

            public TaskCompletionSourceInfo(Type resultType)
            {
                ResultType = resultType;
                Type tcsType = typeof(TaskCompletionSource<>);
                GenericType = tcsType.MakeGenericType(new Type[] { resultType });
                TaskProperty = GenericType.GetTypeInfo().GetDeclaredProperty("Task");
                TrySetResultMethod = GenericType.GetTypeInfo().GetDeclaredMethod("TrySetResult");
                TrySetExceptionMethod = GenericType.GetRuntimeMethod("TrySetException", new Type[] { typeof(Exception) });
                TrySetCanceledMethod = GenericType.GetRuntimeMethod("TrySetCanceled", Array.Empty<Type>());
            }

            public Type ResultType { get; private set; }
            public Type GenericType { get; private set; }
            public PropertyInfo TaskProperty { get; private set; }
            public MethodInfo TrySetResultMethod { get; private set; }
            public MethodInfo TrySetExceptionMethod { get; set; }
            public MethodInfo TrySetCanceledMethod { get; set; }

            public static TaskCompletionSourceInfo GetTaskCompletionSourceInfo(Type resultType)
            {
                return s_cache.GetOrAdd(resultType, t => new TaskCompletionSourceInfo(t));
            }
        }

        private object InvokeTaskService(MethodCall methodCall, ProxyOperationRuntime operation)
        {
            Task task = TaskCreator.CreateTask(_serviceChannel, methodCall, operation);
            return task;
        }

        private object InvokeChannel(MethodCall methodCall)
        {
            //string activityName = null;
            //ActivityType activityType = ActivityType.Unknown;
            //if (DiagnosticUtility.ShouldUseActivity)
            //{
            //    if (ServiceModelActivity.Current == null ||
            //        ServiceModelActivity.Current.ActivityType != ActivityType.Close)
            //    {
            //        MethodData methodData = this.GetMethodData(methodCall);
            //        if (methodData.MethodBase.DeclaringType == typeof(System.ServiceModel.ICommunicationObject)
            //            && methodData.MethodBase.Name.Equals("Close", StringComparison.Ordinal))
            //        {
            //            activityName = SR.Format(SR.ActivityClose, _serviceChannel.GetType().FullName);
            //            activityType = ActivityType.Close;
            //        }
            //    }
            //}

            //using (ServiceModelActivity activity = string.IsNullOrEmpty(activityName) ? null : ServiceModelActivity.CreateBoundedActivity())
            //{
            //    if (DiagnosticUtility.ShouldUseActivity)
            //    {
            //        ServiceModelActivity.Start(activity, activityName, activityType);
            //    }
                return ExecuteMessage(_serviceChannel, methodCall);
            //}
        }

        private object InvokeGetType(MethodCall methodCall)
        {
            return _proxiedType;
        }

        private object InvokeBeginService(MethodCall methodCall, ProxyOperationRuntime operation)
        {
            AsyncCallback callback;
            object asyncState;
            object[] ins = operation.MapAsyncBeginInputs(methodCall, out callback, out asyncState);
            object ret = _serviceChannel.BeginCall(operation.Action, operation.IsOneWay, operation, ins, callback, asyncState);
            return ret;
        }

        private object InvokeEndService(MethodCall methodCall, ProxyOperationRuntime operation)
        {
            IAsyncResult result;
            object[] outs;
            operation.MapAsyncEndInputs(methodCall, out result, out outs);
            object ret = _serviceChannel.EndCall(operation.Action, outs, result);
            operation.MapAsyncOutputs(methodCall, outs, ref ret);
            return ret;
        }

        private object InvokeService(MethodCall methodCall, ProxyOperationRuntime operation)
        {
            object[] outs;
            object[] ins = operation.MapSyncInputs(methodCall, out outs);
            object ret;
            using (TaskHelpers.RunTaskContinuationsOnOurThreads())
            {
              ret = _serviceChannel.CallAsync(operation.Action, operation.IsOneWay, operation, ins, outs).GetAwaiter().GetResult();
            }
            operation.MapSyncOutputs(methodCall, outs, ref ret);
            return ret;
        }

        private object ExecuteMessage(object target, MethodCall methodCall)
        {
            MethodBase targetMethod = methodCall.MethodBase;

            object[] args = methodCall.Args;
            object returnValue = null;
            try
            {
                returnValue = targetMethod.Invoke(target, args);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }

            return returnValue;
        }

        internal class MethodDataCache
        {
            private MethodData[] _methodDatas;

            public MethodDataCache()
            {
                _methodDatas = new MethodData[4];
            }

            private object ThisLock
            {
                get { return this; }
            }

            public bool TryGetMethodData(MethodBase method, out MethodData methodData)
            {
                lock (ThisLock)
                {
                    MethodData[] methodDatas = _methodDatas;
                    int index = FindMethod(methodDatas, method);
                    if (index >= 0)
                    {
                        methodData = methodDatas[index];
                        return true;
                    }
                    else
                    {
                        methodData = new MethodData();
                        return false;
                    }
                }
            }

            private static int FindMethod(MethodData[] methodDatas, MethodBase methodToFind)
            {
                for (int i = 0; i < methodDatas.Length; i++)
                {
                    MethodBase method = methodDatas[i].MethodBase;
                    if (method == null)
                    {
                        break;
                    }
                    if (method == methodToFind)
                    {
                        return i;
                    }
                }
                return -1;
            }

            public void SetMethodData(MethodData methodData)
            {
                lock (ThisLock)
                {
                    int index = FindMethod(_methodDatas, methodData.MethodBase);
                    if (index < 0)
                    {
                        for (int i = 0; i < _methodDatas.Length; i++)
                        {
                            if (_methodDatas[i].MethodBase == null)
                            {
                                _methodDatas[i] = methodData;
                                return;
                            }
                        }
                        MethodData[] newMethodDatas = new MethodData[_methodDatas.Length * 2];
                        Array.Copy(_methodDatas, newMethodDatas, _methodDatas.Length);
                        newMethodDatas[_methodDatas.Length] = methodData;
                        _methodDatas = newMethodDatas;
                    }
                }
            }
        }

        internal enum MethodType
        {
            Service,
            BeginService,
            EndService,
            Channel,
            Object,
            GetType,
            TaskService
        }

        internal struct MethodData
        {
            private MethodBase _methodBase;
            private MethodType _methodType;
            private ProxyOperationRuntime _operation;

            public MethodData(MethodBase methodBase, MethodType methodType)
                : this(methodBase, methodType, null)
            {
            }

            public MethodData(MethodBase methodBase, MethodType methodType, ProxyOperationRuntime operation)
            {
                _methodBase = methodBase;
                _methodType = methodType;
                _operation = operation;
            }

            public MethodBase MethodBase
            {
                get { return _methodBase; }
            }

            public MethodType MethodType
            {
                get { return _methodType; }
            }

            public ProxyOperationRuntime Operation
            {
                get { return _operation; }
            }
        }

        #region Channel interfaces
        // These channel methods exist only to implement additional channel interfaces for ServiceChannelProxy.
        // This is required because clients can down-cast typed proxies to the these channel interfaces.
        // On the desktop, the .Net Remoting layer allowed that type cast, and subsequent calls against the
        // interface went back through the RealProxy and invoked the underlying ServiceChannel.
        // Net Native and CoreClr do not have .Net Remoting and therefore cannot use that mechanism.
        // But because typed proxies derive from ServiceChannelProxy, implementing these interfaces
        // on ServiceChannelProxy permits casting the typed proxy to these interfaces.
        // All interface implementations delegate directly to the underlying ServiceChannel.
        T IChannel.GetProperty<T>()
        {
            return _serviceChannel.GetProperty<T>();
        }

        CommunicationState ICommunicationObject.State
        {
            get { return _serviceChannel.State; }
        }

        event EventHandler ICommunicationObject.Closed
        {
            add { _serviceChannel.Closed += value; }
            remove { _serviceChannel.Closed -= value; }
        }

        event EventHandler ICommunicationObject.Closing
        {
            add { _serviceChannel.Closing += value; }
            remove { _serviceChannel.Closing -= value; }
        }

        event EventHandler ICommunicationObject.Faulted
        {
            add { _serviceChannel.Faulted += value; }
            remove { _serviceChannel.Faulted -= value; }
        }

        event EventHandler ICommunicationObject.Opened
        {
            add { _serviceChannel.Opened += value; }
            remove { _serviceChannel.Opened -= value; }
        }

        event EventHandler ICommunicationObject.Opening
        {
            add { _serviceChannel.Opening += value; }
            remove { _serviceChannel.Opening -= value; }
        }

        void ICommunicationObject.Abort()
        {
            _serviceChannel.Abort();
        }

        Task ICommunicationObject.CloseAsync()
        {
            return _serviceChannel.CloseAsync();
        }

        Task ICommunicationObject.CloseAsync(CancellationToken token)
        {
            return _serviceChannel.CloseAsync(token);
        }

        Task ICommunicationObject.OpenAsync()
        {
            return _serviceChannel.OpenAsync();
        }

        Task ICommunicationObject.OpenAsync(CancellationToken token)
        {
            return _serviceChannel.OpenAsync(token);
        }

        //Uri IClientChannel.Via
        //{
        //    get { return _serviceChannel.Via; }
        //}

        event EventHandler<UnknownMessageReceivedEventArgs> IClientChannel.UnknownMessageReceived
        {
            add { ((IClientChannel)_serviceChannel).UnknownMessageReceived += value; }
            remove { ((IClientChannel)_serviceChannel).UnknownMessageReceived -= value; }
        }

        void IDisposable.Dispose()
        {
            ((IClientChannel)_serviceChannel).Dispose();
        }

        //bool IContextChannel.AllowOutputBatching
        //{
        //    get
        //    {
        //        return ((IContextChannel)_serviceChannel).AllowOutputBatching;
        //    }
        //    set
        //    {
        //        ((IContextChannel)_serviceChannel).AllowOutputBatching = value;
        //    }
        //}

        IInputSession IContextChannel.InputSession
        {
            get { return ((IContextChannel)_serviceChannel).InputSession; }
        }

        EndpointAddress IContextChannel.LocalAddress
        {
            get { return ((IContextChannel)_serviceChannel).LocalAddress; }
        }

        TimeSpan IContextChannel.OperationTimeout
        {
            get
            {
                return ((IContextChannel)_serviceChannel).OperationTimeout;
            }
            set
            {
                ((IContextChannel)_serviceChannel).OperationTimeout = value;
            }
        }

        IOutputSession IContextChannel.OutputSession
        {
            get { return ((IContextChannel)_serviceChannel).OutputSession; }
        }

        EndpointAddress IOutputChannel.RemoteAddress
        {
            get { return ((IContextChannel)_serviceChannel).RemoteAddress; }
        }

        Uri IOutputChannel.Via
        {
            get { return _serviceChannel.Via; }
        }

        EndpointAddress IContextChannel.RemoteAddress
        {
            get { return ((IContextChannel)_serviceChannel).RemoteAddress; }
        }

        string IContextChannel.SessionId
        {
            get { return ((IContextChannel)_serviceChannel).SessionId; }
        }

        IExtensionCollection<IContextChannel> IExtensibleObject<IContextChannel>.Extensions
        {
            get { return ((IContextChannel)_serviceChannel).Extensions; }
        }

        Task IOutputChannel.SendAsync(Message message)
        {
            return _serviceChannel.SendAsync(message);
        }

        Task IOutputChannel.SendAsync(Message message, CancellationToken token)
        {
            return _serviceChannel.SendAsync(message, token);
        }

        Task<Message> IRequestChannel.RequestAsync(Message message)
        {
            return _serviceChannel.RequestAsync(message);
        }

        Task<Message> IRequestChannel.RequestAsync(Message message, CancellationToken token)
        {
            return _serviceChannel.RequestAsync(message, token);
        }

        public Task CloseOutputSessionAsync(CancellationToken token)
        {
            return ((IDuplexContextChannel)_serviceChannel).CloseOutputSessionAsync(token);
        }

        EndpointAddress IRequestChannel.RemoteAddress
        {
            get { return ((IContextChannel)_serviceChannel).RemoteAddress; }
        }

        Uri IRequestChannel.Via
        {
            get { return _serviceChannel.Via; }
        }

        Uri IServiceChannel.ListenUri
        {
            get { return _serviceChannel.ListenUri; }
        }

        public bool AutomaticInputSessionShutdown
        {
            get
            {
                return ((IDuplexContextChannel)_serviceChannel).AutomaticInputSessionShutdown;
            }

            set
            {
                ((IDuplexContextChannel)_serviceChannel).AutomaticInputSessionShutdown = value;
            }
        }

        public InstanceContext CallbackInstance
        {
            get
            {
                return ((IDuplexContextChannel)_serviceChannel).CallbackInstance;
            }

            set
            {
                ((IDuplexContextChannel)_serviceChannel).CallbackInstance = value;
            }
        }
        #endregion // Channel interfaces
    }

}