// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using ServiceContract;

#if NETFRAMEWORK
namespace Services;

public class FrameworkService : IFrameworkService
{
    public Task<AsyncData> AsyncWebGet() => Task.FromResult(new AsyncData { Data = "async" });

    public Task<AsyncData> AsyncWebInvoke(AsyncData body) => Task.FromResult(body);

    public string SyncWebGet() => "Hello World";
}

#endif
