// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Kafka.Tests.Helpers;

internal class DependencyResolverHelper
{
    private readonly IServiceProvider _serviceProvider;

    public DependencyResolverHelper(IWebHost webHost)
    {
        _serviceProvider = webHost.Services;
    }

#if NET6_0_OR_GREATER
    public DependencyResolverHelper(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
#endif

    public T GetService<T>()
    {
        using IServiceScope serviceScope = _serviceProvider.CreateScope();
        IServiceProvider services = serviceScope.ServiceProvider;
        T scopedService = services.GetRequiredService<T>();
        return scopedService;
    }
}
