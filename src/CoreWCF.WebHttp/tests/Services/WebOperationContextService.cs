// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using CoreWCF.Web;

namespace Services
{
    public class WebOperationContextService : ServiceContract.IWebOperationContextService
    {
        public void SetStatusCode()
        {
            WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.Accepted;
        }

        public void AddResponseHeader()
        {
            WebOperationContext.Current.OutgoingResponse.Headers.Add("TestHeader", "test");
        }

        public string SetContentType()
        {
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/plain";
            return "test";
        }

        public string InspectRouteMatch() => WebOperationContext.Current.IncomingRequest.UriTemplateMatch.RequestUri.ToString();
    }
}
