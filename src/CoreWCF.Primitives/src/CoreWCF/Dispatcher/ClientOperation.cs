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
        private string action;
        private SynchronizedCollection<FaultContractInfo> faultContractInfos;
        private bool serializeRequest;
        private bool deserializeReply;
        private IClientMessageFormatter formatter;
        private IClientFaultFormatter faultFormatter;
        private bool isInitiating = true;
        private bool isOneWay;
        private bool isTerminating;
        private bool isSessionOpenNotificationEnabled;
        private string name;
        private ClientRuntime parent;
        private string replyAction;
        private MethodInfo beginMethod;
        private MethodInfo endMethod;
        private MethodInfo syncMethod;
        private MethodInfo taskMethod;
        private Type taskTResult;
        private bool isFaultFormatterSetExplicit = false;
        private SynchronizedCollection<IParameterInspector> parameterInspectors;

        public ClientOperation(ClientRuntime parent, string name, string action)
            : this(parent, name, action, null)
        {
        }

        public ClientOperation(ClientRuntime parent, string name, string action, string replyAction)
        {
            if (parent == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parent));

            if (name == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));

            this.parent = parent;
            this.name = name;
            this.action = action;
            this.replyAction = replyAction;

            faultContractInfos = parent.NewBehaviorCollection<FaultContractInfo>();
            parameterInspectors = parent.NewBehaviorCollection<IParameterInspector>();
        }

        public string Action
        {
            get { return action; }
        }

        internal SynchronizedCollection<FaultContractInfo> FaultContractInfos
        {
            get { return faultContractInfos; }
        }

        public MethodInfo BeginMethod
        {
            get { return beginMethod; }
            set
            {
                lock (parent.ThisLock)
                {
                    parent.InvalidateRuntime();
                    beginMethod = value;
                }
            }
        }

        public MethodInfo EndMethod
        {
            get { return endMethod; }
            set
            {
                lock (parent.ThisLock)
                {
                    parent.InvalidateRuntime();
                    endMethod = value;
                }
            }
        }

        public MethodInfo SyncMethod
        {
            get { return syncMethod; }
            set
            {
                lock (parent.ThisLock)
                {
                    parent.InvalidateRuntime();
                    syncMethod = value;
                }
            }
        }

        public IClientMessageFormatter Formatter
        {
            get { return formatter; }
            set
            {
                lock (parent.ThisLock)
                {
                    parent.InvalidateRuntime();
                    formatter = value;
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
                lock (parent.ThisLock)
                {
                    parent.InvalidateRuntime();
                    faultFormatter = value;
                    isFaultFormatterSetExplicit = true;
                }
            }
        }

        internal bool IsFaultFormatterSetExplicit
        {
            get
            {
                return isFaultFormatterSetExplicit;
            }
        }

        internal IClientMessageFormatter InternalFormatter
        {
            get { return formatter; }
            set { formatter = value; }
        }

        public bool IsInitiating
        {
            get { return isInitiating; }
            set
            {
                lock (parent.ThisLock)
                {
                    parent.InvalidateRuntime();
                    isInitiating = value;
                }
            }
        }

        public bool IsOneWay
        {
            get { return isOneWay; }
            set
            {
                lock (parent.ThisLock)
                {
                    parent.InvalidateRuntime();
                    isOneWay = value;
                }
            }
        }

        public bool IsTerminating
        {
            get { return isTerminating; }
            set
            {
                lock (parent.ThisLock)
                {
                    parent.InvalidateRuntime();
                    isTerminating = value;
                }
            }
        }

        public string Name
        {
            get { return name; }
        }

        public ICollection<IParameterInspector> ClientParameterInspectors
        {
            get { return ParameterInspectors; }
        }

        internal SynchronizedCollection<IParameterInspector> ParameterInspectors
        {
            get { return parameterInspectors; }
        }

        public ClientRuntime Parent
        {
            get { return parent; }
        }

        public string ReplyAction
        {
            get { return replyAction; }
        }

        public bool SerializeRequest
        {
            get { return serializeRequest; }
            set
            {
                lock (parent.ThisLock)
                {
                    parent.InvalidateRuntime();
                    serializeRequest = value;
                }
            }
        }

        public bool DeserializeReply
        {
            get { return deserializeReply; }
            set
            {
                lock (parent.ThisLock)
                {
                    parent.InvalidateRuntime();
                    deserializeReply = value;
                }
            }
        }

        public MethodInfo TaskMethod
        {
            get { return taskMethod; }
            set
            {
                lock (parent.ThisLock)
                {
                    parent.InvalidateRuntime();
                    taskMethod = value;
                }
            }
        }

        public Type TaskTResult
        {
            get { return taskTResult; }
            set
            {
                lock (parent.ThisLock)
                {
                    parent.InvalidateRuntime();
                    taskTResult = value;
                }
            }
        }

        internal bool IsSessionOpenNotificationEnabled
        {
            get { return isSessionOpenNotificationEnabled; }
            set
            {
                lock (parent.ThisLock)
                {
                    parent.InvalidateRuntime();
                    isSessionOpenNotificationEnabled = value;
                }
            }
        }

    }

}