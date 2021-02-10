// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Configuration
{
    public static class ServiceBuilderExtensions
    {
        public static void ConfigureServiceHostBase<TService>(this IServiceBuilder builder, Action<ServiceHostBase> func) where TService : class
        {
            var serviceBuilder = builder as ServiceBuilder;
            ServiceConfigurationDelegateHolder<TService> holder = serviceBuilder.ServiceProvider
                .GetRequiredService<ServiceConfigurationDelegateHolder<TService>>();
            holder.AddConfigDelegate(func);
        }
    }
}