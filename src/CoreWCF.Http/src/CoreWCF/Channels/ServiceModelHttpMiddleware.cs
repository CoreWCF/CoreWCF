using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using CoreWCF.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreWCF.Channels
{
    public partial class ServiceModelHttpMiddleware
    {
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
            serviceBuilder.Opening += ServiceBuilderOpeningCallback;
            serviceBuilder.Opened += ServiceBuilderOpenedCallback;
        }

        public Task InvokeAsync(HttpContext context)
        {
            return _branch(context);
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

        private static void ServiceBuilderOpeningCallback(object sender, EventArgs e)
        {
            ((IServiceBuilder)sender).BaseAddresses.Add(new Uri("http://localhost/"));
        }

        private void ServiceBuilderOpenedCallback(object sender, EventArgs e)
        {
            EnsureBranchBuilt();
        }

        private RequestDelegate BuildBranch()
        {
            _logger.LogDebug("Building branch map");
            var branchApp = _app.New();

            foreach (var serviceType in _serviceBuilder.Services)
            {
                var dispatchers = _dispatcherBuilder.BuildDispatchers(serviceType);
                foreach (var dispatcher in dispatchers)
                {
                    if (dispatcher.BaseAddress == null)
                    {
                        // TODO: Should we throw? Ignore?
                        continue;
                    }

                    var binding = dispatcher.Binding as CustomBinding;
                    if (binding == null)
                    {
                        binding = new CustomBinding(dispatcher.Binding);
                    }
                    if (binding.Elements.Find<HttpTransportBindingElement>() == null)
                    {
                        _logger.LogDebug($"Binding for address {dispatcher.BaseAddress} is not an HTTP[S] binding ao skipping");
                        continue; // Not an HTTP(S) dispatcher
                    }

                    var parameters = new BindingParameterCollection();
                    parameters.Add(_app);
                    Type supportedChannelType = null;
                    IServiceDispatcher serviceDispatcher = null;
                    var supportedChannels = dispatcher.SupportedChannelTypes;
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
                        var servicesScopeFactory = wcfApp.ApplicationServices.GetRequiredService<IServiceScopeFactory>();
                        var requestHandler = new RequestDelegateHandler(serviceDispatcher, servicesScopeFactory);
                        if (requestHandler.WebSocketOptions != null)
                        {
                            wcfApp.UseWebSockets(requestHandler.WebSocketOptions);
                        }
                        wcfApp.Run(requestHandler.HandleRequest);
                    });
                }
            }

            branchApp.Use(_ => { return reqContext => _next(reqContext); });
            return branchApp.Build();
        }
    }
}
