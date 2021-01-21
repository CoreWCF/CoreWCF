// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Configuration
{
    internal class DispatcherBuilderImpl : IDispatcherBuilder
    {
        private readonly IServiceProvider _services;

        public DispatcherBuilderImpl(IServiceProvider services)
        {
            _services = services;
        }

        public List<IServiceDispatcher> BuildDispatchers(Type serviceType)
        {
            var serviceConfigInterface = typeof(IServiceConfiguration<>);
            var serviceConfig = (IServiceConfiguration)_services.GetRequiredService(serviceConfigInterface.MakeGenericType(serviceType));
            return serviceConfig.GetDispatchers();
        }
    }
}
