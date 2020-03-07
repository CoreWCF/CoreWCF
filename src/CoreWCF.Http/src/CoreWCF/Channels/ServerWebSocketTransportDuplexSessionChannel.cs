using CoreWCF.Runtime;
using CoreWCF.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    internal class ServerWebSocketTransportDuplexSessionChannel : WebSocketTransportDuplexSessionChannel
    {
        WebSocketContext _webSocketContext;
        HttpContext _httpContext;
        private IHttpTransportFactorySettings _transportSettings;
        private IServiceProvider _serviceProvider;
        WebSocketMessageSource webSocketMessageSource;
        SessionOpenNotification sessionOpenNotification;

        public ServerWebSocketTransportDuplexSessionChannel(HttpContext httpContext, WebSocketContext webSocketContext, HttpTransportSettings settings, Uri localVia, IServiceProvider serviceProvider)
            : base(settings, new EndpointAddress(localVia), localVia)
        {
            _httpContext = httpContext;
            _webSocketContext = webSocketContext;
            _transportSettings = settings;
            _serviceProvider = serviceProvider;
        }

        protected override bool IsStreamedOutput
        {
            get { return TransferModeHelper.IsResponseStreamed(this.TransferMode); }
        }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(SessionOpenNotification))
            {
                if (this.sessionOpenNotification == null)
                {
                    this.sessionOpenNotification = new SessionOpenNotificationHelper(this);
                }

                return (T)(object)this.sessionOpenNotification;
            }

            T service = _serviceProvider.GetService<T>();
            if (service == null)
            {
                service = base.GetProperty<T>();
            }

            return service;
        }

        internal void SetWebSocketInfo(WebSocketContext webSocketContext, RemoteEndpointMessageProperty remoteEndpointMessageProperty, SecurityMessageProperty handshakeSecurityMessageProperty, bool shouldDisposeWebSocketAfterClosed, HttpContext httpContext)
        {
            Fx.Assert(webSocketContext != null, "webSocketContext should not be null.");
            this.ShouldDisposeWebSocketAfterClosed = shouldDisposeWebSocketAfterClosed;
            this._webSocketContext = webSocketContext;
            this.WebSocket = webSocketContext.WebSocket;

            if (handshakeSecurityMessageProperty != null)
            {
                this.RemoteSecurity = handshakeSecurityMessageProperty;
            }

            bool inputUseStreaming = TransferModeHelper.IsRequestStreamed(this.TransferMode);
            this.webSocketMessageSource = new WebSocketMessageSource(
                            this,
                            this._webSocketContext,
                            inputUseStreaming,
                            remoteEndpointMessageProperty,
                            this,
                            httpContext);

            this.SetMessageSource(this.webSocketMessageSource);
        }

        protected override void OnClosed()
        {
            base.OnClosed();
        }

        protected override async Task OnOpenAsync(CancellationToken token)
        {
            RemoteEndpointMessageProperty remoteEndpointMessageProperty = null;
            if (_httpContext.Connection.RemoteIpAddress != null)
            {
                remoteEndpointMessageProperty = new RemoteEndpointMessageProperty(_httpContext.Connection.RemoteIpAddress.ToString(), _httpContext.Connection.RemotePort);
            }

            SetWebSocketInfo(_webSocketContext, remoteEndpointMessageProperty, ProcessAuthentication(), true, _httpContext);
            //if (TD.WebSocketConnectionAcceptedIsEnabled())
            //{
            //    TD.WebSocketConnectionAccepted(
            //        this._httpContext.EventTraceActivity,
            //        this.WebSocket != null ? this.WebSocket.GetHashCode() : -1);
            //}
        }

        private SecurityMessageProperty ProcessAuthentication()
        {
            if (this.ShouldProcessAuthentication())
            {
                // TODO: Create SecurityMessageProperty further up stack
                return null;
                //return this.ProcessRequiredAuthentication();
            }
            else
            {
                return null;
            }
        }

        bool ShouldProcessAuthentication()
        {
            Fx.Assert(_transportSettings != null, "IsAuthenticated should only be called if _transportSettings != null");
            Fx.Assert(_httpContext != null, "IsAuthenticated should only be called if _httpContext != null");
            return _transportSettings.IsAuthenticationRequired || (_transportSettings.IsAuthenticationSupported && _httpContext.User.Identity.IsAuthenticated);
        }

        class SessionOpenNotificationHelper : SessionOpenNotification
        {
            readonly ServerWebSocketTransportDuplexSessionChannel channel;

            public SessionOpenNotificationHelper(ServerWebSocketTransportDuplexSessionChannel channel)
            {
                this.channel = channel;
            }

            public override bool IsEnabled
            {
                get
                {
                    return this.channel.WebSocketSettings.CreateNotificationOnConnection;
                }
            }

            public override void UpdateMessageProperties(MessageProperties inboundMessageProperties)
            {
                this.channel.webSocketMessageSource.UpdateOpenNotificationMessageProperties(inboundMessageProperties);
            }
        }
    }
}
