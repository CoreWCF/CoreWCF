// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

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
