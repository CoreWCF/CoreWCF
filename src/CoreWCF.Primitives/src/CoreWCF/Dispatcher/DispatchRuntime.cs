// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Collections.Generic;
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    public sealed class DispatchRuntime
    {
        private ServiceAuthenticationManager _serviceAuthenticationManager;
        private ServiceAuthorizationManager _serviceAuthorizationManager;
        private ReadOnlyCollection<IAuthorizationPolicy> _externalAuthorizationPolicies;

        //AuditLogLocation securityAuditLogLocation;
        private ConcurrencyMode _concurrencyMode;
        private bool _ensureOrderedDispatch;

        //bool suppressAuditFailure;
        //AuditLevel serviceAuthorizationAuditLevel;
        //AuditLevel messageAuthenticationAuditLevel;
        private bool _automaticInputSessionShutdown;
        private readonly ChannelDispatcher _channelDispatcher;
        private IInstanceProvider _instanceProvider;
        private IInstanceContextProvider _instanceContextProvider;
        private InstanceContext _singleton;
        private bool _ignoreTransactionMessageProperty;
        private readonly OperationCollection _operations;
        private IDispatchOperationSelector _operationSelector;
        private ImmutableDispatchRuntime _runtime;
        private bool _isExternalPoliciesSet;
        private bool _isAuthorizationManagerSet;
        private SynchronizationContext _synchronizationContext;
        private PrincipalPermissionMode _principalPermissionMode;

        //object roleProvider;
        private Type _type;
        private DispatchOperation _unhandled;
        private bool _impersonateCallerForAllOperations;
        private bool _impersonateOnSerializingReply;
        private readonly SharedRuntimeState _shared;
        private bool _requireClaimsPrincipalOnOperationContext;

        internal DispatchRuntime(EndpointDispatcher endpointDispatcher)
            : this(new SharedRuntimeState(true))
        {
            EndpointDispatcher = endpointDispatcher ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(endpointDispatcher));
            Fx.Assert(_shared.IsOnServer, "Server constructor called on client?");
        }

        internal DispatchRuntime(ClientRuntime proxyRuntime, SharedRuntimeState shared)
            : this(shared)
        {
            ClientRuntime = proxyRuntime ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(proxyRuntime));
            _instanceProvider = new CallbackInstanceProvider();
            _channelDispatcher = new ChannelDispatcher(shared);
            _instanceContextProvider = InstanceContextProviderBase.GetProviderForMode(InstanceContextMode.PerSession, this);

            Fx.Assert(!shared.IsOnServer, "Client constructor called on server?");
        }

        private DispatchRuntime(SharedRuntimeState shared)
        {
            _shared = shared;

            _operations = new OperationCollection(this);

            InputSessionShutdownHandlers = NewBehaviorCollection<IInputSessionShutdown>();
            MessageInspectors = NewBehaviorCollection<IDispatchMessageInspector>();
            InstanceContextInitializers = NewBehaviorCollection<IInstanceContextInitializer>();
            _synchronizationContext = ThreadBehavior.GetCurrentSynchronizationContext();
            _automaticInputSessionShutdown = true;
            _principalPermissionMode = ServiceAuthorizationBehavior.DefaultPrincipalPermissionMode;
            _unhandled = new DispatchOperation(this, "*", MessageHeaders.WildcardAction, MessageHeaders.WildcardAction)
            {
                InternalFormatter = MessageOperationFormatter.Instance,
                InternalInvoker = new UnhandledActionInvoker(this)
            };
        }

        public IInstanceContextProvider InstanceContextProvider
        {
            get
            {
                return _instanceContextProvider;
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
                    _instanceContextProvider = value;
                }
            }
        }

        public InstanceContext SingletonInstanceContext
        {
            get { return _singleton; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                lock (ThisLock)
                {
                    InvalidateRuntime();
                    _singleton = value;
                }
            }
        }

        public ConcurrencyMode ConcurrencyMode
        {
            get
            {
                return _concurrencyMode;
            }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    _concurrencyMode = value;
                }
            }
        }

        public bool EnsureOrderedDispatch
        {
            get
            {
                return _ensureOrderedDispatch;
            }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    _ensureOrderedDispatch = value;
                }
            }
        }

        internal ReadOnlyCollection<IAuthorizationPolicy> ExternalAuthorizationPolicies
        {
            get
            {
                return _externalAuthorizationPolicies;
            }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    _externalAuthorizationPolicies = value;
                    _isExternalPoliciesSet = true;
                }
            }
        }

        public ServiceAuthenticationManager ServiceAuthenticationManager
        {
            get
            {
                return _serviceAuthenticationManager;
            }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    _serviceAuthenticationManager = value;
                    RequiresAuthentication = true;
                }
            }
        }

        public ServiceAuthorizationManager ServiceAuthorizationManager
        {
            get
            {
                return _serviceAuthorizationManager;
            }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    _serviceAuthorizationManager = value;
                    _isAuthorizationManagerSet = true;
                }
            }
        }

        public bool AutomaticInputSessionShutdown
        {
            get { return _automaticInputSessionShutdown; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    _automaticInputSessionShutdown = value;
                }
            }
        }

        internal ChannelDispatcher ChannelDispatcher
        {
            get { return _channelDispatcher ?? EndpointDispatcher.ChannelDispatcher; }
        }

        public ClientRuntime CallbackClientRuntime
        {
            get
            {
                if (ClientRuntime == null)
                {
                    lock (ThisLock)
                    {
                        if (ClientRuntime == null)
                        {
                            ClientRuntime = new ClientRuntime(this, _shared);
                        }
                    }
                }

                return ClientRuntime;
            }
        }

        public EndpointDispatcher EndpointDispatcher { get; }

        public bool ImpersonateCallerForAllOperations
        {
            get
            {
                return _impersonateCallerForAllOperations;
            }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    _impersonateCallerForAllOperations = value;
                }
            }
        }

        public bool ImpersonateOnSerializingReply
        {
            get
            {
                return _impersonateOnSerializingReply;
            }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    _impersonateOnSerializingReply = value;
                }
            }
        }

        internal bool RequireClaimsPrincipalOnOperationContext
        {
            get
            {
                return _requireClaimsPrincipalOnOperationContext;
            }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    _requireClaimsPrincipalOnOperationContext = value;
                }
            }
        }

        internal SynchronizedCollection<IInputSessionShutdown> InputSessionShutdownHandlers { get; }

        public bool IgnoreTransactionMessageProperty
        {
            get { return _ignoreTransactionMessageProperty; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    _ignoreTransactionMessageProperty = value;
                }
            }
        }

        public IInstanceProvider InstanceProvider
        {
            get { return _instanceProvider; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    _instanceProvider = value;
                }
            }
        }

        public SynchronizedCollection<IDispatchMessageInspector> MessageInspectors { get; }

        public SynchronizedKeyedCollection<string, DispatchOperation> Operations
        {
            get { return _operations; }
        }

        internal IDispatchOperationSelector OperationSelector
        {
            get { return _operationSelector; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    _operationSelector = value;
                }
            }
        }

        internal SynchronizedCollection<IInstanceContextInitializer> InstanceContextInitializers { get; }

        public SynchronizationContext SynchronizationContext
        {
            get { return _synchronizationContext; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    _synchronizationContext = value;
                }
            }
        }

        public PrincipalPermissionMode PrincipalPermissionMode
        {
            get
            {
                return _principalPermissionMode;
            }
            set
            {
                if (!PrincipalPermissionModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                lock (ThisLock)
                {
                    InvalidateRuntime();
                    _principalPermissionMode = value;
                }
            }
        }

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
            get { return _type; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    _type = value;
                }
            }
        }

        public DispatchOperation UnhandledDispatchOperation
        {
            get { return _unhandled; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                lock (ThisLock)
                {
                    InvalidateRuntime();
                    _unhandled = value;
                }
            }
        }

        public bool ValidateMustUnderstand
        {
            get { return _shared.ValidateMustUnderstand; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    _shared.ValidateMustUnderstand = value;
                }
            }
        }

        internal bool RequiresAuthentication { get; private set; }

        internal bool RequiresAuthorization
        {
            get
            {
                return (_isAuthorizationManagerSet || _isExternalPoliciesSet);
            }
        }

        internal bool HasMatchAllOperation
        {
            get
            {
                lock (ThisLock)
                {
                    return !(_unhandled.Invoker is UnhandledActionInvoker);
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
                    return _shared.EnableFaults;
                }
            }
        }

        internal bool IsOnServer
        {
            get { return _shared.IsOnServer; }
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
                    return _shared.ManualAddressing;
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

                    for (int i = 0; i < _operations.Count; i++)
                    {
                        max = Math.Max(max, _operations[i].CallContextInitializers.Count);
                    }
                    max = Math.Max(max, _unhandled.CallContextInitializers.Count);
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

                    for (int i = 0; i < _operations.Count; i++)
                    {
                        max = Math.Max(max, _operations[i].ParameterInspectors.Count);
                    }
                    max = Math.Max(max, _unhandled.ParameterInspectors.Count);
                    return max;
                }
            }
        }

        // Internal access to CallbackClientRuntime, but this one doesn't create on demand
        internal ClientRuntime ClientRuntime { get; private set; }

        internal object ThisLock
        {
            get { return _shared; }
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
            ImmutableDispatchRuntime runtime = _runtime;
            if (runtime != null)
            {
                return runtime;
            }
            else
            {
                return GetRuntimeCore();
            }
        }

        private ImmutableDispatchRuntime GetRuntimeCore()
        {
            lock (ThisLock)
            {
                if (_runtime == null)
                {
                    _runtime = new ImmutableDispatchRuntime(this);
                }

                return _runtime;
            }
        }

        internal void InvalidateRuntime()
        {
            lock (ThisLock)
            {
                _shared.ThrowIfImmutable();
                _runtime = null;
            }
        }

        internal void LockDownProperties()
        {
            _shared.LockDownProperties();
            if (_concurrencyMode != ConcurrencyMode.Single && _ensureOrderedDispatch)
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
            private readonly DispatchRuntime _dispatchRuntime;

            public UnhandledActionInvoker(DispatchRuntime dispatchRuntime)
            {
                _dispatchRuntime = dispatchRuntime;
            }

            public object[] AllocateInputs()
            {
                return new object[1];
            }

            public ValueTask<(object returnValue, object[] outputs)> InvokeAsync(object instance, object[] inputs)
            {
                object[] outputs = EmptyArray<object>.Allocate(0);

                if (!(inputs[0] is Message message))
                {
                    return new ValueTask<(object returnValue, object[] outputs)>(((object)null, outputs));
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
                        ChannelDispatcher channelDispatcher = _dispatchRuntime.ChannelDispatcher;
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

                if (_dispatchRuntime._shared.EnableFaults)
                {
                    MessageFault fault = MessageFault.CreateFault(code, reason, action);
                    return new ValueTask<(object returnValue, object[] outputs)>(
                        (Message.CreateMessage(message.Version, fault, message.Version.Addressing.DefaultFaultAction),
                         outputs));
                }
                else
                {
                    OperationContext.Current.RequestContext.CloseAsync().GetAwaiter().GetResult();
                    OperationContext.Current.RequestContext = null;
                    return new ValueTask<(object returnValue, object[] outputs)>((null, outputs));
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

        private class DispatchBehaviorCollection<T> : SynchronizedCollection<T>
        {
            private readonly DispatchRuntime _outer;

            internal DispatchBehaviorCollection(DispatchRuntime outer)
                : base(outer.ThisLock)
            {
                _outer = outer;
            }

            protected override void ClearItems()
            {
                _outer.InvalidateRuntime();
                base.ClearItems();
            }

            protected override void InsertItem(int index, T item)
            {
                if (item == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
                }

                _outer.InvalidateRuntime();
                base.InsertItem(index, item);
            }

            protected override void RemoveItem(int index)
            {
                _outer.InvalidateRuntime();
                base.RemoveItem(index);
            }

            protected override void SetItem(int index, T item)
            {
                if (item == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
                }

                _outer.InvalidateRuntime();
                base.SetItem(index, item);
            }
        }

        private class OperationCollection : SynchronizedKeyedCollection<string, DispatchOperation>
        {
            private readonly DispatchRuntime _outer;

            internal OperationCollection(DispatchRuntime outer)
                : base(outer.ThisLock)
            {
                _outer = outer;
            }

            protected override void ClearItems()
            {
                _outer.InvalidateRuntime();
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
                if (item.Parent != _outer)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.SFxMismatchedOperationParent);
                }

                _outer.InvalidateRuntime();
                base.InsertItem(index, item);
            }

            protected override void RemoveItem(int index)
            {
                _outer.InvalidateRuntime();
                base.RemoveItem(index);
            }

            protected override void SetItem(int index, DispatchOperation item)
            {
                if (item == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
                }
                if (item.Parent != _outer)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.SFxMismatchedOperationParent);
                }

                _outer.InvalidateRuntime();
                base.SetItem(index, item);
            }
        }

        private class CallbackInstanceProvider : IInstanceProvider
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
