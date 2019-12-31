using CoreWCF.Description;
using DispatcherClient;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ServiceModel;

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
    }
}
