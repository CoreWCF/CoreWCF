using Microsoft.Extensions.DependencyInjection;
using System;

namespace CoreWCF.Configuration
{
    public static class ServiceBuilderExtensions
    {
        public static void ConfigureServiceHostBase<TService>(this IServiceBuilder builder, Action<ServiceHostBase> func) where TService : class
        {
            var serviceBuilder = builder as ServiceBuilder;
            var holder = serviceBuilder.ServiceProvider
                .GetRequiredService<ServiceConfigurationDelegateHolder<TService>>();
            holder.AddConfigDelegate(func);
        }
    }
}
