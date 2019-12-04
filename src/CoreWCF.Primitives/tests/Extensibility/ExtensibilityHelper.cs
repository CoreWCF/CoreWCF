using CoreWCF.Description;
using DispatcherClient;
using Extensibility;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ServiceModel;

namespace Extensibility
{
    internal static class ExtensibilityHelper
    {
        internal static string s_endpointAddress = "corewcf://localhost/Service.svc";

        internal static ChannelFactory<TContract> CreateChannelFactory<TService, TContract>(IServiceBehavior serviceBehavior) where TService : class
        {
            return CreateChannelFactory<TService, TContract>(
                        (IServiceCollection services) =>
                        {
                            services.AddSingleton(serviceBehavior);
                        });
        }

        internal static ChannelFactory<TContract> CreateChannelFactory<TService, TContract>() where TService : class
        {
            return CreateChannelFactory<TService, TContract>((Action<IServiceCollection>)null);
        }

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
    }
}
