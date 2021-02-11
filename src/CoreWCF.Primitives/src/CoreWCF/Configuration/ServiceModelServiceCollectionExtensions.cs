// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
            services.AddSingleton<WrappingIServer>();
            for (int i = 0; i < services.Count; i++)
            {
                if (services[i].ServiceType == typeof(IServer))
                {
                    if (services[i].ImplementationType != null)
                    {
                        Type implType = services[i].ImplementationType;
                        if (!services.Any(d => d.ServiceType == implType))
                        {
                            services.AddSingleton(implType);
                        }
                        services[i] = ServiceDescriptor.Singleton<IServer>((provider) =>
                            {
                                var originalIServer = (IServer)provider.GetRequiredService(implType);
                                WrappingIServer wrappingServer = provider.GetRequiredService<WrappingIServer>();
                                wrappingServer.InnerServer = originalIServer;
                                return wrappingServer;
                            });
                    }
                    else if (services[i].ImplementationInstance != null)
                    {
                        object implInstance = services[i].ImplementationInstance;;
                        Type implType = implInstance.GetType();
                        if (!services.Any(d => d.ServiceType == implType))
                        {
                            services.AddSingleton(implType, implInstance);
                        }
                        services[i] = ServiceDescriptor.Singleton<IServer>((provider) =>
                        {
                            var originalIServer = (IServer)provider.GetRequiredService(implType);
                            WrappingIServer wrappingServer = provider.GetRequiredService<WrappingIServer>();
                            wrappingServer.InnerServer = originalIServer;
                            return wrappingServer;
                        });
                    }
                }
            }
            services.AddSingleton<ServiceBuilder>();
            services.AddSingleton<IServiceBuilder>(provider => provider.GetRequiredService<ServiceBuilder>());
            services.AddSingleton<IServiceBehavior>(provider => provider.GetRequiredService<ServiceAuthorizationBehavior>());
            services.AddSingleton<ServiceAuthorizationBehavior>(provider =>
            {
                var behavior = new ServiceAuthorizationBehavior();
                ServiceAuthorizationManager manager = provider.GetService<ServiceAuthorizationManager>();
                if (manager != null)
                {
                    behavior.ServiceAuthorizationManager = manager;
                }
                return behavior;
            });
            services.TryAddSingleton(typeof(IServiceConfiguration<>), typeof(ServiceConfiguration<>));
            services.TryAddSingleton<IDispatcherBuilder, DispatcherBuilderImpl>();
            services.AddSingleton(typeof(ServiceConfigurationDelegateHolder<>));
            services.AddScoped<ReplyChannelBinder>();
            services.AddScoped<DuplexChannelBinder>();
            services.AddScoped<InputChannelBinder>();
            services.AddScoped<ServiceChannel.SessionIdleManager>();
            services.AddSingleton(typeof(ServiceHostObjectModel<>));
            services.AddSingleton(typeof(TransportCompressionSupportHelper));
            return services;
        }
    }
}
