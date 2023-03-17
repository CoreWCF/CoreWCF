// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherClient
{
    internal abstract class DispatcherChannelFactory : ChannelFactoryBase
    {
        internal abstract TimeSpan CloseTimeout { get; }
        internal abstract TimeSpan OpenTimeout { get; }
        internal abstract TimeSpan SendTimeout { get; }

        internal abstract Task<IServiceChannelDispatcher> GetServiceChannelDispatcherAsync(DispatcherRequestChannel channel);
    }

    internal class DispatcherChannelFactory<TChannel, TService, TContract> : DispatcherChannelFactory, IChannelFactory<TChannel> where TService : class
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Action<CoreWCF.ServiceHostBase> _configureServiceHostBase;
        private readonly IDictionary<object, IServiceChannelDispatcher> _serviceChannelDispatchers = new Dictionary<object, IServiceChannelDispatcher>();
        private readonly object _lock = new object();

        public DispatcherChannelFactory(Action<IServiceCollection> configureServices, Action<CoreWCF.ServiceHostBase> configureServiceHostBase = default)
        {
            _serviceProvider = BuildServiceProvider(configureServices);
            _configureServiceHostBase = configureServiceHostBase;
        }

        private IServiceProvider BuildServiceProvider(Action<IServiceCollection> configureServices)
        {
            var services = new ServiceCollection();
            services.AddSingleton<DispatcherChannelFactory>(this);
            services.AddScoped<DispatcherReplyChannel>();
            services.AddServiceModelServices();
            services.AddLogging();
            IServer server = new Helpers.MockServer();
            services.AddSingleton(server);
            services.AddSingleton(GetType(), this);
            configureServices?.Invoke(services);
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            IServerAddressesFeature serverAddressesFeature = serviceProvider.GetRequiredService<IServerAddressesFeature>();
            server.Features.Set(serverAddressesFeature);
            return serviceProvider;
        }

        internal override TimeSpan SendTimeout { get; } = TimeSpan.FromSeconds(60);
        protected override TimeSpan DefaultCloseTimeout => CloseTimeout;
        protected override TimeSpan DefaultOpenTimeout => OpenTimeout;


        internal override TimeSpan CloseTimeout => TimeSpan.FromSeconds(30);
        internal override TimeSpan OpenTimeout => TimeSpan.FromSeconds(30);

        public TChannel CreateChannel(EndpointAddress to, Uri via)
        {
            IServiceScopeFactory servicesScopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            IServiceScope serviceScope = servicesScopeFactory.CreateScope();

            if (typeof(TChannel) == typeof(IRequestChannel))
            {
                var channel = new DispatcherRequestChannel(serviceScope.ServiceProvider, to, via);
                return (TChannel)(object)channel;
            }

            if (typeof(TChannel) == typeof(IRequestSessionChannel))
            {
                var channel = new DispatcherRequestSessionChannel(serviceScope.ServiceProvider, to, via);
                return (TChannel)(object)channel;
            }

            throw new InvalidOperationException("Only support IRequestChannel and IRequestSessionChannel");
        }

        public TChannel CreateChannel(EndpointAddress to)
        {
            return CreateChannel(to, to.Uri);
        }

        internal override async Task<IServiceChannelDispatcher> GetServiceChannelDispatcherAsync(DispatcherRequestChannel channel)
        {
            lock (_lock)
            {
                if (_serviceChannelDispatchers.TryGetValue(channel, out IServiceChannelDispatcher dispatcher))
                {
                    return dispatcher;
                }
            }

            IServiceBuilder serviceBuilder = _serviceProvider.GetRequiredService<IServiceBuilder>();
            serviceBuilder.AddService<TService>();
            var binding = new CoreWCF.Channels.CustomBinding("BindingName", "BindingNS");
            binding.Elements.Add(new Helpers.MockTransportBindingElement());
            serviceBuilder.AddServiceEndpoint<TService, TContract>(binding, channel.Via);
            if (_configureServiceHostBase != null)
            {
                serviceBuilder.ConfigureServiceHostBase<TService>(_configureServiceHostBase);
            }
            await serviceBuilder.OpenAsync();
            IDispatcherBuilder dispatcherBuilder = _serviceProvider.GetRequiredService<IDispatcherBuilder>();
            List<IServiceDispatcher> dispatchers = dispatcherBuilder.BuildDispatchers(typeof(TService));
            IServiceDispatcher serviceDispatcher = dispatchers[0];
            CoreWCF.Channels.IChannel replyChannel;
            if (channel is DispatcherRequestSessionChannel)
            {
                replyChannel = new DispatcherReplySessionChannel(_serviceProvider);
            }
            else
            {
                replyChannel = new DispatcherReplyChannel(_serviceProvider);
            }

            IServiceChannelDispatcher newDispatcher = await serviceDispatcher.CreateServiceChannelDispatcherAsync(replyChannel);

            lock (_lock)
            {
                if (!_serviceChannelDispatchers.TryGetValue(channel, out IServiceChannelDispatcher dispatcher))
                {
                    dispatcher = newDispatcher;
                    _serviceChannelDispatchers[channel] = dispatcher;
                }

                return dispatcher;
            }
        }

        protected override IAsyncResult OnBeginClose(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return Task.CompletedTask;
        }

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return Task.CompletedTask;
        }

        protected override void OnClose(TimeSpan timeout)
        {
        }

        protected override void OnEndClose(IAsyncResult result)
        {
        }

        protected override void OnEndOpen(IAsyncResult result)
        {
        }

        protected override void OnOpen(TimeSpan timeout)
        {
        }
    }
}
