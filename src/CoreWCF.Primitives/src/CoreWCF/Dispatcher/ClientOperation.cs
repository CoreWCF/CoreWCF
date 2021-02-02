// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using CoreWCF.Collections.Generic;

namespace CoreWCF.Dispatcher
{
    public sealed class ClientOperation
    {
        private bool _serializeRequest;
        private bool _deserializeReply;
        private IClientFaultFormatter _faultFormatter;
        private bool _isInitiating = true;
        private bool _isOneWay;
        private bool _isTerminating;
        private bool _isSessionOpenNotificationEnabled;
        private MethodInfo _beginMethod;
        private MethodInfo _endMethod;
        private MethodInfo _syncMethod;
        private MethodInfo _taskMethod;
        private Type _taskTResult;

        public ClientOperation(ClientRuntime parent, string name, string action)
            : this(parent, name, action, null)
        {
        }

        public ClientOperation(ClientRuntime parent, string name, string action, string replyAction)
        {
            Parent = parent ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parent));
            Name = name ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            Action = action;
            ReplyAction = replyAction;

            FaultContractInfos = parent.NewBehaviorCollection<FaultContractInfo>();
            ParameterInspectors = parent.NewBehaviorCollection<IParameterInspector>();
        }

        public string Action { get; }

        internal SynchronizedCollection<FaultContractInfo> FaultContractInfos { get; }

        public MethodInfo BeginMethod
        {
            get { return _beginMethod; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _beginMethod = value;
                }
            }
        }

        public MethodInfo EndMethod
        {
            get { return _endMethod; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _endMethod = value;
                }
            }
        }

        public MethodInfo SyncMethod
        {
            get { return _syncMethod; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _syncMethod = value;
                }
            }
        }

        public IClientMessageFormatter Formatter
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

        internal IClientFaultFormatter FaultFormatter
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

        internal IClientMessageFormatter InternalFormatter { get; set; }

        public bool IsInitiating
        {
            get { return _isInitiating; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _isInitiating = value;
                }
            }
        }

        public bool IsOneWay
        {
            get { return _isOneWay; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _isOneWay = value;
                }
            }
        }

        public bool IsTerminating
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

        public string Name { get; }

        public ICollection<IParameterInspector> ClientParameterInspectors
        {
            get { return ParameterInspectors; }
        }

        internal SynchronizedCollection<IParameterInspector> ParameterInspectors { get; }

        public ClientRuntime Parent { get; }

        public string ReplyAction { get; }

        public bool SerializeRequest
        {
            get { return _serializeRequest; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _serializeRequest = value;
                }
            }
        }

        public bool DeserializeReply
        {
            get { return _deserializeReply; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _deserializeReply = value;
                }
            }
        }

        public MethodInfo TaskMethod
        {
            get { return _taskMethod; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _taskMethod = value;
                }
            }
        }

        public Type TaskTResult
        {
            get { return _taskTResult; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    _taskTResult = value;
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
    }
}