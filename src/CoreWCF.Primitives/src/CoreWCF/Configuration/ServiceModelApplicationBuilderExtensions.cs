using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using CoreWCF.Channels;
using System;
using System.Collections.Generic;
using System.Text;

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

            var serviceBuilder = app.ApplicationServices.GetRequiredService<ServiceBuilder>();
            configureServices(serviceBuilder);

            var transportMiddlewareTypes = new HashSet<Type>();
            foreach (var serviceConfig in serviceBuilder.ServiceConfigurations)
            {
                foreach (var serviceEndpoint in serviceConfig.Endpoints)
                {
                    var be = serviceEndpoint.Binding.CreateBindingElements();
                    var tbe = be.Find<TransportBindingElement>();
                    // TODO : Error handling if TBE doesn't exist
                    var transportMiddlewareType = tbe.MiddlewareType;
                    if (transportMiddlewareTypes.Contains(transportMiddlewareType))
                        continue;
                    transportMiddlewareTypes.Add(transportMiddlewareType);
                    string scheme = tbe.Scheme;
                    if ("http".Equals(scheme, StringComparison.OrdinalIgnoreCase) ||
                        "https".Equals(scheme, StringComparison.OrdinalIgnoreCase))
                    {
                        app.UseMiddleware(transportMiddlewareType, app);
                    }
                    else
                    {
                        // Not implemented yet. Commented code is for how things will work in the future.
                        //svc.UseMiddleware(transportMiddlewareType);
                    }

                }
            }

            return app;
        }
    }
}
