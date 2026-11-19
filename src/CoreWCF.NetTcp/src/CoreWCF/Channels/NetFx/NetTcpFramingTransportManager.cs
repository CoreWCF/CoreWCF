// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels.Framing;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels
{
    // .NET Framework replacement for the Kestrel-based NetTcpFramingOptionsSetup (see issue #1681).
    // Resolves listen endpoints, composes the per-endpoint connection middleware chain ending in
    // NetMessageFramingConnectionHandler, and owns the socket listeners' lifetime.
    internal class NetTcpFramingTransportManager
    {
        private readonly IServiceBuilder _serviceBuilder;
        private readonly ILogger<NetTcpFramingTransportManager> _logger;
        private readonly ILogger<FramingConnection> _framingConnectionLogger;
        private readonly List<SocketListener> _listeners = new List<SocketListener>();

        public NetTcpFramingTransportManager(IServiceBuilder serviceBuilder, ILogger<NetTcpFramingTransportManager> logger, ILogger<FramingConnection> framingConnectionLogger)
        {
            _serviceBuilder = serviceBuilder;
            _logger = logger;
            _framingConnectionLogger = framingConnectionLogger;
        }

        public Task BindAsync(List<TcpListenOptions> listenOptions, CancellationToken cancellationToken)
        {
            foreach (TcpListenOptions tcpListenOptions in listenOptions)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Task.FromCanceled(cancellationToken);
                }

                IPEndPoint endpoint = ResolveEndPoint(tcpListenOptions.BaseAddress);
                if (endpoint == null)
                {
                    continue;
                }

                ConnectionDelegate connectionDelegate = BuildConnectionDelegate(tcpListenOptions);
                var listener = new SocketListener(tcpListenOptions, endpoint, connectionDelegate, _logger, _framingConnectionLogger);
                listener.Bind();
                listener.StartAccepting();
                _listeners.Add(listener);
            }

            return Task.CompletedTask;
        }

        private ConnectionDelegate BuildConnectionDelegate(TcpListenOptions tcpListenOptions)
        {
            // Runs after any user supplied middleware, matching the ordering used by
            // NetTcpFramingOptionsSetup.AddTcpListenOptionsToConnectionContext on other frameworks.
            tcpListenOptions.Use(next =>
            {
                return (ConnectionContext context) =>
                {
                    context.Features.Set<NetFramingListenOptions>(tcpListenOptions);
                    return next(context);
                };
            });
            tcpListenOptions.UseConnectionHandler<NetMessageFramingConnectionHandler>();
            return ((IConnectionBuilder)tcpListenOptions).Build();
        }

        private IPEndPoint ResolveEndPoint(Uri baseAddress)
        {
            // Same hostname resolution rules as NetTcpFramingOptionsSetup.Configure.
            IPAddress listenAddress = null;
            if (baseAddress.HostNameType == UriHostNameType.IPv4 || baseAddress.HostNameType == UriHostNameType.IPv6)
            {
                if (IPAddress.TryParse(baseAddress.DnsSafeHost, out listenAddress))
                {
                    if (listenAddress.AddressFamily == AddressFamily.InterNetwork && !Socket.OSSupportsIPv4)
                    {
                        _logger.LogError("NetTcp listen uri specified the IPv4 hostname \"{HostName}\" and the OS doesn't support IPv4", baseAddress.DnsSafeHost);
                        return null;
                    }
                    if (listenAddress.AddressFamily == AddressFamily.InterNetworkV6 && !Socket.OSSupportsIPv6)
                    {
                        _logger.LogError("NetTcp listen uri specified the IPv6 hostname \"{HostName}\" and the OS doesn't support IPv6", baseAddress.DnsSafeHost);
                        return null;
                    }
                }
            }
            else if (baseAddress.HostNameType == UriHostNameType.Dns)
            {
                if (Socket.OSSupportsIPv6)
                {
                    listenAddress = IPAddress.IPv6Any;
                }
                else
                {
                    listenAddress = IPAddress.Any;
                }
            }
            else
            {
                _logger.LogError("NetTcp listen uri specified an unusable hostname \"{HostName}\"", baseAddress.DnsSafeHost);
                return null;
            }

            return new IPEndPoint(listenAddress, baseAddress.Port);
        }

        internal void UpdateServiceBuilderBaseAddresses()
        {
            foreach (SocketListener listener in _listeners)
            {
                IPEndPoint endpoint = listener.ListeningEndPoint;
                string address = endpoint.Address.ToString();
                if (endpoint.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    address = $"[{address}]";
                var baseAddress = new Uri($"net.tcp://{address}:{endpoint.Port}/");
                _logger.LogDebug("Adding base address {baseAddress} to ServiceBuilderOptions", baseAddress);
                _serviceBuilder.BaseAddresses.Add(baseAddress);
            }
        }

        internal IEnumerable<string> GetListeningAddresses()
        {
            foreach (SocketListener listener in _listeners)
            {
                IPEndPoint endpoint = listener.ListeningEndPoint;
                string address = endpoint.Address.ToString();
                if (endpoint.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    address = $"[{address}]";
                yield return $"net.tcp://{address}:{endpoint.Port}/";
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (SocketListener listener in _listeners)
            {
                await listener.StopAsync(cancellationToken);
            }

            _listeners.Clear();
        }
    }
}
#endif
