// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;

namespace CoreWCF
{
    public sealed class OperationContext : IExtensibleObject<OperationContext>
    {
        private static readonly AsyncLocal<Holder> s_currentContext = new AsyncLocal<Holder>();
        private Message _clientReply;
        private bool _closeClientReply;
        private ExtensionCollection<OperationContext> _extensions;
        private Message _request;
        internal IPrincipal _threadPrincipal;
        private MessageProperties _outgoingMessageProperties;
        private MessageHeaders _outgoingMessageHeaders;

        public event EventHandler OperationCompleted;

        public OperationContext(IContextChannel channel)
        {
            if (channel == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(channel));
            }


            //Could be a TransparentProxy
            if (!(channel is ServiceChannel serviceChannel))
            {
                serviceChannel = ServiceChannelFactory.GetServiceChannel(channel);
            }

            if (serviceChannel != null)
            {
                OutgoingMessageVersion = serviceChannel.MessageVersion;
                InternalServiceChannel = serviceChannel;
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
            Host = host;
            OutgoingMessageVersion = outgoingMessageVersion ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(outgoingMessageVersion));
        }

        internal OperationContext(RequestContext requestContext, Message request, ServiceChannel channel, ServiceHostBase host)
        {
            InternalServiceChannel = channel;
            Host = host;
            RequestContext = requestContext;
            _request = request;
            OutgoingMessageVersion = channel.MessageVersion;
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
                Holder holder = s_currentContext.Value;
                if (holder == null)
                {
                    holder = new Holder();
                    s_currentContext.Value = holder;
                }
                return holder;
            }
        }

        public ServiceHostBase Host { get; }

        public EndpointDispatcher EndpointDispatcher { get; set; }
        public bool IsUserContext
        {
            get
            {
                return (_request == null);
            }
        }

        public IExtensionCollection<OperationContext> Extensions
        {
            get
            {
                if (_extensions == null)
                {
                    _extensions = new ExtensionCollection<OperationContext>(this);
                }
                return _extensions;
            }
        }

        internal bool IsServiceReentrant { get; set; } = false;

        internal Message IncomingMessage
        {
            get { return _clientReply ?? _request; }
        }

        internal ServiceChannel InternalServiceChannel { get; set; }

        internal bool HasOutgoingMessageHeaders
        {
            get { return (_outgoingMessageHeaders != null); }
        }

        public MessageHeaders OutgoingMessageHeaders
        {
            get
            {
                if (_outgoingMessageHeaders == null)
                {
                    _outgoingMessageHeaders = new MessageHeaders(OutgoingMessageVersion);
                }

                return _outgoingMessageHeaders;
            }
        }

        internal bool HasOutgoingMessageProperties
        {
            get { return (_outgoingMessageProperties != null); }
        }

        public MessageProperties OutgoingMessageProperties
        {
            get
            {
                if (_outgoingMessageProperties == null)
                {
                    _outgoingMessageProperties = new MessageProperties();
                }

                return _outgoingMessageProperties;
            }
        }

        internal MessageVersion OutgoingMessageVersion { get; }

        public MessageHeaders IncomingMessageHeaders
        {
            get
            {
                Message message = _clientReply ?? _request;
                if (message != null)
                {
                    return message.Headers;
                }
                else
                {
                    return null;
                }
            }
        }

        public MessageProperties IncomingMessageProperties
        {
            get
            {
                Message message = _clientReply ?? _request;
                if (message != null)
                {
                    return message.Properties;
                }
                else
                {
                    return null;
                }
            }
        }

        public MessageVersion IncomingMessageVersion
        {
            get
            {
                Message message = _clientReply ?? _request;
                if (message != null)
                {
                    return message.Version;
                }
                else
                {
                    return null;
                }
            }
        }

        public InstanceContext InstanceContext { get; private set; }

        public RequestContext RequestContext { get; set; }

        public ServiceSecurityContext ServiceSecurityContext
        {
            get
            {
                MessageProperties properties = IncomingMessageProperties;
                if (properties != null && properties.Security != null)
                {
                    return properties.Security.ServiceSecurityContext;
                }
                return null;
            }
        }

        public string SessionId
        {
            get
            {
                if (InternalServiceChannel != null)
                {
                    IChannel inner = InternalServiceChannel.InnerChannel;
                    if (inner != null)
                    {
                        if ((inner is ISessionChannel<IDuplexSession> duplex) && (duplex.Session != null))
                            return duplex.Session.Id;

                        if ((inner is ISessionChannel<IInputSession> input) && (input.Session != null))
                            return input.Session.Id;

                        if ((inner is ISessionChannel<IOutputSession> output) && (output.Session != null))
                            return output.Session.Id;
                    }
                }
                return null;
            }
        }

        internal IPrincipal ThreadPrincipal
        {
            get { return _threadPrincipal; }
            set { _threadPrincipal = value; }
        }

        public ClaimsPrincipal ClaimsPrincipal
        {
            get;
            internal set;
        }

        internal void ClearClientReplyNoThrow()
        {
            _clientReply = null;
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
                {
                    throw;
                }

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
            }
        }

        public T GetCallbackChannel<T>()
        {
            if (InternalServiceChannel == null || IsUserContext)
            {
                return default;
            }

            // yes, we might throw InvalidCastException here.  Is it really
            // better to check and throw something else instead?
            return (T)InternalServiceChannel.Proxy;
        }

        internal void ReInit(RequestContext requestContext, Message request, ServiceChannel channel)
        {
            RequestContext = requestContext;
            _request = request;
            InternalServiceChannel = channel;
        }

        internal void Recycle()
        {
            RequestContext = null;
            _request = null;
            _extensions = null;
            InstanceContext = null;
            _threadPrincipal = null;
            SetClientReply(null, false);
        }

        internal void SetClientReply(Message message, bool closeMessage)
        {
            Message oldClientReply = null;

            if (!Equals(message, _clientReply))
            {
                if (_closeClientReply && (_clientReply != null))
                {
                    oldClientReply = _clientReply;
                }

                _clientReply = message;
            }

            _closeClientReply = closeMessage;

            if (oldClientReply != null)
            {
                oldClientReply.Close();
            }
        }

        internal void SetInstanceContext(InstanceContext instanceContext)
        {
            InstanceContext = instanceContext;
        }

        internal class Holder
        {
            public OperationContext Context { get; set; }
        }
    }
}
