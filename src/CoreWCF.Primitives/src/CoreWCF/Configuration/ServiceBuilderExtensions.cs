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

        public static void ConfigureServiceHostBase(this IServiceBuilder builder, Type serviceType, Action<ServiceHostBase> func)
        {
            if (serviceType is null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(serviceType)));
            }

            if (!serviceType.IsClass)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.ConfigureServiceHostBaseTypeMustBeClass, serviceType.FullName), nameof(serviceType)));
            }

            var serviceBuilder = builder as ServiceBuilder;
            ServiceConfigurationDelegateHolder holder = (ServiceConfigurationDelegateHolder)serviceBuilder.ServiceProvider
                .GetRequiredService(typeof(ServiceConfigurationDelegateHolder<>).MakeGenericType(serviceType));
            holder.AddConfigDelegate(func);
        }

        public static void ConfigureAllServiceHostBase(this IServiceBuilder builder, Action<ServiceHostBase> func)
        {
            foreach (Type service in builder.Services)
            {
                builder.ConfigureServiceHostBase(service, func);
            }
        }
    }
}
