// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Hosting.Server;
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
        private NamedPipeTransportManager _namedPipeListener;

        public NetNamedPipeHostedService(IOptions<NetNamedPipeOptions> options, IServer server, IServiceBuilder serviceBuilder, ILoggerFactory loggerFactory)
        {
            _serverOptions = options.Value ?? new NetNamedPipeOptions();
            _loggerFactory = loggerFactory;
            foreach (var listenOptions in _serverOptions.CodeBackedListenOptions)
            {
                serviceBuilder.BaseAddresses.Add(listenOptions.BaseAddress);
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
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
