// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Threading.Tasks;

namespace ServiceContract;

[ServiceContract]
public interface IFrameworkService
{
    [WebGet(UriTemplate = "/async/get", BodyStyle = WebMessageBodyStyle.Bare, ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json)]
    [OperationContract]
    Task<AsyncData> AsyncWebGet();

    [WebInvoke(Method = "POST", UriTemplate = "/async/post", BodyStyle = WebMessageBodyStyle.Bare, ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json)]
    [OperationContract]
    Task<AsyncData> AsyncWebInvoke(AsyncData body);

    [WebGet(UriTemplate = "/hello", BodyStyle = WebMessageBodyStyle.Bare, ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json)]
    [OperationContract]
    public string SyncWebGet();

    [WebGet(UriTemplate = "/implicitFormat")]
    [OperationContract]
    public Task<AsyncData> ImplicitlySetFormat();
}

#endif
