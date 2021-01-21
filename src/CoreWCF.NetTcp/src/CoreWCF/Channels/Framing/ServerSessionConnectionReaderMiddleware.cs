// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Channels.Framing
{
    internal class ServerSessionConnectionReaderMiddleware
    {
        private readonly HandshakeDelegate _next;
        private readonly IServiceScopeFactory _servicesScopeFactory;
        private readonly IApplicationLifetime _appLifetime;
        private readonly IDictionary<IServiceDispatcher, ITransportFactorySettings> _transportSettingsCache = new Dictionary<IServiceDispatcher, ITransportFactorySettings>();

        public ServerSessionConnectionReaderMiddleware(HandshakeDelegate next, IServiceScopeFactory servicesScopeFactory, IApplicationLifetime appLifetime)
        {
            _next = next;
            _servicesScopeFactory = servicesScopeFactory;
            _appLifetime = appLifetime;
        }

        public async Task OnConnectedAsync(FramingConnection connection)
        {
            if (!_transportSettingsCache.TryGetValue(connection.ServiceDispatcher, out ITransportFactorySettings settings))
            {
                var be = connection.ServiceDispatcher.Binding.CreateBindingElements();
                var tbe = be.Find<TransportBindingElement>();
                settings = new NetFramingTransportSettings
                {
                    CloseTimeout = connection.ServiceDispatcher.Binding.CloseTimeout,
                    OpenTimeout = connection.ServiceDispatcher.Binding.OpenTimeout,
                    ReceiveTimeout = connection.ServiceDispatcher.Binding.ReceiveTimeout,
                    SendTimeout = connection.ServiceDispatcher.Binding.SendTimeout,
                    ManualAddressing = tbe.ManualAddressing,
                    BufferManager = connection.BufferManager,
                    MaxReceivedMessageSize = tbe.MaxReceivedMessageSize,
                    MessageEncoderFactory = connection.MessageEncoderFactory
                };
            }

            var channel = new ServerFramingDuplexSessionChannel(connection, settings, false, _servicesScopeFactory.CreateScope().ServiceProvider);
            channel.ChannelDispatcher = await connection.ServiceDispatcher.CreateServiceChannelDispatcherAsync(channel);
            await channel.StartReceivingAsync();
        }

    }
}
