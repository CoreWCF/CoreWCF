// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using CoreWCF.Description;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoreWCF.Configuration
{
    public static class MetadataServiceCollectionExtensions
    {
        public static IServiceCollection AddServiceModelMetadata(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            int count = services.Count;
            int existingTransportServiceBuilderIndex = -1;
            for (int i = 0; i < count; i++)
            {
                ServiceDescriptor service = services[i];
                if (service.ServiceType == typeof(ITransportServiceBuilder))
                {
                    existingTransportServiceBuilderIndex = i;
                    break;
                }
            }

            var serviceDescriptor = ServiceDescriptor.Singleton<ITransportServiceBuilder, MetadataTransportServiceBuilder>();
            if (existingTransportServiceBuilderIndex == -1)
            {
                // No existing ITransportServiceBuilder instances so can place at end
                services.Add(serviceDescriptor);
            }
            else
            {
                // There's already an ITransportServiceBuidler and metadata needs to be inserted first
                // Otherwise an HTTP based binding could capture requests meant for metadata.
                services.Insert(existingTransportServiceBuilderIndex, serviceDescriptor);
            }

            services.AddSingleton<ServiceMetadataBehavior>();
            services.AddSingleton<IServiceBehavior>(serviceProvider => serviceProvider.GetRequiredService<ServiceMetadataBehavior>());
            return services;
        }
    }
}
