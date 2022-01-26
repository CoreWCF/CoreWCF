// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;
using CoreWCF.Web;

namespace ServiceContract
{
    [ServiceContract]
    internal interface ISslService
    {
        [WebGet(UriTemplate = "/hello", BodyStyle = WebMessageBodyStyle.Bare, ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json)]
        public string Hello();
    }
}
