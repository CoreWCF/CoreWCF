// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using CoreWCF.Collections.Generic;

namespace CoreWCF.Dispatcher
{
    public sealed class DispatchOperation
    {
        private readonly SynchronizedCollection<FaultContractInfo> faultContractInfos;
        private IDispatchFaultFormatter faultFormatter;
        private ImpersonationOption impersonation;
        private bool isTerminating;
        private bool isSessionOpenNotificationEnabled;
        private bool releaseInstanceAfterCall;
        private bool releaseInstanceBeforeCall;
        private bool deserializeRequest = true;
        private bool serializeReply = true;
        private bool autoDisposeParameters = true;

        public DispatchOperation(DispatchRuntime parent, string name, string action)
        {
            Parent = parent ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parent));
            Name = name ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            Action = action;
            impersonation = OperationBehaviorAttribute.DefaultImpersonationOption;
            // Not necessary for basic functionality
            CallContextInitializers = parent.NewBehaviorCollection<ICallContextInitializer>();
            faultContractInfos = parent.NewBehaviorCollection<FaultContractInfo>();
            ParameterInspectors = parent.NewBehaviorCollection<IParameterInspector>();
            IsOneWay = true;
        }

        internal DispatchOperation(DispatchRuntime parent, string name, string action, string replyAction) : this(parent, name, action)
        {
            ReplyAction = replyAction;
            IsOneWay = false;
        }

        public string Action { get; }

        internal SynchronizedCollection<ICallContextInitializer> CallContextInitializers { get; }

        internal SynchronizedCollection<FaultContractInfo> FaultContractInfos
        {
            get { return faultContractInfos; }
        }

        internal IDispatchMessageFormatter Formatter
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

        internal IDispatchFaultFormatter FaultFormatter
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

        public bool AutoDisposeParameters
        {
            get { return autoDisposeParameters; }

            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    autoDisposeParameters = value;
                }
            }
        }

        public bool DeserializeRequest
        {
            get { return deserializeRequest; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    deserializeRequest = value;
                }
            }
        }

        public bool IsOneWay { get; }

        public ImpersonationOption Impersonation
        {
            get { return impersonation; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    impersonation = value;
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

        public string Name { get; }

        public ICollection<IParameterInspector> ParameterInspectors { get; }

        public DispatchRuntime Parent { get; }

        internal ReceiveContextAcknowledgementMode ReceiveContextAcknowledgementMode { get; set; }

        internal bool ReleaseInstanceAfterCall
        {
            get { return releaseInstanceAfterCall; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    releaseInstanceAfterCall = value;
                }
            }
        }

        internal bool ReleaseInstanceBeforeCall
        {
            get { return releaseInstanceBeforeCall; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    releaseInstanceBeforeCall = value;
                }
            }
        }

        public string ReplyAction { get; }

        public bool SerializeReply
        {
            get { return serializeReply; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    serializeReply = value;
                }
            }
        }
    }
}
