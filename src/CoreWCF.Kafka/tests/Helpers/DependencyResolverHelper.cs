// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Kafka.Tests.Helpers;

internal class DependencyResolverHelper
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWebHost _webHost;

    public DependencyResolverHelper(IWebHost webHost)
    {
        _webHost = webHost;
    }

#if NET6_0_OR_GREATER
    public DependencyResolverHelper(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
#endif

    public T GetService<T>()
    {
        if(_serviceProvider != null)
        {
            return _serviceProvider.GetRequiredService<T>();
        }

        using IServiceScope serviceScope = _serviceProvider != null
            ? _serviceProvider.CreateScope()
            : _webHost.Services.CreateScope();
        IServiceProvider services = serviceScope.ServiceProvider;
        T scopedService = services.GetRequiredService<T>();
        return scopedService;
    }
}
