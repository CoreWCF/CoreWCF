using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace CoreWCF.Channels
{
    internal static class HttpContextExtensions
    {
        private const string ContentTypeHeaderName = "Content-Type";

        internal static WebHeaderCollection ToWebHeaderCollection(this HttpRequest httpRequest)
        {
            var webHeaders = new WebHeaderCollection();
            foreach (var header in httpRequest.Headers)
            {
                webHeaders[header.Key] = header.Value;
            }

            webHeaders[ContentTypeHeaderName] = httpRequest.ContentType;

            return webHeaders;
        }
    }
}
