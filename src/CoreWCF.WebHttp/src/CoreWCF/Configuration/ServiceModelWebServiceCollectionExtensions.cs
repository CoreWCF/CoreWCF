// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using CoreWCF.Description;
using CoreWCF.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace CoreWCF.Configuration
{
    public static class ServiceModelWebServiceCollectionExtensions
    {
        public static IServiceCollection AddServiceModelWebServices(this IServiceCollection services, Action<OpenApiOptions> configure = null)
        {
            bool alreadyAdded = services.Any(s => s.ServiceType == typeof(IServiceBuilder));
            if (!alreadyAdded)
            {
                services.AddServiceModelServices();
            }
                
            services.AddSingleton<IServiceBehavior, WebHttpServiceBehavior>();
            services.AddSingleton<OpenApiDocumentProvider>();
            services.AddSingleton<ISwaggerProvider>(provider => provider.GetRequiredService<OpenApiDocumentProvider>());

            if (configure != null)
            {
                services.Configure<OpenApiOptions>(o => configure(o));
            }
                
            return services;
        }
    }
}
