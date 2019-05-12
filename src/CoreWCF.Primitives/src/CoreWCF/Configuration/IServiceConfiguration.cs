using CoreWCF.Channels;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Configuration
{
    internal interface IServiceConfiguration<TService> : IServiceConfiguration where TService : class
    {

    }

    internal interface IServiceConfiguration
    {
        List<ServiceEndpointConfiguration> Endpoints { get; }
        List<IServiceDispatcher> GetDispatchers();
        Type ServiceType { get; }
    }
}
