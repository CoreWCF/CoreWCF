using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using CoreWCF.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreWCF.Channels.Framing
{
    public class NetMessageFramingConnectionHandler : ConnectionHandler
    {
        private IServiceBuilder _serviceBuilder;
        private IDispatcherBuilder _dispatcherBuilder;
        private HandshakeDelegate _handshake;

        public NetMessageFramingConnectionHandler(IServiceBuilder serviceBuilder, IDispatcherBuilder dispatcherBuilder, IFramingConnectionHandshakeBuilder handshakeBuilder)
        {
            _serviceBuilder = serviceBuilder;
            _dispatcherBuilder = dispatcherBuilder;
            _handshake = BuildHandshake(handshakeBuilder);
        }

        private HandshakeDelegate BuildHandshake(IFramingConnectionHandshakeBuilder handshakeBuilder)
        {
            handshakeBuilder.UseMiddleware<FramingModeHandshakeMiddleware>();
            handshakeBuilder.Map(connection => connection.FramingMode == FramingMode.Duplex,
                configuration =>
                {
                    configuration.UseMiddleware<DuplexFramingMiddleware>();
                    configuration.Use(next => async (connection) =>
                    {
                        var addressTable = configuration.HandshakeServices.GetRequiredService<UriPrefixTable<HandshakeDelegate>>();
                        var serviceHandshake = GetServiceHandshakeDelegate(addressTable, connection.Via);
                        await serviceHandshake(connection);
                        await next(connection);
                    });
                    configuration.UseMiddleware<ServerFramingDuplexSessionMiddleware>();
                    configuration.UseMiddleware<ServerSessionConnectionReaderMiddleware>();
                });
            handshakeBuilder.Map(connection => connection.FramingMode == FramingMode.Singleton,
                configuration =>
                {
                    configuration.UseMiddleware<SingletonFramingMiddleware>();
                    configuration.Use(next => async (connection) =>
                    {
                        var addressTable = configuration.HandshakeServices.GetRequiredService<UriPrefixTable<HandshakeDelegate>>();
                        var serviceHandshake = GetServiceHandshakeDelegate(addressTable, connection.Via);
                        await serviceHandshake(connection);
                        await next(connection);
                    });
                    configuration.UseMiddleware<ServerFramingSingletonMiddleware>();
                    configuration.UseMiddleware<ServerSingletonConnectionReaderMiddleware>();
                });
            return handshakeBuilder.Build();
        }

        internal static UriPrefixTable<HandshakeDelegate> BuildAddressTable(IServiceProvider services)
        {
            var serviceBuilder = services.GetRequiredService<IServiceBuilder>();
            var dispatcherBuilder = services.GetRequiredService<IDispatcherBuilder>();
            var addressTable = new UriPrefixTable<HandshakeDelegate>();
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

                    // TODO: Limit to specifically TcpTransportBindingElement if net.tcp etc
                    var be = dispatcher.Binding.CreateBindingElements();
                    var cotbe = be.Find<ConnectionOrientedTransportBindingElement>();
                    if (cotbe == null)
                    {
                        // TODO: Should we throw? Ignore?
                        continue;
                    }

                    var handshake = BuildHandshakeDelegateForDispatcher(dispatcher);
                    addressTable.RegisterUri(dispatcher.BaseAddress, cotbe.HostNameComparisonMode, handshake);
                }
            }

            return addressTable;
        }

        private static HandshakeDelegate BuildHandshakeDelegateForDispatcher(IServiceDispatcher dispatcher)
        {
            var be = dispatcher.Binding.CreateBindingElements();
            var mebe = be.Find<MessageEncodingBindingElement>();
            MessageEncoderFactory mefact = mebe.CreateMessageEncoderFactory();
            var tbe = be.Find<ConnectionOrientedTransportBindingElement>();
            int maxReceivedMessageSize = (int)Math.Min(tbe.MaxReceivedMessageSize, int.MaxValue);
            int maxBufferSize = tbe.MaxBufferSize;
            var bufferManager = BufferManager.CreateBufferManager(tbe.MaxBufferPoolSize, maxReceivedMessageSize);
            var connectionBufferSize = tbe.ConnectionBufferSize;
            var transferMode = tbe.TransferMode;
            var upgradeBindingElements = (from element in be where element is StreamUpgradeBindingElement select element).Cast<StreamUpgradeBindingElement>().ToList();
            StreamUpgradeProvider streamUpgradeProvider = null;
            ISecurityCapabilities securityCapabilities = null;
            if (upgradeBindingElements.Count > 1)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.MultipleStreamUpgradeProvidersInParameters));
            }
            // TODO: Limit NamedPipes to prevent it using SslStreamSecurityUpgradeProvider
            else if ((upgradeBindingElements.Count == 1) /*&& this.SupportsUpgrade(upgradeBindingElements[0])*/)
            {
                var bindingContext = new BindingContext(new CustomBinding(dispatcher.Binding), new BindingParameterCollection());
                streamUpgradeProvider = upgradeBindingElements[0].BuildServerStreamUpgradeProvider(bindingContext);
                streamUpgradeProvider.OpenAsync().GetAwaiter().GetResult();
                securityCapabilities = upgradeBindingElements[0].GetProperty<ISecurityCapabilities>(bindingContext);
            }
            return (connection) =>
            {
                connection.MessageEncoderFactory = mefact;
                connection.StreamUpgradeAcceptor = streamUpgradeProvider?.CreateUpgradeAcceptor();
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

        internal static HandshakeDelegate GetServiceHandshakeDelegate(UriPrefixTable<HandshakeDelegate>  addressTable, Uri via)
        {
            HandshakeDelegate handshake = null;
            if (addressTable.TryLookupUri(via, HostNameComparisonMode.StrongWildcard, out handshake))
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
