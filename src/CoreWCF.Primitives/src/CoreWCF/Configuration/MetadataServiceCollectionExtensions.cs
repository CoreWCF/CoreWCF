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

            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITransportServiceBuilder, MetadataTransportServiceBuilder>());
            services.AddSingleton<ServiceMetadataBehavior>();
            services.AddSingleton<IServiceBehavior>(serviceProvider => serviceProvider.GetRequiredService<ServiceMetadataBehavior>());
            return services;
        }
    }
}
