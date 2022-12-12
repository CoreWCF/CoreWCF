// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using CoreWCF.Runtime;

namespace CoreWCF.Channels.Framing
{
    internal class ServerSessionConnectionReaderMiddleware
    {
        private readonly HandshakeDelegate _next;
        private readonly IServiceScopeFactory _servicesScopeFactory;
        private readonly IApplicationLifetime _appLifetime;
        private readonly Hashtable _transportSettingsCache = new Hashtable();
        private readonly object _lock = new object();

        public ServerSessionConnectionReaderMiddleware(HandshakeDelegate next, IServiceScopeFactory servicesScopeFactory, IApplicationLifetime appLifetime)
        {
            _next = next;
            _servicesScopeFactory = servicesScopeFactory;
            _appLifetime = appLifetime;
        }

        public async Task OnConnectedAsync(FramingConnection connection)
        {
            ITransportFactorySettings settings;
            if (_transportSettingsCache.Contains(connection.ServiceDispatcher))
            {
                settings = (ITransportFactorySettings)_transportSettingsCache[connection.ServiceDispatcher];
            }
            else
            {
                lock (_lock)
                {
                    // Double locked checking not necessary as there's no functional harm to having multiple instances
                    BindingElementCollection be = connection.ServiceDispatcher.Binding.CreateBindingElements();
                    TransportBindingElement tbe = be.Find<TransportBindingElement>();
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
                    _transportSettingsCache[connection.ServiceDispatcher] = settings;
                }
            }

            var channel = new ServerFramingDuplexSessionChannel(connection, settings, false, _servicesScopeFactory.CreateScope().ServiceProvider);
            channel.ChannelDispatcher = await connection.ServiceDispatcher.CreateServiceChannelDispatcherAsync(channel);
            await channel.StartReceivingAsync();
            await channel.CloseAsync();
        }
    }
}
