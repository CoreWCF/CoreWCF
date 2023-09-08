// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Security;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels.Framing
{
    public class NetMessageFramingConnectionHandler : ConnectionHandler
    {
        private readonly IServiceBuilder _serviceBuilder;
        private readonly IDispatcherBuilder _dispatcherBuilder;
        private readonly HandshakeDelegate _handshake;
        private readonly ILogger _framingLogger;
        private readonly IServiceProvider _services;

        [Obsolete("Added by mistake, does nothing", true)]
        public List<ListenOptions> ListenOptions { get; } = new List<ListenOptions>();

        public NetMessageFramingConnectionHandler(IServiceBuilder serviceBuilder, IDispatcherBuilder dispatcherBuilder, IFramingConnectionHandshakeBuilder handshakeBuilder, ILogger<FramingConnection> framingLogger)
        {
            _serviceBuilder = serviceBuilder;
            _dispatcherBuilder = dispatcherBuilder;
            _handshake = BuildHandshake(handshakeBuilder);
            _framingLogger = framingLogger;
            _services = handshakeBuilder.HandshakeServices;
            serviceBuilder.Opened += OnServiceBuilderOpened;
        }

        private void OnServiceBuilderOpened(object sender, EventArgs e)
        {
            // Trigger building all of the services to improve first request time and to catch any service config issues
            _services.GetRequiredService<UriPrefixTable<HandshakeDelegate>>();
        }

        private HandshakeDelegate BuildHandshake(IFramingConnectionHandshakeBuilder handshakeBuilder)
        {
            handshakeBuilder.UseMiddleware<FramingModeHandshakeMiddleware>();
            handshakeBuilder.Map(connection => connection.FramingMode == FramingMode.Duplex,
                configuration =>
                {
                    configuration.UseMiddleware<DuplexFramingMiddleware>();
                    configuration.Use(next => connection => PerformServiceHandshake(configuration, connection, next));
                    configuration.UseMiddleware<ServerFramingDuplexSessionMiddleware>();
                    configuration.UseMiddleware<ServerSessionConnectionReaderMiddleware>();
                });
            handshakeBuilder.Map(connection => connection.FramingMode == FramingMode.Singleton,
                configuration =>
                {
                    configuration.UseMiddleware<SingletonFramingMiddleware>();
                    configuration.Use(next => connection => PerformServiceHandshake(configuration, connection, next));
                    configuration.UseMiddleware<ServerFramingSingletonMiddleware>();
                    configuration.UseMiddleware<ServerSingletonConnectionReaderMiddleware>();
                });
            return handshakeBuilder.Build();
        }

        internal static UriPrefixTable<HandshakeDelegate> BuildAddressTable(IServiceProvider services)
        {
            ILogger<NetMessageFramingConnectionHandler> logger = services.GetRequiredService<ILogger<NetMessageFramingConnectionHandler>>();
            IServiceBuilder serviceBuilder = services.GetRequiredService<IServiceBuilder>();
            IDispatcherBuilder dispatcherBuilder = services.GetRequiredService<IDispatcherBuilder>();
            var addressTable = new UriPrefixTable<HandshakeDelegate>();
            foreach (Type serviceType in serviceBuilder.Services)
            {
                List<IServiceDispatcher> dispatchers = dispatcherBuilder.BuildDispatchers(serviceType);
                foreach (IServiceDispatcher dispatcher in dispatchers)
                {
                    if (dispatcher.BaseAddress == null)
                    {
                        // TODO: Should we throw? Ignore?
                        continue;
                    }

                    // TODO: Limit to specifically TcpTransportBindingElement if net.tcp etc
                    BindingElementCollection be = dispatcher.Binding.CreateBindingElements();
                    ConnectionOrientedTransportBindingElement cotbe = be.Find<ConnectionOrientedTransportBindingElement>();
                    if (cotbe == null)
                    {
                        // TODO: Should we throw? Ignore?
                        continue;
                    }

                    IServiceDispatcher _serviceDispatcher = null;
                    var _customBinding = dispatcher.Binding as CustomBinding ?? new CustomBinding(dispatcher.Binding);
                    if (_customBinding.Elements.Find<ConnectionOrientedTransportBindingElement>() != null)
                    {
                        var parameters = new BindingParameterCollection();
                        if (_customBinding.CanBuildServiceDispatcher<IDuplexSessionChannel>(parameters))
                        {
                            _serviceDispatcher = _customBinding.BuildServiceDispatcher<IDuplexSessionChannel>(parameters, dispatcher);
                        }
                    }
                    _serviceDispatcher ??= dispatcher;
                    HandshakeDelegate handshake = BuildHandshakeDelegateForDispatcher(_serviceDispatcher);

                    logger.LogDebug($"Registering URI {dispatcher.BaseAddress} with NetMessageFramingConnectionHandler");
                    addressTable.RegisterUri(dispatcher.BaseAddress, cotbe.HostNameComparisonMode, handshake);
                }
            }

            return addressTable;
        }

        private static HandshakeDelegate BuildHandshakeDelegateForDispatcher(IServiceDispatcher dispatcher)
        {
            BindingElementCollection be = dispatcher.Binding.CreateBindingElements();
            MessageEncodingBindingElement mebe = be.Find<MessageEncodingBindingElement>();
            MessageEncoderFactory mefact = mebe.CreateMessageEncoderFactory();
            ConnectionOrientedTransportBindingElement tbe = be.Find<ConnectionOrientedTransportBindingElement>();
            long maxReceivedMessageSize = tbe.MaxReceivedMessageSize;
            int maxBufferSize = tbe.MaxBufferSize;
            var bufferManager = BufferManager.CreateBufferManager(tbe.MaxBufferPoolSize, maxBufferSize);
            int connectionBufferSize = tbe.ConnectionBufferSize;
            TransferMode transferMode = tbe.TransferMode;
            var upgradeBindingElements = (from element in be where element is StreamUpgradeBindingElement select element).Cast<StreamUpgradeBindingElement>().ToList();
            StreamUpgradeProvider streamUpgradeProvider = null;
            ISecurityCapabilities securityCapabilities = null;
            if (upgradeBindingElements.Count > 1)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.MultipleStreamUpgradeProvidersInParameters));
            }
            // TODO: Limit NamedPipes to prevent it using SslStreamSecurityUpgradeProvider
            else if ((upgradeBindingElements.Count == 1) && tbe.SupportsUpgrade(upgradeBindingElements[0]))
            {
                SecurityCredentialsManager credentialsManager = dispatcher.Host.Description.Behaviors.Find<SecurityCredentialsManager>();
                var bindingContext = new BindingContext(new CustomBinding(dispatcher.Binding), new BindingParameterCollection());

                if (credentialsManager != null)
                    bindingContext.BindingParameters.Add(credentialsManager);

                streamUpgradeProvider = upgradeBindingElements[0].BuildServerStreamUpgradeProvider(bindingContext);
                streamUpgradeProvider.OpenAsync().GetAwaiter().GetResult();
                securityCapabilities = upgradeBindingElements[0].GetProperty<ISecurityCapabilities>(bindingContext);
            }
            return (connection) =>
            {
                connection.MessageEncoderFactory = mefact;
                connection.StreamUpgradeAcceptor = streamUpgradeProvider?.CreateUpgradeAcceptor();
                if(connection.StreamUpgradeAcceptor != null)
                {
                    connection.StreamUpgradeAcceptor.Features.Set<FramingConnection>(connection);
                }
                connection.SecurityCapabilities = securityCapabilities;
                connection.ServiceDispatcher = dispatcher;
                connection.BufferManager = bufferManager;
                connection.MaxReceivedMessageSize = maxReceivedMessageSize;
                connection.MaxBufferSize = maxBufferSize;
                connection.ConnectionBufferSize = connectionBufferSize;
                connection.TransferMode = transferMode;
                return Task.CompletedTask;
            };
        }

        private static async Task PerformServiceHandshake(IFramingConnectionHandshakeBuilder configuration, FramingConnection connection, HandshakeDelegate next)
        {
            UriPrefixTable<HandshakeDelegate> addressTable = configuration.HandshakeServices.GetRequiredService<UriPrefixTable<HandshakeDelegate>>();
            HandshakeDelegate serviceHandshake = GetServiceHandshakeDelegate(addressTable, connection.Via);
            if (serviceHandshake != null)
            {
                await serviceHandshake(connection);
                await next(connection);
            }
            else
            {
                await connection.SendFaultAsync(FramingEncodingString.EndpointNotFoundFault, ServiceDefaults.SendTimeout, TransportDefaults.MaxDrainSize);
            }
        }

        private static HandshakeDelegate GetServiceHandshakeDelegate(UriPrefixTable<HandshakeDelegate> addressTable, Uri via)
        {
            if (addressTable.TryLookupUri(via, HostNameComparisonMode.StrongWildcard, out HandshakeDelegate handshake))
            {
                return handshake;
            }

            if (addressTable.TryLookupUri(via, HostNameComparisonMode.Exact, out handshake))
            {
                return handshake;
            }

            addressTable.TryLookupUri(via, HostNameComparisonMode.WeakWildcard, out handshake);
            return handshake;
        }

        public override Task OnConnectedAsync(ConnectionContext context)
        {
            var connection = new FramingConnection(context);
            return _handshake(connection);
        }
    }
}
