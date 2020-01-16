using CoreWCF.Runtime;
using CoreWCF.Security;
using Microsoft.AspNetCore.Http;
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
        string _subProtocol;
        private IHttpTransportFactorySettings _transportSettings;
        WebSocketMessageSource webSocketMessageSource;
        SessionOpenNotification sessionOpenNotification;

        public ServerWebSocketTransportDuplexSessionChannel(
                        IHttpTransportFactorySettings settings,
                        EndpointAddress localAddress,
                        Uri localVia,
                        HttpContext httpContext,
                        string subProtocol)
            : base(settings, localAddress, localVia)
        {
            this._httpContext = httpContext;
            this._subProtocol = subProtocol;
            _transportSettings = settings;
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

            return base.GetProperty<T>();
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
            ((IDisposable)this._httpContext).Dispose();
        }

        protected override async Task OnOpenAsync(CancellationToken token)
        {
            //if (TD.WebSocketConnectionAcceptStartIsEnabled())
            //{
            //    TD.WebSocketConnectionAcceptStart(this.httpRequestContext.EventTraceActivity);
            //}
            if (!_httpContext.WebSockets.IsWebSocketRequest)
            {
                // TODO: Add support for this
                // this.context.SendResponseAndClose(HttpStatusCode.BadRequest, SR.GetString(SR.WebSocketEndpointOnlySupportWebSocketError));
                throw new ProtocolException(SR.WebSocketEndpointOnlySupportWebSocketError);
            }

            WebSocketContext webSocketContext;
            try
            {
                using (token.Register(() => { _httpContext.Abort(); }))
                {
                    WebSocket webSocket = await _httpContext.WebSockets.AcceptWebSocketAsync(_subProtocol);
                    webSocketContext = new AspNetCoreWebSocketContext(_httpContext, webSocket);
                }
            }
            catch (Exception ex)
            {
                if (Fx.IsFatal(ex))
                {
                    throw;
                }

                if (token.IsCancellationRequested)
                {
                    throw Fx.Exception.AsError(new TimeoutException(SR.AcceptWebSocketTimedOutError));
                }

                WebSocketHelper.ThrowCorrectException(ex);
                throw;
            }

            RemoteEndpointMessageProperty remoteEndpointMessageProperty = null;
            if (_httpContext.Connection.RemoteIpAddress != null)
            {
                remoteEndpointMessageProperty = new RemoteEndpointMessageProperty(_httpContext.Connection.RemoteIpAddress.ToString(), _httpContext.Connection.RemotePort);
            }

            SetWebSocketInfo(webSocketContext, remoteEndpointMessageProperty, ProcessAuthentication(), true, _httpContext);
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
