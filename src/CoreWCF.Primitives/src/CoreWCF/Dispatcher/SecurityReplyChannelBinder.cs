using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using CoreWCF.Runtime;
using CoreWCF.Security;
using CoreWCF.Security.Tokens;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace CoreWCF.Dispatcher
{
    //For Security over SOAP, creating a separte Binder
    class SecurityReplyChannelBinder : IChannelBinder
    {
        private IReplyChannel _channel;
        private bool _initialized = false;
        private IServiceChannelDispatcher _next;
        private SecurityServiceDispatcher _securityDispatcher;

        public SecurityReplyChannelBinder() { }

        internal void Init(IReplyChannel channel, Uri listenUri)
        {
            if (_initialized)
            {
                Fx.Assert(_channel == channel, "Wrong channel when calling Init");
                Fx.Assert(ListenUri == listenUri, "Wrong listenUri when calling Init");
                return;
            }

            if (channel == null)
            {
                Fx.Assert("ReplyChannelBinder.ReplyChannelBinder: (channel != null)");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(channel));
            }
            _channel = channel;
            ListenUri = listenUri;
            _initialized = true;
            if(_channel is SecurityReplyChannel)
            {
                this._securityDispatcher = ((SecurityReplyChannel)_channel).SecurityServiceDispatcher;
            }
        }

        public IChannel Channel
        {
            get { return _channel; }
        }

        public bool HasSession
        {
            get { return _channel is SecurityReplyChannel; }
        }

        public Uri ListenUri { get; private set; }

        public EndpointAddress LocalAddress
        {
            get { return _channel.LocalAddress; }
        }

        public EndpointAddress RemoteAddress
        {
            get
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
            }
        }

        public void Abort()
        {
            _channel.Abort();
        }

        public void CloseAfterFault(TimeSpan timeout)
        {
            var helper = new TimeoutHelper(timeout);
            _channel.CloseAsync(helper.GetCancellationToken()).GetAwaiter().GetResult();
        }

        public RequestContext CreateRequestContext(Message message)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }

        public Task SendAsync(Message message, CancellationToken token)
        {
            throw TraceUtility.ThrowHelperError(new NotImplementedException(), message);
        }

        public Task<Message> RequestAsync(Message message, CancellationToken token)
        {
            throw TraceUtility.ThrowHelperError(new NotImplementedException(), message);
        }

        public void SetNextDispatcher(IServiceChannelDispatcher dispatcher)
        {
            _next = dispatcher;
        }

        public Task DispatchAsync(RequestContext context)
        {
            Fx.Assert(_next != null, "SetNextDispatcher wasn't called");
            if (IdentityModel.SecurityUtils.
                IsRequestSecurityContextIssuance(context.RequestMessage.Headers.Action))
            {
                context = HandleHandshake(context);
                //Add logic to separate flow for handshake or actual call
                return _next.DispatchAsync(context);
            }
            else
            {
                return ProcessRequest(context);

            }

        }

        public Task DispatchAsync(Message message)
        {
            throw new NotImplementedException();
        }





        private Task ProcessRequest(RequestContext context)
        {
            String sessionKey = GetIdentifier(context.RequestMessage.Headers);
            if (String.IsNullOrEmpty(sessionKey))
                throw new Exception("Session Key is mising");
            if (this._securityDispatcher.SessionServerSettings != null)
            {
                UniqueId sessionId = new UniqueId(sessionKey);
                SecuritySessionServerSettings settings = this._securityDispatcher.SessionServerSettings;
                SecurityContextSecurityToken cacheToken =
                    settings.GetSecurityContextSecurityToken(sessionId);
                SecurityContextSecurityToken securityToken = cacheToken.Clone();
                settings.RemovePendingSession(sessionId);
                ServerSecuritySimplexSessionChannel.SecurityReplySessionChannel
                    sessionReplyChannel = new ServerSecuritySimplexSessionChannel.
                    SecurityReplySessionChannel(settings, securityToken, null
                    , settings.SettingsLifetimeManager);
                RequestContext securityRequestContext = sessionReplyChannel.ReceiveRequest(context);
                ServiceDispatcher serviceDispatcher = (ServiceDispatcher)this._securityDispatcher.InnerServiceDispatcher;
                Task<IServiceChannelDispatcher> serviceChannelDispatcherTask = serviceDispatcher.CreateServiceChannelDispatcherAsync(this._securityDispatcher.OuterChannel);
                IServiceChannelDispatcher serviceChannelDispatcher = serviceChannelDispatcherTask.GetAwaiter().GetResult();
                return serviceChannelDispatcher.DispatchAsync(securityRequestContext);
            }
            else
            {
                throw new Exception("Session in server missing");
            }

        }

        private SecurityRequestContext HandleHandshake(RequestContext context)
        {
            SecurityReplyChannel securityReplyChannel = (SecurityReplyChannel)_channel;
            SecurityRequestContext securedMessage = (SecurityRequestContext)securityReplyChannel.ProcessReceivedRequest(context);
            return securedMessage;
        }


        private String GetIdentifier(MessageHeaders headers)
        {
            int headerIndex = headers.FindHeader(XD.SecurityJan2004Dictionary.Security.Value, XD.SecurityJan2004Dictionary.Namespace.Value);
            if(headerIndex >0)
            {
                XmlReader reader = headers.GetReaderAtHeader(headerIndex).ReadSubtree();
                while (!reader.EOF)
                {
                    reader.MoveToElement();
                    if (String.Compare(reader.LocalName, SecureConversationApr2004Strings.SecurityContextToken, true) == 0)
                    {
                        reader.MoveToElement();
                        while (!reader.EOF)
                        {
                            if(String.Compare(reader.LocalName, SecureConversationApr2004Strings.Identifier, true) == 0)
                            {
                                reader.Read();
                                return reader.Value;
                            }
                            reader.Read();
                        }
                    }
                    reader.Read();
                 }
            }

            return String.Empty;
        }

    }
}
