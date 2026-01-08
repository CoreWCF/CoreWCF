// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreWCF.Channels
{
    [SupportedOSPlatform("windows")]
    internal class NetNamedPipeHostedService : IHostedService, IAsyncDisposable, IDisposable
    {
        private NetNamedPipeOptions _serverOptions;
        private ILoggerFactory _loggerFactory;
        private IServiceBuilder _serviceBuilder;
        private IServiceProvider _serviceProvider;
        private NamedPipeTransportManager _namedPipeListener;

        public NetNamedPipeHostedService(IOptions<NetNamedPipeOptions> options, IServiceBuilder serviceBuilder, ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
        {
            _serverOptions = options.Value ?? new NetNamedPipeOptions();
            _loggerFactory = loggerFactory;
            _serviceBuilder = serviceBuilder;
            _serviceProvider = serviceProvider;
            foreach (var listenOptions in _serverOptions.CodeBackedListenOptions)
            {
                serviceBuilder.BaseAddresses.Add(listenOptions.BaseAddress);
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_serviceBuilder.State == CommunicationState.Opened)
            {
                // Need to resolve the UriPrefixTable as the service builder was already opened
                // before we could hook up NetMessageFramingConnectionHandler.OnServiceBuilderOpened
                // to be called when the service builder is opened. As UriPrefixTable is internal
                // to NetFramingBase, it was also registered as its implemented interface type which
                // allows us to resolve it here.
                _ = _serviceProvider.GetRequiredService<IEnumerable<KeyValuePair<BaseUriWithWildcard, HandshakeDelegate>>>();
            }
            _namedPipeListener = new NamedPipeTransportManager(_loggerFactory);
            await _namedPipeListener.BindAsync(_serverOptions.CodeBackedListenOptions, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            var pipeListener = _namedPipeListener;
            _namedPipeListener = null;
            return pipeListener?.StopAsync(cancellationToken) ?? Task.CompletedTask;
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
    }
}
