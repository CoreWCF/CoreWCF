using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceModel.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel.Channels
{
    public partial class ServiceModelHttpMiddleware
    {
        private IServiceBuilder _serviceBuilder;
        private RequestDelegate _next;
        private readonly RequestDelegate _branch;

        public ServiceModelHttpMiddleware(RequestDelegate next, IApplicationBuilder app, IServiceBuilder serviceBuilder, IDispatcherBuilder dispatcherBuilder)
        {
            _serviceBuilder = serviceBuilder;
            _next = next;
            _branch = BuildBranch(app, _serviceBuilder, dispatcherBuilder);
        }

        public Task InvokeAsync(HttpContext context)
        {
            return _branch(context);
        }

        private RequestDelegate BuildBranch(IApplicationBuilder app, IServiceBuilder serviceBuilder, IDispatcherBuilder dispatcherBuilder)
        {
            var branchApp = app.New();
            var serverAddresses = app.ServerFeatures.Get<IServerAddressesFeature>();
            foreach (var address in serverAddresses.Addresses)
            {
                serviceBuilder.BaseAddresses.Add(new Uri(address));
            }

            foreach (var serviceType in serviceBuilder.Services)
            {
                var dispatchers = dispatcherBuilder.BuildDispatchers(serviceType);
                foreach (var dispatcher in dispatchers)
                {
                    if (dispatcher.BaseAddress == null)
                    {
                        // TODO: Should we throw? Ignore?
                        continue;
                    }

                    var scheme = dispatcher.BaseAddress?.Scheme;
                    if (!"http".Equals(scheme, StringComparison.OrdinalIgnoreCase) &&
                        !"https".Equals(scheme, StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Not an HTTP(S) dispatcher
                    }

                    bool matching = false;
                    foreach (var serverAddress in serverAddresses.Addresses)
                    {
                        // TODO: Might not be necessary to compare paths
                        var serverUri = new Uri(serverAddress);
                        var serverAddressNormalized = string.Join(':',
                            serverUri.GetComponents(UriComponents.Port | UriComponents.Path, UriFormat.SafeUnescaped));
                        var dispatcherAddressNormalized = string.Join(':',
                            dispatcher.BaseAddress.GetComponents(UriComponents.Port | UriComponents.Path, UriFormat.SafeUnescaped));
                        if (dispatcherAddressNormalized.StartsWith(serverAddressNormalized, StringComparison.OrdinalIgnoreCase))
                        {
                            matching = true;
                            break; // Dispatcher address is based on server listening address;
                        }
                    }

                    if (matching)
                    {
                        branchApp.Map(dispatcher.BaseAddress.AbsolutePath, wcfApp =>
                        {
                            var servicesScopeFactory = wcfApp.ApplicationServices.GetRequiredService<IServiceScopeFactory>();
                            wcfApp.Run(new RequestDelegateHandler(dispatcher, servicesScopeFactory).HandleRequest);
                        });
                    }
                }
            }

            branchApp.Use(_ => { return context => _next(context); });
            return branchApp.Build();
        }
    }
}
