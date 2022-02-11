// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Description;
using CoreWCF.Runtime;

namespace CoreWCF.Configuration
{
    internal class ServiceConfiguration<TService> : IServiceConfiguration<TService> where TService : class
    {
        private readonly ServiceBuilder _serviceBuilder;
        private readonly IServiceProvider _services;
        private List<IServiceDispatcher> _dispatchers;
        private object _lock = new object();

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
                // The services can't be Open'd until after ASP.NET Core is running and listening for requests. The Open call
                // is started and allowed to run in the background. If a request comes in before the dispatchers have finished
                // being built, there are multiple problems which can occur such as adding an endpoint twice, or concurrent
                // modification of collections, or trying to modify the service host after it has been opened.
                // This double check lock ensures this doesn't happen.
                lock (_lock)
                {
                    if (_dispatchers == null)
                    {
                        _dispatchers = DispatcherBuilder.BuildDispatcher<TService>(this, _services);
                    }
                }
            }

            return _dispatchers;
        }
    }
}
