// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;
using CoreWCF.Web;

namespace ServiceContract
{
    [ServiceContract]
    public interface IWebOperationContextService
    {
        [WebGet(UriTemplate = "/statuscode")]
        void SetStatusCode();

        [WebGet(UriTemplate = "/responseheader")]
        void AddResponseHeader();

        [WebGet(UriTemplate = "/contenttype")]
        string SetContentType();

        [WebGet(UriTemplate = "/match", BodyStyle = WebMessageBodyStyle.Bare, ResponseFormat = WebMessageFormat.Json)]
        string InspectRouteMatch();
    }
}
