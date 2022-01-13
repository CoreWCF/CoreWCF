// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels
{
    internal class MetadataMiddleware
    {
        private const string RestorePathsDelegateItemName = nameof(MetadataMiddleware) + "_RestorePathsDelegate";
        private IApplicationBuilder _app;
        private readonly IServiceBuilder _serviceBuilder;
        private readonly IDispatcherBuilder _dispatcherBuilder;
        private ServiceMetadataBehavior _metadataBehavior;
        private RequestDelegate _next;
        private ILogger<MetadataMiddleware> _logger;
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
                void MapMetadata(IApplicationBuilder app, string path, Action<IApplicationBuilder> configure)
                {
                    if (path.EndsWith("/"))
                    {
                        path = path.Substring(0, path.Length - 1);
                    }

                    if (string.IsNullOrEmpty(path) )
                    {
                        configure(app);
                    }
                    else
                    {
                        app.Map(path, configure);
                    }
                }

                System.Collections.Generic.List<IServiceDispatcher> dispatchers = _dispatcherBuilder.BuildDispatchers(serviceType);
                foreach (IServiceDispatcher dispatcher in dispatchers)
                {
                    bool extensionProvidedUrl = false;
                    var metadataExtension = dispatcher.Host.Extensions.Find<ServiceMetadataExtension>();
                    if (metadataExtension.HttpGetEnabled && metadataExtension.HttpGetUrl != null && metadataExtension.HttpGetUrl.AbsolutePath != "/")
                    {
                        extensionProvidedUrl = true;
                        MapMetadata(branchApp, metadataExtension.HttpGetUrl.AbsolutePath, metadataExtension.ConfigureWith(metadataExtension.HttpGetUrl));
                    }

                    if (metadataExtension.HttpsGetEnabled && metadataExtension.HttpsGetUrl != null && metadataExtension.HttpsGetUrl.AbsolutePath != "/"
                        && !string.Equals(metadataExtension.HttpsGetUrl.AbsolutePath, metadataExtension.HttpGetUrl?.AbsolutePath))
                    {
                        extensionProvidedUrl = true;
                        MapMetadata(branchApp, metadataExtension.HttpsGetUrl.AbsolutePath, metadataExtension.ConfigureWith(metadataExtension.HttpsGetUrl));
                    }

                    if (metadataExtension.HttpHelpPageEnabled && metadataExtension.HttpHelpPageUrl != null && metadataExtension.HttpHelpPageUrl.AbsolutePath != "/"
                        && !string.Equals(metadataExtension.HttpHelpPageUrl.AbsolutePath, metadataExtension.HttpGetUrl?.AbsolutePath))
                    {
                        extensionProvidedUrl = true;
                        MapMetadata(branchApp, metadataExtension.HttpHelpPageUrl.AbsolutePath, metadataExtension.ConfigureWith(metadataExtension.HttpHelpPageUrl));
                    }

                    if (metadataExtension.HttpsHelpPageEnabled && metadataExtension.HttpsHelpPageUrl != null && metadataExtension.HttpsHelpPageUrl.AbsolutePath != "/"
                        && !string.Equals(metadataExtension.HttpsHelpPageUrl.AbsolutePath, metadataExtension.HttpHelpPageUrl?.AbsolutePath))
                    {
                        extensionProvidedUrl = true;
                        MapMetadata(branchApp, metadataExtension.HttpsHelpPageUrl.AbsolutePath, metadataExtension.ConfigureWith(metadataExtension.HttpsHelpPageUrl));
                    }

                    if (!extensionProvidedUrl)
                    {
                        if (dispatcher.BaseAddress == null)
                        {
                            // TODO: Should we throw? Ignore?
                            continue;
                        }

                        branchApp.Map(dispatcher.BaseAddress.AbsolutePath, metadataExtension.ConfigureWith(dispatcher.BaseAddress));
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
