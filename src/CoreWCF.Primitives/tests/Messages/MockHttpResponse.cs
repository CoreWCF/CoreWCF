// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace CoreWCF.Primitives.Tests.Messages;

public class MockHttpResponse : HttpResponse
{
    public override void OnStarting(Func<object, Task> callback, object state) => throw new NotImplementedException();

    public override void OnCompleted(Func<object, Task> callback, object state) => throw new NotImplementedException();

    public override void Redirect(string location, bool permanent) => throw new NotImplementedException();

    public override HttpContext HttpContext { get; }
    public override int StatusCode { get; set; }
    public override IHeaderDictionary Headers { get; }
    public override Stream Body { get; set; } = new MemoryStream();
    public override long? ContentLength { get; set; }
    public override string ContentType { get; set; }
    public override IResponseCookies Cookies { get; }
    public override bool HasStarted { get; }
}
