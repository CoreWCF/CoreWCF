// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace CoreWCF.Configuration
{
    public class ServiceModelOptions
    {
        private readonly Dictionary<Type, ServiceConfigurationBuilder> _configBuilders = new Dictionary<Type, ServiceConfigurationBuilder>();
        public void ConfigureService(Type serviceType, Action<ServiceConfigurationBuilder> configure)
        {
            if (!_configBuilders.TryGetValue(serviceType, out ServiceConfigurationBuilder configBuilder))
            {
                configBuilder = new ServiceConfigurationBuilder(serviceType);
                _configBuilders[serviceType] = configBuilder;
            }
            configBuilder.Configure(configure);
        }
        internal void ConfigureServiceBuilder(IServiceBuilder serviceBuilder)
        {
            foreach (var serviceConfigBuilder in _configBuilders.Values)
            {
                serviceConfigBuilder.ConfigureServiceBuilder(serviceBuilder);
            }
        }
    }
}
