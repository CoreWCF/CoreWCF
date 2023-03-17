// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.ServiceModel.Web;
using System.Text.Json;
using CoreWCF.OpenApi;
using Xunit;

namespace CoreWCF.WebHttp.Tests;

public partial class OpenApiWcfTests
{
    private interface IFrameworkResponse
    {
        [WebGet(UriTemplate = "/get")]
        public string GetOperation();
        [WebInvoke(Method = "POST", UriTemplate = "/post")]
        public string PostOperation();
    }

    [Fact]
    public void FrameworkGetResponseAdded()
    {
        JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IFrameworkResponse) });

        json
            .GetProperty("paths")
            .GetProperty("/get")
            .GetProperty("get")
            .GetProperty("responses")
            .GetProperty("200");
    }

    [Fact]
    public void FrameworkPostResponseAdded()
    {
        JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IFrameworkResponse) });

        json
            .GetProperty("paths")
            .GetProperty("/post")
            .GetProperty("post")
            .GetProperty("responses")
            .GetProperty("200");
    }
}
#endif
