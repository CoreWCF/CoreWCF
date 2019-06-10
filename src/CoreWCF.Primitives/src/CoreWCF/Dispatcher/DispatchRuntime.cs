using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using CoreWCF.Collections.Generic;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Runtime;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;

namespace CoreWCF.Dispatcher
{
    public sealed class DispatchRuntime
    {
        //ServiceAuthenticationManager serviceAuthenticationManager;
        //ServiceAuthorizationManager serviceAuthorizationManager;
        //ReadOnlyCollection<IAuthorizationPolicy> externalAuthorizationPolicies;
        //AuditLogLocation securityAuditLogLocation;
        ConcurrencyMode concurrencyMode;
        bool ensureOrderedDispatch;
        //bool suppressAuditFailure;
        //AuditLevel serviceAuthorizationAuditLevel;
        //AuditLevel messageAuthenticationAuditLevel;
        bool automaticInputSessionShutdown;
        ChannelDispatcher channelDispatcher;
        SynchronizedCollection<IInputSessionShutdown> inputSessionShutdownHandlers;
        EndpointDispatcher endpointDispatcher;
        IInstanceProvider instanceProvider;
        IInstanceContextProvider instanceContextProvider;
        InstanceContext singleton;
        bool ignoreTransactionMessageProperty;
        SynchronizedCollection<IDispatchMessageInspector> messageInspectors;
        OperationCollection operations;
        IDispatchOperationSelector operationSelector;
        ClientRuntime proxyRuntime;
        ImmutableDispatchRuntime runtime;
        SynchronizedCollection<IInstanceContextInitializer> instanceContextInitializers;
        //bool isExternalPoliciesSet;
        //bool isAuthenticationManagerSet;
        //bool isAuthorizationManagerSet;
        SynchronizationContext synchronizationContext;
        //PrincipalPermissionMode principalPermissionMode;
        //object roleProvider;
        Type type;
        DispatchOperation unhandled;
        //bool transactionAutoCompleteOnSessionClose;
        //bool impersonateCallerForAllOperations;
        //bool impersonateOnSerializingReply;
        //bool releaseServiceInstanceOnTransactionComplete;
        SharedRuntimeState shared;
        bool preserveMessage;
        //bool requireClaimsPrincipalOnOperationContext;

        internal DispatchRuntime(EndpointDispatcher endpointDispatcher)
            : this(new SharedRuntimeState(true))
        {
            if (endpointDispatcher == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(endpointDispatcher));
            }

            this.endpointDispatcher = endpointDispatcher;

            Fx.Assert(shared.IsOnServer, "Server constructor called on client?");
        }

        internal DispatchRuntime(ClientRuntime proxyRuntime, SharedRuntimeState shared)
            : this(shared)
        {
            if (proxyRuntime == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(proxyRuntime));
            }

            this.proxyRuntime = proxyRuntime;
            instanceProvider = new CallbackInstanceProvider();
            channelDispatcher = new ChannelDispatcher(shared);
            instanceContextProvider = InstanceContextProviderBase.GetProviderForMode(InstanceContextMode.PerSession, this);

