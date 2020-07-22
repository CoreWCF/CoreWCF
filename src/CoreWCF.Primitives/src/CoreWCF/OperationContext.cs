using System;
using System.Threading;
using CoreWCF.Runtime;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;
using System.Security.Claims;
using System.Security.Principal;

namespace CoreWCF
{
    public sealed class OperationContext : IExtensibleObject<OperationContext>
    {
        private static AsyncLocal<Holder> currentContext = new AsyncLocal<Holder>();

        ServiceChannel channel;
        Message clientReply;
        bool closeClientReply;
        ExtensionCollection<OperationContext> extensions;
        ServiceHostBase host;
        RequestContext requestContext;
        Message request;
        InstanceContext instanceContext;
        bool isServiceReentrant = false;
        internal IPrincipal threadPrincipal;
        MessageProperties outgoingMessageProperties;
        MessageHeaders outgoingMessageHeaders;
        MessageVersion outgoingMessageVersion;
        EndpointDispatcher endpointDispatcher;

        public event EventHandler OperationCompleted;

        public OperationContext(IContextChannel channel)
        {
            if (channel == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(channel));

            ServiceChannel serviceChannel = channel as ServiceChannel;

            //Could be a TransparentProxy
            if (serviceChannel == null)
            {
                serviceChannel = ServiceChannelFactory.GetServiceChannel(channel);
            }

            if (serviceChannel != null)
            {
                outgoingMessageVersion = serviceChannel.MessageVersion;
                this.channel = serviceChannel;
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxInvalidChannelToOperationContext));
            }
        }

        internal OperationContext(ServiceHostBase host)
            : this(host, MessageVersion.Soap12WSAddressing10)
        {
        }

        internal OperationContext(ServiceHostBase host, MessageVersion outgoingMessageVersion)
        {
            if (outgoingMessageVersion == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(outgoingMessageVersion));

            this.host = host;
            this.outgoingMessageVersion = outgoingMessageVersion;
        }

        internal OperationContext(RequestContext requestContext, Message request, ServiceChannel channel, ServiceHostBase host)
        {
            this.channel = channel;
            this.host = host;
            this.requestContext = requestContext;
            this.request = request;
            outgoingMessageVersion = channel.MessageVersion;
        }

        // TODO: Probably want to revert this to public
        internal IContextChannel Channel
        {
            get { return GetCallbackChannel<IContextChannel>(); }
        }

        public static OperationContext Current
        {
            get
            {
                return CurrentHolder.Context;
            }

            set
            {
                CurrentHolder.Context = value;
            }
        }

        internal static Holder CurrentHolder
        {
            get
            {
                Holder holder = OperationContext.currentContext.Value;
                if (holder == null)
                {
                    holder = new Holder();
                    OperationContext.currentContext.Value = holder;
                }
                return holder;
            }
        }

        public ServiceHostBase Host
        {
            get { return host; }
        }

        public EndpointDispatcher EndpointDispatcher
        {
            get
            {
                return endpointDispatcher;
            }
            set
            {
                endpointDispatcher = value;
            }
        }
        public bool IsUserContext
        {
            get
            {
                return (request == null);
            }
        }

        public IExtensionCollection<OperationContext> Extensions
        {
            get
            {
                if (extensions == null)
                {
                    extensions = new ExtensionCollection<OperationContext>(this);
                }
                return extensions;
            }
        }

        internal bool IsServiceReentrant
        {
            get { return isServiceReentrant; }
            set { isServiceReentrant = value; }
        }

        internal Message IncomingMessage
        {
            get { return clientReply ?? request; }
        }

        internal ServiceChannel InternalServiceChannel
        {
            get { return channel; }
            set { channel = value; }
        }

        internal bool HasOutgoingMessageHeaders
        {
            get { return (outgoingMessageHeaders != null); }
        }

        public MessageHeaders OutgoingMessageHeaders
        {
            get
            {
                if (outgoingMessageHeaders == null)
                    outgoingMessageHeaders = new MessageHeaders(OutgoingMessageVersion);

                return outgoingMessageHeaders;
            }
        }

        internal bool HasOutgoingMessageProperties
        {
            get { return (outgoingMessageProperties != null); }
        }

        public MessageProperties OutgoingMessageProperties
        {
            get
            {
                if (outgoingMessageProperties == null)
                    outgoingMessageProperties = new MessageProperties();

                return outgoingMessageProperties;
            }
        }

        internal MessageVersion OutgoingMessageVersion
        {
            get { return outgoingMessageVersion; }
        }

        public MessageHeaders IncomingMessageHeaders
        {
            get
            {
                Message message = clientReply ?? request;
                if (message != null)
                    return message.Headers;
                else
                    return null;
            }
        }

        public MessageProperties IncomingMessageProperties
        {
            get
            {
                Message message = clientReply ?? request;
                if (message != null)
                    return message.Properties;
                else
                    return null;
            }
        }

        public MessageVersion IncomingMessageVersion
        {
            get
            {
                Message message = clientReply ?? request;
                if (message != null)
                    return message.Version;
                else
                    return null;
            }
        }

        public InstanceContext InstanceContext
        {
            get { return instanceContext; }
        }

        public RequestContext RequestContext
        {
            get { return requestContext; }
            set { requestContext = value; }
        }

        public ServiceSecurityContext ServiceSecurityContext
        {
            get
            {
                MessageProperties properties = this.IncomingMessageProperties;
                if (properties != null && properties.Security != null)
                {
                    return properties.Security.ServiceSecurityContext;
                }
                return null;
            }
        }

        internal IPrincipal ThreadPrincipal
        {
            get { return this.threadPrincipal; }
            set { this.threadPrincipal = value; }
        }

        public ClaimsPrincipal ClaimsPrincipal
        {
            get;
            internal set;
        }

        internal void ClearClientReplyNoThrow()
        {
            clientReply = null;
        }

        internal void FireOperationCompleted()
        {
            try
            {
                EventHandler handler = OperationCompleted;

                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                    throw;

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
            }
        }

        public T GetCallbackChannel<T>()
        {
            if (channel == null || IsUserContext)
                return default(T);

            // yes, we might throw InvalidCastException here.  Is it really
            // better to check and throw something else instead?
            return (T)channel.Proxy;
        }

        internal void ReInit(RequestContext requestContext, Message request, ServiceChannel channel)
        {
            this.requestContext = requestContext;
            this.request = request;
            this.channel = channel;
        }

        internal void Recycle()
        {
            requestContext = null;
            request = null;
            extensions = null;
            instanceContext = null;
            threadPrincipal = null;
            SetClientReply(null, false);
        }

        internal void SetClientReply(Message message, bool closeMessage)
        {
            Message oldClientReply = null;

            if (!object.Equals(message, clientReply))
            {
                if (closeClientReply && (clientReply != null))
                {
                    oldClientReply = clientReply;
                }

                clientReply = message;
            }

            closeClientReply = closeMessage;

            if (oldClientReply != null)
            {
                oldClientReply.Close();
            }
        }

        internal void SetInstanceContext(InstanceContext instanceContext)
        {
            this.instanceContext = instanceContext;
        }

        internal class Holder
        {
            OperationContext context;

            public OperationContext Context
            {
                get
                {
                    return context;
                }

                set
                {
                    context = value;
                }
            }
        }
    }
}