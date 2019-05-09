using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.ServiceModel.Configuration
{
    internal class DispatcherBuilderImpl : IDispatcherBuilder
    {
        private IServiceProvider _services;

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
