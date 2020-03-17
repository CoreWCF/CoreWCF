using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using CoreWCF.Runtime;
using CoreWCF.Configuration;
using CoreWCF.Security;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace CoreWCF.Channels.Framing
{
    internal class ServerSessionConnectionReaderMiddleware
    {
        private HandshakeDelegate _next;
        private IServiceScopeFactory _servicesScopeFactory;
        private IApplicationLifetime _appLifetime;
        private IDictionary<IServiceDispatcher, ITransportFactorySettings> _transportSettingsCache = new Dictionary<IServiceDispatcher, ITransportFactorySettings>();

        public ServerSessionConnectionReaderMiddleware(HandshakeDelegate next, IServiceScopeFactory servicesScopeFactory, IApplicationLifetime appLifetime)
        {
            _next = next;
            _servicesScopeFactory = servicesScopeFactory;
            _appLifetime = appLifetime;
        }

        public async Task OnConnectedAsync(FramingConnection connection)
        {
            ITransportFactorySettings settings;
            if (!_transportSettingsCache.TryGetValue(connection.ServiceDispatcher, out settings))
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
