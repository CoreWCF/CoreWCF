// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel;
using CoreWCF.Description;
using DispatcherClient;
using Microsoft.Extensions.DependencyInjection;

namespace Extensibility
{
    internal static class ExtensibilityHelper
    {
        internal static ChannelFactory<TContract> CreateChannelFactory<TService, TContract>(IServiceBehavior serviceBehavior) where TService : class
        {
            return DispatcherHelper.CreateChannelFactory<TService, TContract>(
                        (IServiceCollection services) =>
                        {
                            services.AddSingleton(serviceBehavior);
                        });
        }

        internal static ChannelFactory<TContract> CreateChannelFactory<TService, TContract>(Action<CoreWCF.ServiceHostBase> configureServiceHostBase) where TService : class
        {
            return DispatcherHelper.CreateChannelFactory<TService, TContract>(
                        (IServiceCollection services) => { }, configureServiceHostBase);
        }
    }
}
