// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using CoreWCF.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreWCF.Configuration
{
    public static class ServiceModelApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseServiceModel(this IApplicationBuilder app, Action<IServiceBuilder> configureServices)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            if (configureServices == null)
            {
                throw new ArgumentNullException(nameof(configureServices));
            }

            ServiceBuilder serviceBuilder = app.ApplicationServices.GetRequiredService<ServiceBuilder>();          
            configureServices(serviceBuilder);
            return UseServiceModel(app);
        }
       
        public static IApplicationBuilder UseServiceModel(this IApplicationBuilder app)
        {
            ServiceBuilder serviceBuilder = app.ApplicationServices.GetRequiredService<ServiceBuilder>();
            ILoggerFactory loggerFactory = app.ApplicationServices.GetRequiredService<ILoggerFactory>();
            ILogger logger = loggerFactory.CreateLogger(typeof(ServiceModelApplicationBuilderExtensions));

            var options = app.ApplicationServices.GetService<IOptions<ServiceModelOptions>>();
            var serviceModelOptions = options.Value ?? new ServiceModelOptions();
            serviceModelOptions.ConfigureServiceBuilder(serviceBuilder);

            IEnumerable<ITransportServiceBuilder> transportServiceBuilders = app.ApplicationServices.GetServices<ITransportServiceBuilder>();
            var transportServiceBuilderSeenTypes = new HashSet<Type>();
            foreach (ITransportServiceBuilder transportServiceBuilder in transportServiceBuilders)
            {
                if (!transportServiceBuilderSeenTypes.Contains(transportServiceBuilder.GetType()))
                {
                    logger.LogDebug("Calling {builderType}.Configure", transportServiceBuilder.GetType().FullName);
                    transportServiceBuilder.Configure(app);
                    transportServiceBuilderSeenTypes.Add(transportServiceBuilder.GetType());
                }
            }

            foreach (IServiceConfiguration serviceConfig in serviceBuilder.ServiceConfigurations)
            {
                foreach (ServiceEndpointConfiguration serviceEndpoint in serviceConfig.Endpoints)
                {
                    ITransportServiceBuilder transportServiceBuilder = serviceEndpoint.Binding.GetProperty<ITransportServiceBuilder>(new BindingParameterCollection());
                    // Check if this transport service builder type has already been used in this app
                    if (transportServiceBuilder != null && !transportServiceBuilderSeenTypes.Contains(transportServiceBuilder.GetType()))
                    {
                        logger.LogDebug("Calling {builderType}.Configure", transportServiceBuilder.GetType().FullName);
                        transportServiceBuilder.Configure(app);
                        transportServiceBuilderSeenTypes.Add(transportServiceBuilder.GetType());
                    }
                }
            }

            return app;
        }

        public static IHost UseServiceModel(this IHost host, Action<IServiceBuilder> configureServices)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            host.Services.UseServiceModel(configureServices);
            return host;
        }

        public static IServiceProvider UseServiceModel(this IServiceProvider serviceProvider, Action<IServiceBuilder> configureServices)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (configureServices == null)
            {
                throw new ArgumentNullException(nameof(configureServices));
            }

            ServiceBuilder serviceBuilder = serviceProvider.GetService<ServiceBuilder>();
            configureServices(serviceBuilder);
            return UseServiceModel(serviceProvider);
        }

        public static IServiceProvider UseServiceModel(this IServiceProvider serviceProvider)
        {
            ServiceBuilder serviceBuilder = serviceProvider.GetRequiredService<ServiceBuilder>();
            ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            var options = serviceProvider.GetService<IOptions<ServiceModelOptions>>();
            var serviceModelOptions = options.Value ?? new ServiceModelOptions();
            serviceModelOptions.ConfigureServiceBuilder(serviceBuilder);

            IEnumerable<ITransportServiceBuilder> transportServiceBuilders = serviceProvider.GetServices<ITransportServiceBuilder>();
            if (transportServiceBuilders.Count() > 0)
            {
                throw new InvalidOperationException(SR.WebHostRequired);
            }

            foreach (IServiceConfiguration serviceConfig in serviceBuilder.ServiceConfigurations)
            {
                foreach (ServiceEndpointConfiguration serviceEndpoint in serviceConfig.Endpoints)
                {
                    ITransportServiceBuilder transportServiceBuilder = serviceEndpoint.Binding.GetProperty<ITransportServiceBuilder>(new BindingParameterCollection());
                    if (transportServiceBuilder != null)
                    {
                        throw new InvalidOperationException(SR.WebHostRequired);
                    }
                }
            }

            return serviceProvider;
        }
    }
}
