// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.ConfigurationManager.Tests
{
    class DependencyResolverHelper
    {
        private readonly IHost _webHost;
        public DependencyResolverHelper(IHost webHost)
        {
            _webHost = webHost;
        }

        public T GetService<T>()
        {
            using (IServiceScope serviceScope = _webHost.Services.CreateScope())
            {
                IServiceProvider services = serviceScope.ServiceProvider;

                T scopedService = services.GetRequiredService<T>();
                return scopedService;
            }
        }
    }
}
