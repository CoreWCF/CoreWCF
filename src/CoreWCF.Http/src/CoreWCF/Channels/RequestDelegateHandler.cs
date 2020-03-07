using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using CoreWCF.Runtime;
using CoreWCF.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Builder;
using System.Net.WebSockets;
using System.Net;
using Microsoft.AspNetCore.Http.Features;

namespace CoreWCF.Channels
{
    internal class RequestDelegateHandler
    {
        internal const long DefaultMaxBufferPoolSize = 512 * 1024;
        private IServiceDispatcher _serviceDispatcher;
        private IDefaultCommunicationTimeouts _timeouts;
        private IServiceScopeFactory _servicesScopeFactory;
        private HttpTransportSettings _httpSettings;
        private AspNetCoreReplyChannel _replyChannel;
        private Task<IServiceChannelDispatcher> _replyChannelDispatcherTask;
        private IServiceChannelDispatcher _replyChannelDispatcher;

        public RequestDelegateHandler(IServiceDispatcher serviceDispatcher, IServiceScopeFactory servicesScopeFactory)
        {
            _serviceDispatcher = serviceDispatcher;
            _timeouts = _serviceDispatcher.Binding;
            _servicesScopeFactory = servicesScopeFactory;
            BuildHandler();
        }

        internal WebSocketOptions WebSocketOptions { get; set; }

        private void BuildHandler()
        {
            var be = _serviceDispatcher.Binding.CreateBindingElements();
            var mebe = be.Find<MessageEncodingBindingElement>();
            if (mebe == null)
            {
                throw new ArgumentException("Must provide a MessageEncodingBindingElement", nameof(_serviceDispatcher.Binding));
            }

            var tbe = be.Find<HttpTransportBindingElement>();
            if (tbe == null)
            {
                throw new ArgumentException("Must provide a HttpTransportBindingElement", nameof(_serviceDispatcher.Binding));
            }

            var httpSettings = new HttpTransportSettings();
            httpSettings.BufferManager = BufferManager.CreateBufferManager(DefaultMaxBufferPoolSize, tbe.MaxBufferSize);
            httpSettings.OpenTimeout = _serviceDispatcher.Binding.OpenTimeout;
            httpSettings.ReceiveTimeout = _serviceDispatcher.Binding.ReceiveTimeout;
            httpSettings.SendTimeout = _serviceDispatcher.Binding.SendTimeout;
            httpSettings.CloseTimeout = _serviceDispatcher.Binding.CloseTimeout;
            httpSettings.MaxBufferSize = tbe.MaxBufferSize;
            httpSettings.MaxReceivedMessageSize = tbe.MaxReceivedMessageSize;
            httpSettings.MessageEncoderFactory = mebe.CreateMessageEncoderFactory();
            httpSettings.ManualAddressing = tbe.ManualAddressing;
            httpSettings.TransferMode = tbe.TransferMode;
            httpSettings.KeepAliveEnabled = tbe.KeepAliveEnabled;
            httpSettings.AnonymousUriPrefixMatcher = new HttpAnonymousUriPrefixMatcher();
            httpSettings.AuthenticationScheme = tbe.AuthenticationScheme;
            httpSettings.WebSocketSettings = tbe.WebSocketSettings.Clone();

            _httpSettings = httpSettings;
            WebSocketOptions = CreateWebSocketOptions(tbe);
            if (WebSocketOptions == null)
            {
                _replyChannel = new AspNetCoreReplyChannel(_servicesScopeFactory.CreateScope().ServiceProvider, _httpSettings);
                _replyChannelDispatcherTask =  _serviceDispatcher.CreateServiceChannelDispatcherAsync(_replyChannel);
            }
        }

        private WebSocketOptions CreateWebSocketOptions(HttpTransportBindingElement tbe)
        {
            // TODO: Is a check for IDuplexSessionChannel also needed?
            bool canUseWebSockets = tbe.WebSocketSettings.TransportUsage == WebSocketTransportUsage.Always ||
                (tbe.WebSocketSettings.TransportUsage == WebSocketTransportUsage.WhenDuplex && _serviceDispatcher.SupportedChannelTypes.Contains(typeof(IDuplexChannel)));
            if (!canUseWebSockets)
            {
                return null;
            }

            return new WebSocketOptions
            {
                ReceiveBufferSize = WebSocketHelper.GetReceiveBufferSize(tbe.MaxReceivedMessageSize),
                KeepAliveInterval = tbe.WebSocketSettings.GetEffectiveKeepAliveInterval()
            };
        }

        internal async Task HandleRequest(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                if (_replyChannelDispatcher == null)
                {
                    _replyChannelDispatcher = await _replyChannelDispatcherTask;
                }

                await _replyChannel.HandleRequest(context);
            }
            else
            {
                var openTimeoutToken = new TimeoutHelper(((IDefaultCommunicationTimeouts)_httpSettings).OpenTimeout).GetCancellationToken();
                var webSocketContext = await AcceptWebSocketAsync(context, openTimeoutToken);
                if (webSocketContext == null)
                {
                    return;
                }

                var channel = new ServerWebSocketTransportDuplexSessionChannel(context, webSocketContext, _httpSettings, _serviceDispatcher.BaseAddress,_servicesScopeFactory.CreateScope().ServiceProvider);
                channel.ChannelDispatcher = await _serviceDispatcher.CreateServiceChannelDispatcherAsync(channel);
                await channel.StartReceivingAsync();
            }

            return;
        }

        private async Task<WebSocketContext> AcceptWebSocketAsync(HttpContext context, CancellationToken token)
        {
            //if (TD.WebSocketConnectionAcceptStartIsEnabled())
            //{
            //    TD.WebSocketConnectionAcceptStart(this.httpRequestContext.EventTraceActivity);
            //}

            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Features.Get<IHttpResponseFeature>().ReasonPhrase = SR.WebSocketEndpointOnlySupportWebSocketError;
                return null;
            }

            try
            {
                using (token.Register(() => { context.Abort(); }))
                {
                    string negotiatedProtocol = null;

                    // match client protocols vs server protocol
                    foreach (string protocol in context.WebSockets.WebSocketRequestedProtocols)
                    {
                        if (string.Compare(protocol, _httpSettings.WebSocketSettings.SubProtocol, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            negotiatedProtocol = protocol;
                            break;
                        }
                    }

                    if (negotiatedProtocol == null)
                    {
                        string errorMessage = SR.Format(SR.WebSocketInvalidProtocolNotInClientList, _httpSettings.WebSocketSettings.SubProtocol, string.Join(", ", context.WebSockets.WebSocketRequestedProtocols));
                        Fx.Exception.AsWarning(new WebException(errorMessage));

                        context.Response.StatusCode = (int)HttpStatusCode.UpgradeRequired;
                        context.Features.Get<IHttpResponseFeature>().ReasonPhrase = SR.WebSocketEndpointOnlySupportWebSocketError;
                        return null;
                    }

                    WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync(negotiatedProtocol);
                    return new AspNetCoreWebSocketContext(context, webSocket);
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

        }

        private void SendUpgradeRequiredResponseMessageWithSubProtocol()
        {
        }

        internal async Task HandleDuplexConnection(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                
            }
        }
    }
}