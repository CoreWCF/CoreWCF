// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreWCF.Channels
{
    internal class NetTcpHostedService : IHostedService, IAsyncDisposable, IDisposable
    {
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
            // The IServer is started after IHostedService's, so the ApplicationStarted callback is where the server
            // addresses can safely be fixed up.
            _ = server;
            _serverOptions = options.Value ?? new NetTcpOptions();
            _serviceBuilder = serviceBuilder;
            _logger = logger;
            _serviceProvider = serviceProvider;
            IApplicationLifetime appLifetime = _serviceProvider.GetRequiredService<IApplicationLifetime>();
            _applicationStartedRegistration = appLifetime.ApplicationStarted.Register(UpdateServerAddressesFeature);
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
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (!_started) return Task.CompletedTask;
            _started = false;
            _applicationStartedRegistration.Dispose();

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
}
