// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using CoreWCF.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Channels
{
    internal class ServerWebSocketTransportDuplexSessionChannel : WebSocketTransportDuplexSessionChannel
    {
        private WebSocketContext _webSocketContext;
        private readonly HttpContext _httpContext;
        private readonly IHttpTransportFactorySettings _transportSettings;
        private readonly IServiceProvider _serviceProvider;
        private WebSocketMessageSource _webSocketMessageSource;
        private SessionOpenNotification _sessionOpenNotification;

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
            get { return TransferModeHelper.IsResponseStreamed(TransferMode); }
        }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(SessionOpenNotification))
            {
                if (_sessionOpenNotification == null)
                {
                    _sessionOpenNotification = new SessionOpenNotificationHelper(this);
                }

                return (T)(object)_sessionOpenNotification;
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
            ShouldDisposeWebSocketAfterClosed = shouldDisposeWebSocketAfterClosed;
            _webSocketContext = webSocketContext;
            WebSocket = webSocketContext.WebSocket;

            if (handshakeSecurityMessageProperty != null)
            {
                RemoteSecurity = handshakeSecurityMessageProperty;
            }

            bool inputUseStreaming = TransferModeHelper.IsRequestStreamed(TransferMode);
            _webSocketMessageSource = new WebSocketMessageSource(
                            this,
                            _webSocketContext,
                            inputUseStreaming,
                            remoteEndpointMessageProperty,
                            this,
                            httpContext);

            SetMessageSource(_webSocketMessageSource);
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
            if (ShouldProcessAuthentication())
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

        private bool ShouldProcessAuthentication()
        {
            Fx.Assert(_transportSettings != null, "IsAuthenticated should only be called if _transportSettings != null");
            Fx.Assert(_httpContext != null, "IsAuthenticated should only be called if _httpContext != null");
            return _transportSettings.IsAuthenticationRequired || (_transportSettings.IsAuthenticationSupported && _httpContext.User.Identity.IsAuthenticated);
        }

        private class SessionOpenNotificationHelper : SessionOpenNotification
        {
            private readonly ServerWebSocketTransportDuplexSessionChannel _channel;

            public SessionOpenNotificationHelper(ServerWebSocketTransportDuplexSessionChannel channel)
            {
                _channel = channel;
            }

            public override bool IsEnabled
            {
                get
                {
                    return _channel.WebSocketSettings.CreateNotificationOnConnection;
                }
            }

            public override void UpdateMessageProperties(MessageProperties inboundMessageProperties)
            {
                _channel._webSocketMessageSource.UpdateOpenNotificationMessageProperties(inboundMessageProperties);
            }
        }
    }
}
