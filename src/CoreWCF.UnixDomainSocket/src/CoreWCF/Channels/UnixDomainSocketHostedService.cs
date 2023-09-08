// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    internal class UnixDomainSocketHostedService : IHostedService, IAsyncDisposable, IDisposable
    {
        private readonly IServiceBuilder _serviceBuilder;
        private readonly ILogger<UnixDomainSocketHostedService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private KestrelServer _kestrel;
        private CancellationTokenRegistration _applicationStartedRegistration;
        private bool _started;

        public UnixDomainSocketHostedService(IServiceBuilder serviceBuilder, ILogger<UnixDomainSocketHostedService> logger, IServiceProvider serviceProvider)
        {
            _serviceBuilder = serviceBuilder;
            _logger = logger;
            _serviceProvider = serviceProvider;
            IApplicationLifetime appLifetime = _serviceProvider.GetRequiredService<IApplicationLifetime>();
            Init();
        }

        private void Init()
        {
            // Check if Kestrel is already being used and if it is, it was already configured so there's nothing to do.
            var unixDomainSocketFramingOptionsSetup = _serviceProvider.GetServices<IConfigureOptions<KestrelServerOptions>>().SingleOrDefault(options => options is UnixDomainSocketFramingOptionsSetup) as UnixDomainSocketFramingOptionsSetup;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _started = true;
            var transportFactory = _serviceProvider.GetRequiredService<SocketTransportFactory>();
            var unixDomainSocketFramingOptionsSetup = _serviceProvider.GetServices<IConfigureOptions<KestrelServerOptions>>().SingleOrDefault(options => options is UnixDomainSocketFramingOptionsSetup) as UnixDomainSocketFramingOptionsSetup;
            unixDomainSocketFramingOptionsSetup.AttachUDS = true;
            _kestrel = ActivatorUtilities.CreateInstance(_serviceProvider, typeof(KestrelServer), transportFactory) as KestrelServer;
            Debug.Assert(unixDomainSocketFramingOptionsSetup.AttachUDS);

            await _kestrel.StartAsync<int>(null, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (!_started) return Task.CompletedTask;
            _started = false;
            return _kestrel.StopAsync(cancellationToken);
        }

        private void UpdateServerAddressesFeature()
        {
            // This method needs to be called from appLifetime.ApplicationStarted otherwise an IServer might try to listen on
            // the address that Kestrel is listening on. It's not needed if Kestrel is the IServer implementation for the WebHost
            var kestrelServerAddresses = _kestrel.Features.Get<IServerAddressesFeature>();
            var serverAddressesFeature = _serviceProvider.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
            foreach (var address in kestrelServerAddresses.Addresses)
            {
                serverAddressesFeature.Addresses.Add(address);
            }
        }

        public void Dispose()
        {
            StopAsync(default).GetAwaiter().GetResult();
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask(StopAsync(default));
        }
    }
}
