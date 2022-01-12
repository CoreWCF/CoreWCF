// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels
{
    public partial class ServiceModelHttpMiddleware
    {
        private const string RestorePathsDelegateItemName = nameof(ServiceModelHttpMiddleware) + "_RestorePathsDelegate";
        private readonly IApplicationBuilder _app;
        private readonly IServiceBuilder _serviceBuilder;
        private readonly IDispatcherBuilder _dispatcherBuilder;
        private readonly RequestDelegate _next;
        private readonly ILogger<ServiceModelHttpMiddleware> _logger;
        private RequestDelegate _branch;
        private bool _branchBuilt;

        public ServiceModelHttpMiddleware(RequestDelegate next, IApplicationBuilder app, IServiceBuilder serviceBuilder, IDispatcherBuilder dispatcherBuilder, ILogger<ServiceModelHttpMiddleware> logger)
        {
            _app = app;
            _serviceBuilder = serviceBuilder;
            _dispatcherBuilder = dispatcherBuilder;
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

            foreach (Type serviceType in _serviceBuilder.Services)
            {
                System.Collections.Generic.List<IServiceDispatcher> dispatchers = _dispatcherBuilder.BuildDispatchers(serviceType);
                foreach (IServiceDispatcher dispatcher in dispatchers)
                {
                    if (dispatcher.BaseAddress == null)
                    {
                        // TODO: Should we throw? Ignore?
                        continue;
                    }

                    if (!(dispatcher.Binding is CustomBinding binding))
                    {
                        binding = new CustomBinding(dispatcher.Binding);
                    }
                    if (binding.Elements.Find<HttpTransportBindingElement>() == null)
                    {
                        _logger.LogDebug($"Binding for address {dispatcher.BaseAddress} is not an HTTP[S] binding so skipping");
                        continue; // Not an HTTP(S) dispatcher
                    }

                    var parameters = new BindingParameterCollection
                    {
                        _app
                    };
                    Type supportedChannelType = null;
                    IServiceDispatcher serviceDispatcher = null;
                    System.Collections.Generic.IList<Type> supportedChannels = dispatcher.SupportedChannelTypes;
                    for (int i = 0; i < supportedChannels.Count; i++)
                    {
                        Type channelType = supportedChannels[i];

                        if (channelType == typeof(IInputChannel))
                        {
                            if (binding.CanBuildServiceDispatcher<IInputChannel>(parameters))
                            {
                                serviceDispatcher = binding.BuildServiceDispatcher<IInputChannel>(parameters, dispatcher);
                                supportedChannelType = typeof(IInputChannel);
                                break;
                            }
                        }
                        if (channelType == typeof(IReplyChannel))
                        {
                            if (binding.CanBuildServiceDispatcher<IReplyChannel>(parameters))
                            {
                                serviceDispatcher = binding.BuildServiceDispatcher<IReplyChannel>(parameters, dispatcher);
                                supportedChannelType = typeof(IReplyChannel);
                            }
                        }
                        if (channelType == typeof(IDuplexChannel))
                        {
                            if (binding.CanBuildServiceDispatcher<IDuplexChannel>(parameters))
                            {
                                serviceDispatcher = binding.BuildServiceDispatcher<IDuplexChannel>(parameters, dispatcher);
                                supportedChannelType = typeof(IDuplexChannel);
                            }
                        }
                        if (channelType == typeof(IInputSessionChannel))
                        {
                            if (binding.CanBuildServiceDispatcher<IInputSessionChannel>(parameters))
                            {
                                serviceDispatcher = binding.BuildServiceDispatcher<IInputSessionChannel>(parameters, dispatcher);
                                supportedChannelType = typeof(IInputSessionChannel);
                            }
                        }
                        if (channelType == typeof(IReplySessionChannel))
                        {
                            if (binding.CanBuildServiceDispatcher<IReplySessionChannel>(parameters))
                            {
                                serviceDispatcher = binding.BuildServiceDispatcher<IReplySessionChannel>(parameters, dispatcher);
                                supportedChannelType = typeof(IReplySessionChannel);
                            }
                        }
                        if (channelType == typeof(IDuplexSessionChannel))
                        {
                            if (binding.CanBuildServiceDispatcher<IDuplexSessionChannel>(parameters))
                            {
                                serviceDispatcher = binding.BuildServiceDispatcher<IDuplexSessionChannel>(parameters, dispatcher);
                                supportedChannelType = typeof(IDuplexSessionChannel);
                            }
                        }
                    }

                    _logger.LogInformation($"Mapping CoreWCF branch app for path {dispatcher.BaseAddress.AbsolutePath}");
                    branchApp.Map(dispatcher.BaseAddress.AbsolutePath, wcfApp =>
                    {
                        IServiceScopeFactory servicesScopeFactory = wcfApp.ApplicationServices.GetRequiredService<IServiceScopeFactory>();
                        var requestHandler = new RequestDelegateHandler(serviceDispatcher, servicesScopeFactory);
                        if (requestHandler.WebSocketOptions != null)
                        {
                            wcfApp.UseWebSockets(requestHandler.WebSocketOptions);
                        }
                        wcfApp.Run(requestHandler.HandleRequest);
                    });
                }
            }

            branchApp.Use(_ => { return reqContext =>
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
            }; });
            return branchApp.Build();
        }
    }
}
