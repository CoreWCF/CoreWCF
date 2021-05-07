// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Description;
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

        public static void AddServiceEndpoint(this IServiceBuilder builder, string name)
        {
            var serviceBuilder = builder as ServiceBuilder;
            IConfigurationHolder configHolder = serviceBuilder.ServiceProvider.GetService<IConfigurationHolder>();
            configHolder.Initialize();
            IXmlConfigEndpoint endpoint = configHolder.GetXmlConfigEndpoint(name);
         
            serviceBuilder.AddServiceEndpoint(endpoint.Service, endpoint.Contract, endpoint.Binding, endpoint.Address, null);
        }
    }
}
