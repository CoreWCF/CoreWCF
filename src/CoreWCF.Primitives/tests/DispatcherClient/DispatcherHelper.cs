// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel;
using CoreWCF.Description;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherClient
{
    internal static class DispatcherHelper
    {
        internal static string s_endpointAddress = "corewcf://localhost/Service.svc";

        internal static ChannelFactory<TContract> CreateChannelFactory<TService, TContract>(Action<IServiceCollection> configure) where TService : class
        {
            var binding = new DispatcherBinding<TService, TContract>((services) =>
            {
                configure?.Invoke(services);
                IServerAddressesFeature serverAddressesFeature = new ServerAddressesFeature();
                serverAddressesFeature.Addresses.Add(new Uri(s_endpointAddress).GetLeftPart(UriPartial.Authority) + "/");
                services.AddSingleton(serverAddressesFeature);
            });
            return new ChannelFactory<TContract>(binding, new EndpointAddress(s_endpointAddress));
        }

        internal static ChannelFactory<TContract> CreateChannelFactory<TService, TContract>() where TService : class
        {
            return CreateChannelFactory<TService, TContract>(null);
        }
    }
}
