// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

            var loggerFactory = app.ApplicationServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger(nameof(ServiceModelApplicationBuilderExtensions));
            var serviceBuilder = app.ApplicationServices.GetRequiredService<ServiceBuilder>();
            configureServices(serviceBuilder);

            var transportServiceBuilders = app.ApplicationServices.GetServices<ITransportServiceBuilder>();
            var transportServiceBuilderSeenTypes = new HashSet<Type>();
            foreach (var transportServiceBuilder in transportServiceBuilders)
            {
                if (!transportServiceBuilderSeenTypes.Contains(transportServiceBuilder.GetType()))
                {
                    logger.LogDebug($"Calling {transportServiceBuilder.GetType().FullName}.Configure");
                    transportServiceBuilder.Configure(app);
                    transportServiceBuilderSeenTypes.Add(transportServiceBuilder.GetType());
                }
            }

            foreach (var serviceConfig in serviceBuilder.ServiceConfigurations)
            {
                foreach (var serviceEndpoint in serviceConfig.Endpoints)
                {
                    var transportServiceBuilder = serviceEndpoint.Binding.GetProperty<ITransportServiceBuilder>(new BindingParameterCollection());
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
