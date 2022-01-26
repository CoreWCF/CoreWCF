// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF;
using CoreWCF.Web;

namespace ServiceContract
{
    [ServiceContract]
    internal interface IAsyncService
    {
        [WebGet(UriTemplate = "/async/get", BodyStyle = WebMessageBodyStyle.Bare, ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json)]
        Task<AsyncData> AsyncWebGet();

        [WebInvoke(Method = "POST", UriTemplate = "/async/post", BodyStyle = WebMessageBodyStyle.Bare, ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json)]
        Task<AsyncData> AsyncWebInvoke(AsyncData body);
    }
}
