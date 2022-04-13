// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels
{
    internal class MetadataMiddleware
    {
        private const string RestorePathsDelegateItemName = nameof(MetadataMiddleware) + "_RestorePathsDelegate";
        private readonly IApplicationBuilder _app;
        private readonly IServiceBuilder _serviceBuilder;
        private readonly IDispatcherBuilder _dispatcherBuilder;
        private readonly ServiceMetadataBehavior _metadataBehavior;
        private readonly RequestDelegate _next;
        private readonly ILogger<MetadataMiddleware> _logger;
        private RequestDelegate _branch;
        private bool _branchBuilt;

        public MetadataMiddleware(RequestDelegate next, IApplicationBuilder app, IServiceBuilder serviceBuilder, IDispatcherBuilder dispatcherBuilder, ServiceMetadataBehavior metadataBehavior, ILogger<MetadataMiddleware> logger)
        {
            _app = app;
            _serviceBuilder = serviceBuilder;
            _dispatcherBuilder = dispatcherBuilder;
            _metadataBehavior = metadataBehavior;
            _next = next;
            _logger = logger;
            _branch = BuildBranchAndInvoke;
            serviceBuilder.Opened += ServiceBuilderOpenedCallback;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Update the path
            var path = context.Request.Path;
            var pathBase = context.Request.PathBase;
            context.Request.Path = pathBase.Add(path);
            context.Request.PathBase = "";
            Action restorePaths = () =>
            {
                context.Request.PathBase = pathBase;
                context.Request.Path = path;
            };
            context.Items[RestorePathsDelegateItemName] = restorePaths;
            await _branch(context);
        }

        private Task BuildBranchAndInvoke(HttpContext request)
        {
            EnsureBranchBuilt();
            return _branch(request);
        }

        private void EnsureBranchBuilt()
        {
            lock (this)
            {
                if (!_branchBuilt)
                {
                    _branch = BuildBranch();
                    _branchBuilt = true;
                }
            }
        }

        private void ServiceBuilderOpenedCallback(object sender, EventArgs e)
        {
            EnsureBranchBuilt();
        }

        private RequestDelegate BuildBranch()
        {
            _logger.LogDebug("Building branch map");
            IApplicationBuilder branchApp = _app.New();
            branchApp.Use(branchNext => {
                return reqContext =>
                {
                    if ("GET".Equals(reqContext.Request.Method, StringComparison.OrdinalIgnoreCase))
                    {
                        // If request is a GET request, continue on the branchApp middleware chain
                        // to handle requests for WSDL, HelpPage etc
                        return branchNext(reqContext);
                    }

                    // Not a GET request so short circuit to the next middleware after MetadataMiddleware.
                    if (reqContext.Items.TryGetValue(RestorePathsDelegateItemName, out object restorePathsDelegateAsObject))
                    {
                        (restorePathsDelegateAsObject as Action)?.Invoke();
                    }
                    else
                    {
                        _logger.LogWarning("RequestContext missing delegate with key " + RestorePathsDelegateItemName);
                    }

                    return _next(reqContext);
                };
            });

            foreach (Type serviceType in _serviceBuilder.Services)
            {
                void MapMetadata(IApplicationBuilder app, string path, Func<RequestDelegate, RequestDelegate> middleware)
                {
                    _logger.LogInformation($"Configuring metadata to {path}");
                    app.Use(middleware);
                }

                var dispatchers = _dispatcherBuilder.BuildDispatchers(serviceType);
                var serviceHost = _app.ApplicationServices.GetRequiredService(typeof(ServiceHostObjectModel<>).MakeGenericType(serviceType)) as ServiceHostBase;
                var metadataExtension = serviceHost.Extensions.Find<ServiceMetadataExtension>();
                if (metadataExtension == null)
                {
                    continue; // No ServiceMetadataExtension on this service
                }

                // Look for endpoints listening on HTTP and HTTPS and use them for fallback paths
                Uri fallbackHttpEndpointAddress = null;
                Uri fallbackHttpsEndpointAddress = null;
                foreach (var dispatcher in dispatchers)
                {
                    var scheme = dispatcher.BaseAddress?.Scheme;
                    if (fallbackHttpEndpointAddress == null && Uri.UriSchemeHttp.Equals(scheme))
                    {
                        fallbackHttpEndpointAddress = dispatcher.BaseAddress;
                        if (IPAddress.TryParse(fallbackHttpEndpointAddress.Host, out IPAddress hostAsAddress))
                        {
                            var uriBuilder = new UriBuilder(fallbackHttpEndpointAddress);
                            if (hostAsAddress.Equals(IPAddress.Any) || hostAsAddress.Equals(IPAddress.IPv6Any))
                            {
                                uriBuilder.Host = DnsCache.MachineName;
                                fallbackHttpEndpointAddress = uriBuilder.Uri;
                            }
                            else if (IPAddress.IsLoopback(hostAsAddress))
                            {
                                uriBuilder.Host = "localhost";
                                fallbackHttpEndpointAddress = uriBuilder.Uri;
                            }
                        }

                        continue;
                    }

                    if (fallbackHttpsEndpointAddress == null && Uri.UriSchemeHttps.Equals(scheme))
                    {
                        fallbackHttpsEndpointAddress = dispatcher.BaseAddress;
                        if (IPAddress.TryParse(fallbackHttpsEndpointAddress.Host, out IPAddress hostAsAddress))
                        {
                            var uriBuilder = new UriBuilder(fallbackHttpsEndpointAddress);
                            if (hostAsAddress.Equals(IPAddress.Any) || hostAsAddress.Equals(IPAddress.IPv6Any))
                            {
                                uriBuilder.Host = DnsCache.MachineName;
                                fallbackHttpsEndpointAddress = uriBuilder.Uri;
                            }
                            else if (IPAddress.IsLoopback(hostAsAddress))
                            {
                                uriBuilder.Host = "localhost";
                                fallbackHttpsEndpointAddress = uriBuilder.Uri;
                            }
                        }

                        continue;
                    }
                }

                Uri httpMetadataUri = null;
                Uri httpsMetadataUri = null;
                if (metadataExtension.HttpGetEnabled)
                {
                    if (metadataExtension.HttpGetUrl != null && metadataExtension.HttpGetUrl.AbsolutePath != "/")
                    {
                        httpMetadataUri = metadataExtension.HttpGetUrl;
                    }
                    else
                    {
                        httpMetadataUri = fallbackHttpEndpointAddress;
                        metadataExtension.HttpGetUrl = fallbackHttpEndpointAddress;
                    }

                    if(httpMetadataUri != null)
                    {
                        MapMetadata(branchApp, httpMetadataUri.AbsolutePath, metadataExtension.CreateMiddleware(httpMetadataUri, false));
                    }
                }

                if (metadataExtension.HttpHelpPageEnabled)
                {
                    Uri helpPageUri;
                    if (metadataExtension.HttpHelpPageUrl != null && metadataExtension.HttpHelpPageUrl.AbsolutePath != "/")
                    {
                        helpPageUri = metadataExtension.HttpHelpPageUrl;
                    }
                    else
                    {
                        helpPageUri = fallbackHttpEndpointAddress;
                        metadataExtension.HttpHelpPageUrl = fallbackHttpEndpointAddress;
                    }
                    if (helpPageUri != null && !helpPageUri.Equals(httpMetadataUri))
                    {
                        // Only map the help page uri if it's not null, and different from http metadata uri.
                        MapMetadata(branchApp, helpPageUri.AbsolutePath, metadataExtension.CreateMiddleware(helpPageUri, false));
                    }
                }

                if (metadataExtension.HttpsGetEnabled)
                {
                    if (metadataExtension.HttpsGetUrl != null && metadataExtension.HttpsGetUrl.AbsolutePath != "/")
                    {
                        httpsMetadataUri = metadataExtension.HttpsGetUrl;
                    }
                    else
                    {
                        httpsMetadataUri = fallbackHttpsEndpointAddress;
                        metadataExtension.HttpsGetUrl = fallbackHttpsEndpointAddress;
                    }

                    if (httpsMetadataUri != null)
                    {
                        MapMetadata(branchApp, httpsMetadataUri.AbsolutePath, metadataExtension.CreateMiddleware(httpsMetadataUri, true));
                    }
                }

                if (metadataExtension.HttpsHelpPageEnabled)
                {
                    Uri helpPageUri;
                    if (metadataExtension.HttpsHelpPageUrl != null && metadataExtension.HttpsHelpPageUrl.AbsolutePath != "/")
                    {
                        helpPageUri = metadataExtension.HttpsHelpPageUrl;
                    }
                    else
                    {
                        helpPageUri = fallbackHttpsEndpointAddress;
                        metadataExtension.HttpsHelpPageUrl = fallbackHttpsEndpointAddress;
                    }
                    if (helpPageUri != null && !helpPageUri.Equals(httpsMetadataUri))
                    {
                        // Only map the help page uri if it's not null, and different from https metadata uri.
                        MapMetadata(branchApp, helpPageUri.AbsolutePath, metadataExtension.CreateMiddleware(helpPageUri, true));
                    }
                }
            }

            branchApp.Use(_ => {
                return reqContext =>
                {
                    if (reqContext.Items.TryGetValue(RestorePathsDelegateItemName, out object restorePathsDelegateAsObject))
                    {
                        (restorePathsDelegateAsObject as Action)?.Invoke();
                    }
                    else
                    {
                        _logger.LogWarning("RequestContext missing delegate with key " + RestorePathsDelegateItemName);
                    }

                    return _next(reqContext);
                };
            });
            return branchApp.Build();
        }
    }
}
