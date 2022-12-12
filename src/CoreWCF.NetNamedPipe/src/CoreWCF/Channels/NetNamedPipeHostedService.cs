// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreWCF.Channels
{
    internal class NetNamedPipeHostedService : IHostedService
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
            return _namedPipeListener?.StopAsync(cancellationToken) ?? Task.CompletedTask;
        }
    }
}
