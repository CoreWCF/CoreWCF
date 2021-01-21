// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Description;

namespace CoreWCF.Configuration
{
    internal class ServiceConfiguration<TService> : IServiceConfiguration<TService> where TService : class
    {
        private readonly ServiceBuilder _serviceBuilder;
        private readonly IServiceProvider _services;
        private List<IServiceDispatcher> _dispatchers;

        public ServiceConfiguration(ServiceBuilder serviceBuilder, IServiceProvider services)
        {
            _serviceBuilder = serviceBuilder;
            _services = services;
        }

        public Type ServiceType => typeof(TService);

        public List<ServiceEndpointConfiguration> Endpoints { get; } = new List<ServiceEndpointConfiguration>();

        public List<IServiceDispatcher> GetDispatchers()
        {
            if (_dispatchers == null)
            {
                _dispatchers = DispatcherBuilder.BuildDispatcher<TService>(this, _services);
            }

            return _dispatchers;
        }
    }
}
