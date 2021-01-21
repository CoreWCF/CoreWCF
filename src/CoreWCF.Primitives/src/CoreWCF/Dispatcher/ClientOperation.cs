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
        private readonly SynchronizedCollection<FaultContractInfo> faultContractInfos;
        private bool serializeRequest;
        private bool deserializeReply;
        private IClientFaultFormatter faultFormatter;
        private bool isInitiating = true;
        private bool isOneWay;
        private bool isTerminating;
        private bool isSessionOpenNotificationEnabled;
        private readonly string replyAction;
        private MethodInfo beginMethod;
        private MethodInfo endMethod;
        private MethodInfo syncMethod;
        private MethodInfo taskMethod;
        private Type taskTResult;
        private readonly SynchronizedCollection<IParameterInspector> parameterInspectors;

        public ClientOperation(ClientRuntime parent, string name, string action)
            : this(parent, name, action, null)
        {
        }

        public ClientOperation(ClientRuntime parent, string name, string action, string replyAction)
        {
            if (parent == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parent));
            }

            if (name == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            }

            Parent = parent;
            Name = name;
            Action = action;
            this.replyAction = replyAction;

            faultContractInfos = parent.NewBehaviorCollection<FaultContractInfo>();
            parameterInspectors = parent.NewBehaviorCollection<IParameterInspector>();
        }

        public string Action { get; }

        internal SynchronizedCollection<FaultContractInfo> FaultContractInfos
        {
            get { return faultContractInfos; }
        }

        public MethodInfo BeginMethod
        {
            get { return beginMethod; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    beginMethod = value;
                }
            }
        }

        public MethodInfo EndMethod
        {
            get { return endMethod; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    endMethod = value;
                }
            }
        }

        public MethodInfo SyncMethod
        {
            get { return syncMethod; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    syncMethod = value;
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
                if (faultFormatter == null)
                {
                    faultFormatter = new DataContractSerializerFaultFormatter(faultContractInfos);
                }
                return faultFormatter;
            }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    faultFormatter = value;
                    IsFaultFormatterSetExplicit = true;
                }
            }
        }

        internal bool IsFaultFormatterSetExplicit { get; private set; } = false;

        internal IClientMessageFormatter InternalFormatter { get; set; }

        public bool IsInitiating
        {
            get { return isInitiating; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    isInitiating = value;
                }
            }
        }

        public bool IsOneWay
        {
            get { return isOneWay; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    isOneWay = value;
                }
            }
        }

        public bool IsTerminating
        {
            get { return isTerminating; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    isTerminating = value;
                }
            }
        }

        public string Name { get; }

        public ICollection<IParameterInspector> ClientParameterInspectors
        {
            get { return ParameterInspectors; }
        }

        internal SynchronizedCollection<IParameterInspector> ParameterInspectors
        {
            get { return parameterInspectors; }
        }

        public ClientRuntime Parent { get; }

        public string ReplyAction
        {
            get { return replyAction; }
        }

        public bool SerializeRequest
        {
            get { return serializeRequest; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    serializeRequest = value;
                }
            }
        }

        public bool DeserializeReply
        {
            get { return deserializeReply; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    deserializeReply = value;
                }
            }
        }

        public MethodInfo TaskMethod
        {
            get { return taskMethod; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    taskMethod = value;
                }
            }
        }

        public Type TaskTResult
        {
            get { return taskTResult; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    taskTResult = value;
                }
            }
        }

        internal bool IsSessionOpenNotificationEnabled
        {
            get { return isSessionOpenNotificationEnabled; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    isSessionOpenNotificationEnabled = value;
                }
            }
        }

    }

}