// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CoreWCF.Collections.Generic;
using CoreWCF.IdentityModel.Claims;
using Microsoft.AspNetCore.Authorization;

namespace CoreWCF.Dispatcher
{
    public sealed class DispatchOperation
    {
        private IDispatchFaultFormatter _faultFormatter;
        private ImpersonationOption _impersonation;
        private bool _isTerminating;
        private bool _isSessionOpenNotificationEnabled;
        private bool _releaseInstanceAfterCall;
        private bool _releaseInstanceBeforeCall;
        private bool _deserializeRequest = true;
        private bool _serializeReply = true;
        private bool _autoDisposeParameters = true;
        private ConcurrentDictionary<string, List<Claim>> _authorizeClaims;

        public DispatchOperation(DispatchRuntime parent, string name, string action)
        {
            Parent = parent ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parent));
            Name = name ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            Action = action;
            _impersonation = OperationBehaviorAttribute.DefaultImpersonationOption;
            // Not necessary for basic functionality
            CallContextInitializers = parent.NewBehaviorCollection<ICallContextInitializer>();
            FaultContractInfos = parent.NewBehaviorCollection<FaultContractInfo>();
            ParameterInspectors = parent.NewBehaviorCollection<IParameterInspector>();
            IsOneWay = true;
            _authorizeClaims = new ConcurrentDictionary<string, List<Claim>>();
        }

        public DispatchOperation(DispatchRuntime parent, string name, string action, string replyAction) : this(parent, name, action)
        {
            ReplyAction = replyAction;
            IsOneWay = false;
        }

        public string Action { get; }

        public SynchronizedCollection<ICallContextInitializer> CallContextInitializers { get; }

        public SynchronizedCollection<FaultContractInfo> FaultContractInfos { get; }

        public IDispatchMessageFormatter Formatter
        {
            get { return InternalFormatter; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    InternalFormatter = value;
                }
            }
        }

        public IDispatchFaultFormatter FaultFormatter
        {
            get
            {
                if (_faultFormatter == null)
                {
                    _faultFormatter = new DataContractSerializerFaultFormatter(FaultContractInfos);
                }
                return _faultFormatter;
            }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _faultFormatter = value;
                    IsFaultFormatterSetExplicit = true;
                }
            }
        }

        internal bool IsFaultFormatterSetExplicit { get; private set; } = false;

        public bool AutoDisposeParameters
        {
            get { return _autoDisposeParameters; }

            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _autoDisposeParameters = value;
                }
            }
        }

        public bool DeserializeRequest
        {
            get { return _deserializeRequest; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _deserializeRequest = value;
                }
            }
        }

        public bool IsOneWay { get; }

        public ImpersonationOption Impersonation
        {
            get { return _impersonation; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _impersonation = value;
                }
            }
        }

        public ConcurrentDictionary<string,List<Claim>> AuthorizeClaims //need help with naming
        {
            get { return _authorizeClaims; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _authorizeClaims = value;
                }
            }
        }

        internal bool HasNoDisposableParameters { get; set; }

        internal IDispatchMessageFormatter InternalFormatter { get; set; }

        internal IOperationInvoker InternalInvoker { get; set; }

        public IOperationInvoker Invoker
        {
            get { return InternalInvoker; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    InternalInvoker = value;
                }
            }
        }

        internal bool IsTerminating
        {
            get { return _isTerminating; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _isTerminating = value;
                }
            }
        }

        internal bool IsSessionOpenNotificationEnabled
        {
            get { return _isSessionOpenNotificationEnabled; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _isSessionOpenNotificationEnabled = value;
                }
            }
        }

        public string Name { get; }

        public ICollection<IParameterInspector> ParameterInspectors { get; }

        public DispatchRuntime Parent { get; }

        internal Lazy<AuthorizationPolicy> AuthorizationPolicy { get; set; }

        internal ReceiveContextAcknowledgementMode ReceiveContextAcknowledgementMode { get; set; }

        internal bool ReleaseInstanceAfterCall
        {
            get { return _releaseInstanceAfterCall; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _releaseInstanceAfterCall = value;
                }
            }
        }

        internal bool ReleaseInstanceBeforeCall
        {
            get { return _releaseInstanceBeforeCall; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _releaseInstanceBeforeCall = value;
                }
            }
        }

        public string ReplyAction { get; }

        public bool SerializeReply
        {
            get { return _serializeReply; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _serializeReply = value;
                }
            }
        }
    }
}
