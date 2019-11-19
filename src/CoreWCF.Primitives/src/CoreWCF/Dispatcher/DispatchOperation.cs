using System.Collections.Generic;
using CoreWCF.Collections.Generic;

namespace CoreWCF.Dispatcher
{
    public sealed class DispatchOperation
    {
        string action;
        SynchronizedCollection<FaultContractInfo> faultContractInfos;
        IDispatchMessageFormatter formatter;
        IDispatchFaultFormatter faultFormatter;
        ImpersonationOption impersonation;
        IOperationInvoker invoker;
        bool isTerminating;
        bool isSessionOpenNotificationEnabled;
        string name;
        bool releaseInstanceAfterCall;
        bool releaseInstanceBeforeCall;
        string replyAction;
        bool deserializeRequest = true;
        bool serializeReply = true;
        bool autoDisposeParameters = true;

        public DispatchOperation(DispatchRuntime parent, string name, string action)
        {
            Parent = parent ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parent));
            this.name = name ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            this.action = action;
            this.impersonation = OperationBehaviorAttribute.DefaultImpersonationOption;
            // Not necessary for basic functionality
            CallContextInitializers = parent.NewBehaviorCollection<ICallContextInitializer>();
            faultContractInfos = parent.NewBehaviorCollection<FaultContractInfo>();
            ParameterInspectors = parent.NewBehaviorCollection<IParameterInspector>();
            IsOneWay = true;
        }

        internal DispatchOperation(DispatchRuntime parent, string name, string action, string replyAction) : this(parent, name, action)
        {
            this.replyAction = replyAction;
            IsOneWay = false;
        }

        public string Action
        {
            get { return action; }
        }

        internal SynchronizedCollection<ICallContextInitializer> CallContextInitializers { get; }

        internal SynchronizedCollection<FaultContractInfo> FaultContractInfos
        {
            get { return faultContractInfos; }
        }

        internal IDispatchMessageFormatter Formatter
        {
            get { return formatter; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    formatter = value;
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
            get { return this.impersonation; }
            set
            {
                lock (this.Parent.ThisLock)
                {
                    this.Parent.InvalidateRuntime();
                    this.impersonation = value;
                }
            }
        }

        internal bool HasNoDisposableParameters { get; set; }

        internal IDispatchMessageFormatter InternalFormatter
        {
            get { return formatter; }
            set { formatter = value; }
        }

        internal IOperationInvoker InternalInvoker
        {
            get { return invoker; }
            set { invoker = value; }
        }

        internal IOperationInvoker Invoker
        {
            get { return invoker; }
            set
            {
                lock (Parent.ThisLock)
                {
                    Parent.InvalidateRuntime();
                    invoker = value;
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

        public string Name
        {
            get { return name; }
        }

        public ICollection<IParameterInspector> ParameterInspectors { get; }

        public DispatchRuntime Parent { get; }

        internal ReceiveContextAcknowledgementMode ReceiveContextAcknowledgementMode { get; set; }

        internal bool BufferedReceiveEnabled
        {
            get { return Parent.ChannelDispatcher.BufferedReceiveEnabled; }
            set { Parent.ChannelDispatcher.BufferedReceiveEnabled = value; }
        }

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

        public string ReplyAction
        {
            get { return replyAction; }
        }

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
