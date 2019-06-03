using CoreWCF.Channels;
using CoreWCF.Description;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Configuration
{
    internal class ServiceConfiguration<TService> : IServiceConfiguration<TService> where TService : class
    {
        private ServiceBuilder _serviceBuilder;
        private IServiceProvider _services;
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
