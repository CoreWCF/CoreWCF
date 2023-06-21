// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Description;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Configuration;

internal class ServiceAuthorizationBehaviorFactory
{
    private readonly IServiceProvider _serviceProvider;

    private ServiceAuthorizationBehavior _singleton;

    public ServiceAuthorizationBehaviorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ServiceAuthorizationBehavior GetOrCreate()
    {
        if (_singleton == null)
        {
            _singleton = new ServiceAuthorizationBehavior();
            ServiceAuthorizationManager manager = _serviceProvider.GetService<ServiceAuthorizationManager>();
            if (manager != null)
            {
                _singleton.ServiceAuthorizationManager = manager;
            }
            IServiceScopeFactory serviceScopeFactory = _serviceProvider.GetService<IServiceScopeFactory>();
            _singleton.ServiceScopeFactory = serviceScopeFactory;
            return _singleton;
        }

        return _singleton;
    }
}