            Fx.Assert(!shared.IsOnServer, "Client constructor called on server?");
        }

        DispatchRuntime(SharedRuntimeState shared)
        {
            this.shared = shared;

            operations = new OperationCollection(this);

            inputSessionShutdownHandlers = NewBehaviorCollection<IInputSessionShutdown>();
            messageInspectors = NewBehaviorCollection<IDispatchMessageInspector>();
            instanceContextInitializers = NewBehaviorCollection<IInstanceContextInitializer>();
            synchronizationContext = ThreadBehavior.GetCurrentSynchronizationContext();

            automaticInputSessionShutdown = true;
            //this.principalPermissionMode = ServiceAuthorizationBehavior.DefaultPrincipalPermissionMode;

            //this.securityAuditLogLocation = ServiceSecurityAuditBehavior.defaultAuditLogLocation;
            //this.suppressAuditFailure = ServiceSecurityAuditBehavior.defaultSuppressAuditFailure;
            //this.serviceAuthorizationAuditLevel = ServiceSecurityAuditBehavior.defaultServiceAuthorizationAuditLevel;
            //this.messageAuthenticationAuditLevel = ServiceSecurityAuditBehavior.defaultMessageAuthenticationAuditLevel;

            unhandled = new DispatchOperation(this, "*", MessageHeaders.WildcardAction, MessageHeaders.WildcardAction);
            unhandled.InternalFormatter = MessageOperationFormatter.Instance;
            unhandled.InternalInvoker = new UnhandledActionInvoker(this);
        }

        internal IInstanceContextProvider InstanceContextProvider
        {
            get
            {
                return instanceContextProvider;
            }

            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                lock (ThisLock)
                {
                    InvalidateRuntime();
                    instanceContextProvider = value;
                }
            }
        }

        public InstanceContext SingletonInstanceContext
        {
            get { return singleton; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                lock (ThisLock)
                {
                    InvalidateRuntime();
                    singleton = value;
                }
            }
        }

        public ConcurrencyMode ConcurrencyMode
        {
            get
            {
                return concurrencyMode;
            }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    concurrencyMode = value;
                }
            }
        }

        public bool EnsureOrderedDispatch
        {
            get
            {
                return ensureOrderedDispatch;
            }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    ensureOrderedDispatch = value;
                }
            }
        }

        //public AuditLogLocation SecurityAuditLogLocation
        //{
        //    get
        //    {
        //        return this.securityAuditLogLocation;
        //    }
        //    set
        //    {
        //        if (!AuditLogLocationHelper.IsDefined(value))
        //        {
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
        //        }

        //        lock (this.ThisLock)
        //        {
        //            this.InvalidateRuntime();
        //            this.securityAuditLogLocation = value;
        //        }
        //    }
        //}

        //public bool SuppressAuditFailure
        //{
        //    get
        //    {
        //        return this.suppressAuditFailure;
        //    }
        //    set
        //    {
        //        lock (this.ThisLock)
        //        {
        //            this.InvalidateRuntime();
        //            this.suppressAuditFailure = value;
        //        }
        //    }
        //}

        //public AuditLevel ServiceAuthorizationAuditLevel
        //{
        //    get
        //    {
        //        return this.serviceAuthorizationAuditLevel;
        //    }
        //    set
        //    {
        //        if (!AuditLevelHelper.IsDefined(value))
        //        {
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
        //        }

        //        lock (this.ThisLock)
        //        {
        //            this.InvalidateRuntime();
        //            this.serviceAuthorizationAuditLevel = value;
        //        }
        //    }
        //}

        //public AuditLevel MessageAuthenticationAuditLevel
        //{
        //    get
        //    {
        //        return this.messageAuthenticationAuditLevel;
        //    }
        //    set
        //    {
        //        if (!AuditLevelHelper.IsDefined(value))
        //        {
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
        //        }

        //        lock (this.ThisLock)
        //        {
        //            this.InvalidateRuntime();
        //            this.messageAuthenticationAuditLevel = value;
        //        }
        //    }
        //}

        //internal ReadOnlyCollection<IAuthorizationPolicy> ExternalAuthorizationPolicies
        //{
        //    get
        //    {
        //        return this.externalAuthorizationPolicies;
        //    }
        //    set
        //    {
        //        lock (this.ThisLock)
        //        {
        //            this.InvalidateRuntime();
        //            this.externalAuthorizationPolicies = value;
        //            this.isExternalPoliciesSet = true;
        //        }
        //    }
        //}

        //public ServiceAuthenticationManager ServiceAuthenticationManager
        //{
        //    get
        //    {
        //        return this.serviceAuthenticationManager;
        //    }
        //    set
        //    {
        //        lock (this.ThisLock)
        //        {
        //            this.InvalidateRuntime();
        //            this.serviceAuthenticationManager = value;
        //            this.isAuthenticationManagerSet = true;
        //        }
        //    }
        //}

        //public ServiceAuthorizationManager ServiceAuthorizationManager
        //{
        //    get
        //    {
        //        return this.serviceAuthorizationManager;
        //    }
        //    set
        //    {
        //        lock (this.ThisLock)
        //        {
        //            this.InvalidateRuntime();
        //            this.serviceAuthorizationManager = value;
        //            this.isAuthorizationManagerSet = true;
        //        }
        //    }
        //}

        public bool AutomaticInputSessionShutdown
        {
            get { return automaticInputSessionShutdown; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    automaticInputSessionShutdown = value;
                }
            }
        }

        internal ChannelDispatcher ChannelDispatcher
        {
            get { return channelDispatcher ?? endpointDispatcher.ChannelDispatcher; }
        }

        public ClientRuntime CallbackClientRuntime
        {
            get
            {
                if (proxyRuntime == null)
                {
                    lock (ThisLock)
                    {
                        if (proxyRuntime == null)
                        {
                            proxyRuntime = new ClientRuntime(this, shared);
                        }
                    }
                }

                return proxyRuntime;
            }
        }

        public EndpointDispatcher EndpointDispatcher
        {
            get { return endpointDispatcher; }
        }

        //public bool ImpersonateCallerForAllOperations
        //{
        //    get
        //    {
        //        return this.impersonateCallerForAllOperations;
        //    }
        //    set
        //    {
        //        lock (this.ThisLock)
        //        {
        //            this.InvalidateRuntime();
        //            this.impersonateCallerForAllOperations = value;
        //        }
        //    }
        //}

        //public bool ImpersonateOnSerializingReply
        //{
        //    get
        //    {
        //        return this.impersonateOnSerializingReply;
        //    }
        //    set
        //    {
        //        lock (this.ThisLock)
        //        {
        //            this.InvalidateRuntime();
        //            this.impersonateOnSerializingReply = value;
        //        }
        //    }
        //}

        //internal bool RequireClaimsPrincipalOnOperationContext
        //{
        //    get
        //    {
        //        return this.requireClaimsPrincipalOnOperationContext;
        //    }
        //    set
        //    {
        //        lock (this.ThisLock)
        //        {
        //            this.InvalidateRuntime();
        //            this.requireClaimsPrincipalOnOperationContext = value;
        //        }
        //    }
        //}

        internal SynchronizedCollection<IInputSessionShutdown> InputSessionShutdownHandlers
        {
            get { return inputSessionShutdownHandlers; }
        }

        public bool IgnoreTransactionMessageProperty
        {
            get { return ignoreTransactionMessageProperty; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    ignoreTransactionMessageProperty = value;
                }
            }
        }

        internal IInstanceProvider InstanceProvider
        {
            get { return instanceProvider; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    instanceProvider = value;
                }
            }
        }

        public SynchronizedCollection<IDispatchMessageInspector> MessageInspectors
        {
            get { return messageInspectors; }
        }

        public SynchronizedKeyedCollection<string, DispatchOperation> Operations
        {
            get { return operations; }
        }

        internal IDispatchOperationSelector OperationSelector
        {
            get { return operationSelector; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    operationSelector = value;
                }
            }
        }

        //public bool ReleaseServiceInstanceOnTransactionComplete
        //{
        //    get { return this.releaseServiceInstanceOnTransactionComplete; }
        //    set
        //    {
        //        lock (this.ThisLock)
        //        {
        //            this.InvalidateRuntime();
        //            this.releaseServiceInstanceOnTransactionComplete = value;
        //        }
        //    }
        //}

        internal SynchronizedCollection<IInstanceContextInitializer> InstanceContextInitializers
        {
            get { return instanceContextInitializers; }
        }

        public SynchronizationContext SynchronizationContext
        {
            get { return synchronizationContext; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    synchronizationContext = value;
                }
            }
        }

        //public PrincipalPermissionMode PrincipalPermissionMode
        //{
        //    get
        //    {
        //        return this.principalPermissionMode;
        //    }
        //    set
        //    {
        //        if (!PrincipalPermissionModeHelper.IsDefined(value))
        //        {
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
        //        }

        //        lock (this.ThisLock)
        //        {
        //            this.InvalidateRuntime();
        //            this.principalPermissionMode = value;
        //        }
        //    }
        //}

        //public RoleProvider RoleProvider
        //{
        //    get { return (RoleProvider)this.roleProvider; }
        //    set
        //    {
        //        lock (this.ThisLock)
        //        {
        //            this.InvalidateRuntime();
        //            this.roleProvider = value;
        //        }
        //    }
        //}

        //public bool TransactionAutoCompleteOnSessionClose
        //{
        //    get { return this.transactionAutoCompleteOnSessionClose; }
        //    set
        //    {
        //        lock (this.ThisLock)
        //        {
        //            this.InvalidateRuntime();
        //            this.transactionAutoCompleteOnSessionClose = value;
        //        }
        //    }
        //}

        public Type Type
        {
            get { return type; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    type = value;
                }
            }
        }

        public DispatchOperation UnhandledDispatchOperation
        {
            get { return unhandled; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                lock (ThisLock)
                {
                    InvalidateRuntime();
                    unhandled = value;
                }
            }
        }

        public bool ValidateMustUnderstand
        {
            get { return shared.ValidateMustUnderstand; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    shared.ValidateMustUnderstand = value;
                }
            }
        }

        public bool PreserveMessage
        {
            get { return preserveMessage; }
            set
            {
                throw new PlatformNotSupportedException(nameof(PreserveMessage));
                //lock (ThisLock)
                //{
                //    InvalidateRuntime();
                //    preserveMessage = value;
                //}
            }
        }

        //internal bool RequiresAuthentication
        //{
        //    get
        //    {
        //        return this.isAuthenticationManagerSet;
        //    }
        //}

        //internal bool RequiresAuthorization
        //{
        //    get
        //    {
        //        return (this.isAuthorizationManagerSet || this.isExternalPoliciesSet ||
        //            AuditLevel.Success == (this.serviceAuthorizationAuditLevel & AuditLevel.Success));
        //    }
        //}

        internal bool HasMatchAllOperation
        {
            get
            {
                lock (ThisLock)
                {
                    return !(unhandled.Invoker is UnhandledActionInvoker);
                }
            }
        }

        internal bool EnableFaults
        {
            get
            {
                if (IsOnServer)
                {
                    ChannelDispatcher channelDispatcher = ChannelDispatcher;
                    return (channelDispatcher != null) && channelDispatcher.EnableFaults;
                }
                else
                {
                    return shared.EnableFaults;
                }
            }
        }

        internal bool IsOnServer
        {
            get { return shared.IsOnServer; }
        }

        internal bool ManualAddressing
        {
            get
            {
                if (IsOnServer)
                {
                    ChannelDispatcher channelDispatcher = ChannelDispatcher;
                    return (channelDispatcher != null) && channelDispatcher.ManualAddressing;
                }
                else
                {
                    return shared.ManualAddressing;
                }
            }
        }

        internal int MaxCallContextInitializers
        {
            get
            {
                lock (ThisLock)
                {
                    int max = 0;

                    for (int i = 0; i < operations.Count; i++)
                    {
                        max = System.Math.Max(max, operations[i].CallContextInitializers.Count);
                    }
                    max = System.Math.Max(max, unhandled.CallContextInitializers.Count);
                    return max;
                }
            }
        }

        internal int MaxParameterInspectors
        {
            get
            {
                lock (ThisLock)
                {
                    int max = 0;

                    for (int i = 0; i < operations.Count; i++)
                    {
                        max = System.Math.Max(max, operations[i].ParameterInspectors.Count);
                    }
                    max = System.Math.Max(max, unhandled.ParameterInspectors.Count);
                    return max;
                }
            }
        }

        // Internal access to CallbackClientRuntime, but this one doesn't create on demand
        internal ClientRuntime ClientRuntime
        {
            get { return proxyRuntime; }
        }

        internal object ThisLock
        {
            get { return shared; }
        }

        //internal bool IsRoleProviderSet
        //{
        //    get { return this.roleProvider != null; }
        //}

        internal DispatchOperationRuntime GetOperation(ref Message message)
        {
            ImmutableDispatchRuntime runtime = GetRuntime();
            return runtime.GetOperation(ref message);
        }

        internal ImmutableDispatchRuntime GetRuntime()
        {
            ImmutableDispatchRuntime runtime = this.runtime;
            if (runtime != null)
            {
                return runtime;
            }
            else
            {
                return GetRuntimeCore();
            }
        }

        ImmutableDispatchRuntime GetRuntimeCore()
        {
            lock (ThisLock)
            {
                if (runtime == null)
                {
                    runtime = new ImmutableDispatchRuntime(this);
                }

                return runtime;
            }
        }

        internal void InvalidateRuntime()
        {
            lock (ThisLock)
            {
                shared.ThrowIfImmutable();
                runtime = null;
            }
        }

        internal void LockDownProperties()
        {
            shared.LockDownProperties();
            if (concurrencyMode != ConcurrencyMode.Single && ensureOrderedDispatch)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SfxDispatchRuntimeNonConcurrentOrEnsureOrderedDispatch));
            }
        }

        internal SynchronizedCollection<T> NewBehaviorCollection<T>()
        {
            return new DispatchBehaviorCollection<T>(this);
        }

        internal class UnhandledActionInvoker : IOperationInvoker
        {
            DispatchRuntime dispatchRuntime;

            public UnhandledActionInvoker(DispatchRuntime dispatchRuntime)
            {
                this.dispatchRuntime = dispatchRuntime;
            }

            public bool IsSynchronous
            {
                get { return true; }
            }

            public object[] AllocateInputs()
            {
                return new object[1];
            }

            public object Invoke(object instance, object[] inputs, out object[] outputs)
            {
                outputs = EmptyArray<object>.Allocate(0);

                Message message = inputs[0] as Message;
                if (message == null)
                {
                    return null;
                }

                string action = message.Headers.Action;

                //if (DiagnosticUtility.ShouldTraceInformation)
                //{
                //    TraceUtility.TraceEvent(TraceEventType.Information, TraceCode.UnhandledAction,
                //        SR.TraceCodeUnhandledAction,
                //        new StringTraceRecord("Action", action),
                //        this, null, message);
                //}

                FaultCode code = FaultCode.CreateSenderFaultCode(AddressingStrings.ActionNotSupported,
                    message.Version.Addressing.Namespace);
                string reasonText = SR.Format(SR.SFxNoEndpointMatchingContract, action);
                FaultReason reason = new FaultReason(reasonText);

                FaultException exception = new FaultException(reason, code);
                ErrorBehavior.ThrowAndCatch(exception);

                ServiceChannel serviceChannel = OperationContext.Current.InternalServiceChannel;
                OperationContext.Current.OperationCompleted +=
                    delegate (object sender, EventArgs e)
                    {
                        ChannelDispatcher channelDispatcher = dispatchRuntime.ChannelDispatcher;
                        if (!channelDispatcher.HandleError(exception) && serviceChannel.HasSession)
                        {
                            try
                            {
                                var helper = new TimeoutHelper(ChannelHandler.CloseAfterFaultTimeout);
                                serviceChannel.CloseAsync(helper.GetCancellationToken()).GetAwaiter().GetResult();
                            }
                            catch (Exception ex)
                            {
                                if (Fx.IsFatal(ex))
                                {
                                    throw;
                                }
                                channelDispatcher.HandleError(ex);
                            }
                        }
                    };

                if (dispatchRuntime.shared.EnableFaults)
                {
                    MessageFault fault = MessageFault.CreateFault(code, reason, action);
                    return Message.CreateMessage(message.Version, fault, message.Version.Addressing.DefaultFaultAction);
                }
                else
                {
                    OperationContext.Current.RequestContext.CloseAsync().GetAwaiter().GetResult();
                    OperationContext.Current.RequestContext = null;
                    return null;
                }
            }

            public IAsyncResult InvokeBegin(object instance, object[] inputs, AsyncCallback callback, object state)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
            }

            public object InvokeEnd(object instance, out object[] outputs, IAsyncResult result)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
            }
        }

        class DispatchBehaviorCollection<T> : SynchronizedCollection<T>
        {
            DispatchRuntime outer;

            internal DispatchBehaviorCollection(DispatchRuntime outer)
                : base(outer.ThisLock)
            {
                this.outer = outer;
            }

            protected override void ClearItems()
            {
                outer.InvalidateRuntime();
                base.ClearItems();
            }

            protected override void InsertItem(int index, T item)
            {
                if (item == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
                }

                outer.InvalidateRuntime();
                base.InsertItem(index, item);
            }

            protected override void RemoveItem(int index)
            {
                outer.InvalidateRuntime();
                base.RemoveItem(index);
            }

            protected override void SetItem(int index, T item)
            {
                if (item == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
                }

                outer.InvalidateRuntime();
                base.SetItem(index, item);
            }
        }

        class OperationCollection : SynchronizedKeyedCollection<string, DispatchOperation>
        {
            DispatchRuntime outer;

            internal OperationCollection(DispatchRuntime outer)
                : base(outer.ThisLock)
            {
                this.outer = outer;
            }

            protected override void ClearItems()
            {
                outer.InvalidateRuntime();
                base.ClearItems();
            }

            protected override string GetKeyForItem(DispatchOperation item)
            {
                return item.Name;
            }

            protected override void InsertItem(int index, DispatchOperation item)
            {
                if (item == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
                }
                if (item.Parent != outer)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.SFxMismatchedOperationParent);
                }

                outer.InvalidateRuntime();
                base.InsertItem(index, item);
            }

            protected override void RemoveItem(int index)
            {
                outer.InvalidateRuntime();
                base.RemoveItem(index);
            }

            protected override void SetItem(int index, DispatchOperation item)
            {
                if (item == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
                }
                if (item.Parent != outer)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.SFxMismatchedOperationParent);
                }

                outer.InvalidateRuntime();
                base.SetItem(index, item);
            }
        }

        class CallbackInstanceProvider : IInstanceProvider
        {
            object IInstanceProvider.GetInstance(InstanceContext instanceContext)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxCannotActivateCallbackInstace));
            }

            object IInstanceProvider.GetInstance(InstanceContext instanceContext, Message message)
            {
                throw TraceUtility.ThrowHelperError(new InvalidOperationException(SR.SFxCannotActivateCallbackInstace), message);
            }

            void IInstanceProvider.ReleaseInstance(InstanceContext instanceContext, object instance)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxCannotActivateCallbackInstace));
            }
        }
    }
}