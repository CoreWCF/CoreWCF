// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
#if !NETFRAMEWORK
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreWCF.Channels
{
#if NETFRAMEWORK
    // On .NET Framework, CoreWCF cannot host NetTcp on Kestrel: Kestrel 2.x (the last version
    // supporting .NET Framework) fails to load its transport types when the application graph
    // contains Microsoft.AspNetCore.Connections.Abstractions >= 8.0, e.g. via the SignalR client
    // (see issue #1681). Instead a CoreWCF-owned socket transport is always used here.
    internal class NetTcpHostedService : IHostedService, IAsyncDisposable, IDisposable
    {
        private readonly NetTcpOptions _serverOptions;
        private readonly IServiceBuilder _serviceBuilder;
        private readonly ILogger<NetTcpHostedService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private NetTcpFramingTransportManager _transportManager;
        private bool _started;

        public NetTcpHostedService(IOptions<NetTcpOptions> options, IServer server, IServiceBuilder serviceBuilder, ILogger<NetTcpHostedService> logger, IServiceProvider serviceProvider)
        {
            // We request the IServer in the constructor to trigger the constructor of the implementation
            // so any startup exceptions are thrown via WrappingIServer, same as on other frameworks.
            _ = server;
            _serverOptions = options.Value ?? new NetTcpOptions();
            _serviceBuilder = serviceBuilder;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _started = true;
            _transportManager = _serviceProvider.GetRequiredService<NetTcpFramingTransportManager>();
            await _transportManager.BindAsync(_serverOptions.CodeBackedListenOptions, cancellationToken);

            // On .NET Framework (asp.net core 2.1 hosting) the IServer has already been started
            // before hosted services, so the ServiceBuilder was already opened by WrappingIServer
            // and we can fix up the base addresses now.
            _transportManager.UpdateServiceBuilderBaseAddresses();
            UpdateServerAddressesFeature();
            var serviceBuilder = _serviceProvider.GetRequiredService<IServiceBuilder>() as ICommunicationObject;
            if (serviceBuilder.State == CommunicationState.Opened)
            {
                // Need to resolve the UriPrefixTable as the service builder was already opened
                // before we could hook up NetMessageFramingConnectionHandler.OnServiceBuilderOpened
                // to be called when the service builder is opened. As UriPrefixTable is internal
                // to NetFramingBase, it was also registered as its implemented interface type which
                // allows us to resolve it here.
                _ = _serviceProvider.GetRequiredService<IEnumerable<KeyValuePair<BaseUriWithWildcard, HandshakeDelegate>>>();
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (!_started) return Task.CompletedTask;
            _started = false;
            var transportManager = _transportManager;
            _transportManager = null;
            return transportManager?.StopAsync(cancellationToken) ?? Task.CompletedTask;
        }

        public void Dispose()
        {
            // No need to check if StopAsync has been called as it is a no-op after the first call
            StopAsync(default).GetAwaiter().GetResult();
        }

        public ValueTask DisposeAsync()
        {
            // No need to check if StopAsync has been called as it is a no-op after the first call
            return new ValueTask(StopAsync(default));
        }

        private void UpdateServerAddressesFeature()
        {
            var serverAddressesFeature = _serviceProvider.GetRequiredService<IServer>().Features?.Get<IServerAddressesFeature>();
            if (serverAddressesFeature == null)
            {
                return;
            }

            foreach (string address in _transportManager.GetListeningAddresses())
            {
                serverAddressesFeature.Addresses.Add(address);
            }
        }
    }
#else
    internal class NetTcpHostedService : IHostedService, IAsyncDisposable, IDisposable
    {
        private static bool s_isNetFramework => RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase);

        private readonly NetTcpOptions _serverOptions;
        private readonly IServiceBuilder _serviceBuilder;
        private readonly ILogger<NetTcpHostedService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private KestrelServer _kestrel;
        private CancellationTokenRegistration _applicationStartedRegistration;
        private bool _started;

        public NetTcpHostedService(IOptions<NetTcpOptions> options, IServer server, IServiceBuilder serviceBuilder, ILogger<NetTcpHostedService> logger, IServiceProvider serviceProvider)
        {
            // We request the IServer in the constructor to trigger the constructor of the implementation. If the implementation
            // is KestrelServer it will run NetTcpFramingOptionsSetup.Configure, which we use to detect the server is Kestrel.
            // We can't just examine the type as we wrap it so we can throw any startup exceptions using WrappingIServer.
            // The IServer will have already been started on asp.net core 2.1, but later versions start it after IHostedService's
            _ = server;
            _serverOptions = options.Value ?? new NetTcpOptions();
            _serviceBuilder = serviceBuilder;
            _logger = logger;
            _serviceProvider = serviceProvider;
            IApplicationLifetime appLifetime = _serviceProvider.GetRequiredService<IApplicationLifetime>();
            // asp.net core 2.1 executes the ApplicationStarted registered callback before it starts hosted services
            if (!s_isNetFramework)
            {
                _applicationStartedRegistration = appLifetime.ApplicationStarted.Register(UpdateServerAddressesFeature);
            }
            Init();
        }

        public bool KestrelAlreadyInUse { get; private set; }

        private void Init()
        {
            // Check if Kestrel is already being used and if it is, it was already configured so there's nothing to do.
            var netTcpFramingOptionsSetup = _serviceProvider.GetServices<IConfigureOptions<KestrelServerOptions>>().SingleOrDefault(options => options is NetTcpFramingOptionsSetup) as NetTcpFramingOptionsSetup;
            if (netTcpFramingOptionsSetup != null && netTcpFramingOptionsSetup.ConfigureCalled)
            {
                KestrelAlreadyInUse = true;
                return;
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _started = true;
            if (KestrelAlreadyInUse) return;

            var transportFactory = _serviceProvider.GetRequiredService<SocketTransportFactory>();
            _kestrel = ActivatorUtilities.CreateInstance(_serviceProvider, typeof(KestrelServer), transportFactory) as KestrelServer;

            await _kestrel.StartAsync<int>(null, cancellationToken);

            // As we don't register the ApplicationStarted callback when running on .NET Framework, and we know that the IServer
            // has already started before us, we can fixup the server addresses now.
            if (s_isNetFramework)
            {
                UpdateServerAddressesFeature();
                // Need to update base addresses as ServiceBuilder was opened by wrapping WrappingIServer
                var framingOptionsSetup = _serviceProvider.GetRequiredService<NetTcpFramingOptionsSetup>();
                framingOptionsSetup.UpdateServiceBuilderBaseAddresses();
                var serviceBuilder = _serviceProvider.GetRequiredService<IServiceBuilder>() as ICommunicationObject;
                if (serviceBuilder.State == CommunicationState.Opened)
                {
                    // Need to resolve the UriPrefixTable as the service builder was already opened
                    // before we could hook up NetMessageFramingConnectionHandler.OnServiceBuilderOpened
                    // to be called when the service builder is opened. As UriPrefixTable is internal
                    // to NetFramingBase, it was also registered as its implemented interface type which
                    // allows us to resolve it here.
                    _ = _serviceProvider.GetRequiredService<IEnumerable<KeyValuePair<BaseUriWithWildcard, HandshakeDelegate>>>();
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (!_started) return Task.CompletedTask;
            _started = false;
            if (!s_isNetFramework)
            {
                _applicationStartedRegistration.Dispose();
            }

            if (KestrelAlreadyInUse) return Task.CompletedTask;
            return _kestrel.StopAsync(cancellationToken);
        }

        public void Dispose()
        {
            // No need to check if StopAsync has been called as it is a no-op after the first call
            StopAsync(default).GetAwaiter().GetResult();
        }

        public ValueTask DisposeAsync()
        {
            // No need to check if StopAsync has been called as it is a no-op after the first call
            return new ValueTask(StopAsync(default));
        }

        private void UpdateServerAddressesFeature()
        {
            // This method needs to be called from appLifetime.ApplicationStarted otherwise an IServer might try to listen on
            // the address that Kestrel is listening on. It's not needed if Kestrel is the IServer implementation for the WebHost
            if (KestrelAlreadyInUse) return;
            var kestrelServerAddresses = _kestrel.Features.Get<IServerAddressesFeature>();
            var serverAddressesFeature = _serviceProvider.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
            foreach (var address in kestrelServerAddresses.Addresses)
            {
                serverAddressesFeature.Addresses.Add(address);
            }
        }
    }
#endif
}
