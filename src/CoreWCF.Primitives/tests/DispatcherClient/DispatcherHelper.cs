// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel;
using Helpers;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DispatcherClient
{
    internal static class DispatcherHelper
    {
        internal static string s_endpointAddress = "corewcf://localhost/Service.svc";

        internal static ChannelFactory<TContract> CreateChannelFactory<TService, TContract>(Action<IServiceCollection> configure, Action<CoreWCF.ServiceHostBase> configureServiceHostBase = default) where TService : class
        {
            var binding = new DispatcherBinding<TService, TContract>((services) =>
            {
                configure?.Invoke(services);
                IServerAddressesFeature serverAddressesFeature = new ServerAddressesFeature();
                serverAddressesFeature.Addresses.Add(new Uri(s_endpointAddress).GetLeftPart(UriPartial.Authority) + "/");
                services.AddSingleton(serverAddressesFeature);
                services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
                services.RegisterApplicationLifetime();
            }, configureServiceHostBase);
            return new ChannelFactory<TContract>(binding, new EndpointAddress(s_endpointAddress));
        }

        internal static ChannelFactory<TContract> CreateChannelFactory<TService, TContract>() where TService : class
        {
            return CreateChannelFactory<TService, TContract>(null);
        }
    }
}
