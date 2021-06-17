// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using CoreWCF.Runtime;
using Microsoft.AspNetCore.Http;

namespace CoreWCF.Channels
{
    public sealed class HttpRequestMessageProperty : IMessageProperty
    {
        private readonly HttpContextBackedProperty _httpContextBackedProperty;

        internal HttpRequestMessageProperty(HttpContext httpContext)
        {
            _httpContextBackedProperty = new HttpContextBackedProperty(httpContext);
        }

        public static string Name => "httpRequest";

        /// <summary>
        /// Gets the headers from the underlying HTTP Request.
        /// </summary>
        public WebHeaderCollection Headers => _httpContextBackedProperty.Headers;

        /// <summary>
        /// Gets the method from the underlying HTTP Request.
        /// </summary>
        public string Method => _httpContextBackedProperty.Method;

        /// <summary>
        /// Gets the query string from the underlying HTTP Request.
        /// </summary>
        /// <remarks>The leading '?' is removed.</remarks>
        public string QueryString => _httpContextBackedProperty.QueryString;

        /// <summary>
        /// The entity body is suppressed is the underlying HTTP request has a zero ContentLength.
        /// </summary>
        /// <value><c>true</c> if [suppress entity body]; otherwise, <c>false</c>.</value>
        public bool SuppressEntityBody => _httpContextBackedProperty.SuppressEntityBody;

        IMessageProperty IMessageProperty.CreateCopy()
        {
            return this;
        }

        private class HttpContextBackedProperty
        {
            public HttpContextBackedProperty(HttpContext httpContext)
            {
                Fx.Assert(httpContext != null, "The 'httpResponseMessage' property should never be null.");

                HttpContext = httpContext;
            }

            public HttpContext HttpContext { get; private set; }

            private WebHeaderCollection _headers;

            public WebHeaderCollection Headers
            {
                get
                {
                    if (_headers == null)
                    {
                        _headers = HttpContext.Request.ToWebHeaderCollection();
                    }

                    return _headers;
                }
            }

            public string Method => HttpContext.Request.Method;

            public string QueryString
            {
                get
                {
                    string query = HttpContext.Request.QueryString.Value;
                    return query.Length == 0 ? string.Empty : query.Substring(1);
                }
            }

            public bool SuppressEntityBody
            {
                get
                {
                    long? contentLength = HttpContext.Request.ContentLength;

                    if (!contentLength.HasValue ||
                        (contentLength.HasValue && contentLength.Value > 0))
                    {
                        return false;
                    }

                    return true;
                }
            }
        }
    }
}
