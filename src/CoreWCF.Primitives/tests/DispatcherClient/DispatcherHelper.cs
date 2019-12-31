using CoreWCF.Description;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ServiceModel;

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
