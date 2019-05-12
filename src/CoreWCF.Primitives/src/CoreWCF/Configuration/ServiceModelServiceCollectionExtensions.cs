using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Configuration
{
    public static class ServiceModelServiceCollectionExtensions
    {
        public static IServiceCollection AddServiceModelServices(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<ServiceBuilder>();
            services.AddSingleton<IServiceBuilder>(provider => provider.GetRequiredService<ServiceBuilder>());
            services.TryAddSingleton(typeof(IServiceConfiguration<>), typeof(ServiceConfiguration<>));
            services.TryAddSingleton<IDispatcherBuilder, DispatcherBuilderImpl>();
            services.AddScoped<ReplyChannelBinder>();
            services.AddScoped<DuplexChannelBinder>();
            services.AddScoped<ServiceChannel.SessionIdleManager>();

            return services;
        }
    }
}
