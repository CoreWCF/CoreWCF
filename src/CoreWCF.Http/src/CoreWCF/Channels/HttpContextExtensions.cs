// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Microsoft.AspNetCore.Http;

namespace CoreWCF.Channels
{
    internal static class HttpContextExtensions
    {
        private const string ContentTypeHeaderName = "Content-Type";
        
        internal static WebHeaderCollection ToWebHeaderCollection(this HttpRequest httpRequest)
        {
            var webHeaders = new WebHeaderCollection();
            foreach (System.Collections.Generic.KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> header in httpRequest.Headers)
            {
                if (header.Key.StartsWith(":")) // HTTP/2 pseudo header, skip as they appear on other properties of HttpRequest
                    continue;

                webHeaders[header.Key] = header.Value;
            }

            webHeaders[ContentTypeHeaderName] = httpRequest.ContentType;

            return webHeaders;
        }
    }
}
