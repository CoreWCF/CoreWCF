// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Net.WebSockets;
using System.Security.Principal;
using CoreWCF.Runtime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace CoreWCF.Channels
{
    internal class AspNetCoreWebSocketContext : WebSocketContext
    {
        private readonly HttpContext _httpContext;
        private readonly WebSocket _webSocket;
        private CookieCollection _cookieCollection;
        private NameValueCollection _headers;
        private bool? _isLocal;
        private Uri _requestUri;

        internal AspNetCoreWebSocketContext(HttpContext httpContext, WebSocket webSocket)
        {
            Fx.Assert(httpContext != null, "HttpContext can't be null");
            Fx.Assert(webSocket != null, "WebSocket can't be null");
            _httpContext = httpContext;
            _webSocket = webSocket;
        }

        public override CookieCollection CookieCollection
        {
            get
            {
                if (_cookieCollection == null)
                {
                    var cookieContainer = new CookieContainer();
                    foreach (KeyValuePair<string, string> item in _httpContext.Request.Cookies)
                    {
                        cookieContainer.SetCookies(RequestUri, item.Value);
                    }

                    _cookieCollection = cookieContainer.GetCookies(RequestUri);
                }

                return _cookieCollection;
            }
        }

        public override NameValueCollection Headers
        {
            get
            {
                if (_headers == null)
                {
                    var headers = new NameValueCollection();
                    foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> header in _httpContext.Request.Headers)
                    {
                        headers.Add(header.Key, header.Value);
                    }
                    _headers = headers;
                }

                return _headers;
            }
        }

        public override bool IsAuthenticated => _httpContext.User.Identity.IsAuthenticated;

        public override bool IsLocal
        {
            get
            {
                if (!_isLocal.HasValue)
                {
                    ConnectionInfo connection = _httpContext.Connection;
                    if (connection.RemoteIpAddress != null)
                    {
                        if (connection.RemoteIpAddress.Equals(connection.LocalIpAddress))
                        {
                            _isLocal = true;
                        }
                        else
                        {
                            _isLocal = IPAddress.IsLoopback(connection.RemoteIpAddress);
                        }
                    }
                    else if (connection.LocalIpAddress == null)
                    {
                        _isLocal = true;
                    }
                    else
                    {
                        _isLocal = false;
                    }
                }

                return _isLocal.Value;
            }
        }

        public override bool IsSecureConnection => throw new PlatformNotSupportedException(nameof(IsSecureConnection));

        public override string Origin => Headers["Origin"];

        public override Uri RequestUri
        {
            get
            {
                if (_requestUri == null)
                {
                    _requestUri = new Uri(_httpContext.Request.GetEncodedUrl());
                }

                return _requestUri;
            }
        }

        public override string SecWebSocketKey => Headers["Sec-WebSocket-Key"];

        public override IEnumerable<string> SecWebSocketProtocols => _httpContext.WebSockets.WebSocketRequestedProtocols;

        public override string SecWebSocketVersion => Headers["Sec-WebSocket-Version"];

        public override IPrincipal User => _httpContext.User;

        public override WebSocket WebSocket => _webSocket;
    }
}
