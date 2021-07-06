// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace CoreWCF.Configuration
{
    public class ServiceModelOptions
    {
        private readonly Dictionary<Type, ServiceConfigurationBuilder> _config = new Dictionary<Type, ServiceConfigurationBuilder>();
        public void ConfigureService(Type serviceType, Action<ServiceConfigurationBuilder> configure)
        {
            if (!_config.TryGetValue(serviceType, out ServiceConfigurationBuilder configBuilder))
            {
                configBuilder = new ServiceConfigurationBuilder(serviceType);
                _config[serviceType] = configBuilder;
            }
            configBuilder.AddConfigureDelegate(configure);
        }
        internal void ConfigureServiceBuilder(IServiceBuilder serviceBuilder)
        {
            foreach (var serviceConfigBuilder in _config.Values)
            {
                serviceConfigBuilder.ConfigureServiceBuilder(serviceBuilder);
            }
        }
    }
}
