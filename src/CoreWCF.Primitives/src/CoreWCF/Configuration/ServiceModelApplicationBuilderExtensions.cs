// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
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
                    logger.LogDebug($"Calling {transportServiceBuilder.GetType().FullName}.Configure");
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
                        //Console.WriteLine($"Found ITransportServiceBuilder of type {transportServiceBuilder.GetType().FullName}");
                        logger.LogDebug($"Calling {transportServiceBuilder.GetType().FullName}.Configure");
                        transportServiceBuilder.Configure(app);
                        transportServiceBuilderSeenTypes.Add(transportServiceBuilder.GetType());
                    }
                }
            }

            return app;
        }
    }
}
