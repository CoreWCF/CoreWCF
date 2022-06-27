// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Runtime;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Channels
{
    internal class RequestDelegateHandler
    {
        internal const long DefaultMaxBufferPoolSize = 512 * 1024;
        private readonly IServiceDispatcher _serviceDispatcher;
        private readonly IDefaultCommunicationTimeouts _timeouts;
        private readonly IServiceScopeFactory _servicesScopeFactory;
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

        public bool IsAuthenticationRequired => _httpSettings.IsAuthenticationRequired;

        internal WebSocketOptions WebSocketOptions { get; set; }

        private void BuildHandler()
        {
            BindingElementCollection be = _serviceDispatcher.Binding.CreateBindingElements();
            MessageEncodingBindingElement mebe = be.Find<MessageEncodingBindingElement>();
            if (mebe == null)
            {
                throw new ArgumentException("Must provide a MessageEncodingBindingElement", nameof(_serviceDispatcher.Binding));
            }

            HttpTransportBindingElement tbe = be.Find<HttpTransportBindingElement>();
            if (tbe == null)
            {
                throw new ArgumentException("Must provide a HttpTransportBindingElement", nameof(_serviceDispatcher.Binding));
            }

            var httpSettings = new HttpTransportSettings
            {
                BufferManager = BufferManager.CreateBufferManager(DefaultMaxBufferPoolSize, tbe.MaxBufferSize),
                OpenTimeout = _serviceDispatcher.Binding.OpenTimeout,
                ReceiveTimeout = _serviceDispatcher.Binding.ReceiveTimeout,
                SendTimeout = _serviceDispatcher.Binding.SendTimeout,
                CloseTimeout = _serviceDispatcher.Binding.CloseTimeout,
                MaxBufferSize = tbe.MaxBufferSize,
                MaxReceivedMessageSize = tbe.MaxReceivedMessageSize,
                MessageEncoderFactory = mebe.CreateMessageEncoderFactory(),
                ManualAddressing = tbe.ManualAddressing,
                TransferMode = tbe.TransferMode,
                KeepAliveEnabled = tbe.KeepAliveEnabled,
                AnonymousUriPrefixMatcher = new HttpAnonymousUriPrefixMatcher(),
                AuthenticationScheme = tbe.AuthenticationScheme,
                WebSocketSettings = tbe.WebSocketSettings.Clone()
            };
            _httpSettings = httpSettings;
            WebSocketOptions = CreateWebSocketOptions(tbe);

            if (WebSocketOptions == null)
            {
                _replyChannel = new AspNetCoreReplyChannel(_servicesScopeFactory.CreateScope().ServiceProvider, _httpSettings);
                _replyChannelDispatcherTask = _serviceDispatcher.CreateServiceChannelDispatcherAsync(_replyChannel);
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
            if (IsAuthenticationRequired)
            {
                string scheme = _httpSettings.AuthenticationScheme.ToString();
                AuthenticateResult authenticateResult = await context.AuthenticateAsync(scheme);
                if (authenticateResult.None)
                {
                    await context.ChallengeAsync(scheme);
                    return;
                }
            }

            if (!context.WebSockets.IsWebSocketRequest)
            {
                if (_replyChannelDispatcher == null)
                {
                    _replyChannelDispatcher = await _replyChannelDispatcherTask;
                    _replyChannel.ChannelDispatcher = _replyChannelDispatcher;
                }

                await _replyChannel.HandleRequest(context);
            }
            else
            {
                CancellationToken openTimeoutToken = new TimeoutHelper(((IDefaultCommunicationTimeouts)_httpSettings).OpenTimeout).GetCancellationToken();
                WebSocketContext webSocketContext = await AcceptWebSocketAsync(context, openTimeoutToken);
                if (webSocketContext == null)
                {
                    return;
                }

                var channel = new ServerWebSocketTransportDuplexSessionChannel(context, webSocketContext, _httpSettings, _serviceDispatcher.BaseAddress, _servicesScopeFactory.CreateScope().ServiceProvider);
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
