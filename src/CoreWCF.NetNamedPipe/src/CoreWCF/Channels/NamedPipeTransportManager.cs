// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels
{
    internal class NamedPipeTransportManager
    {
        private readonly ILogger<NamedPipeTransportManager> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly Dictionary<Uri, NamedPipeListener> _listeners;

        public NamedPipeTransportManager(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<NamedPipeTransportManager>();
            _listeners = new Dictionary<Uri, NamedPipeListener>();
        }

        internal async Task BindAsync(List<NamedPipeListenOptions> listenOptions, CancellationToken cancellationToken)
        {
            Fx.Assert(listenOptions.Count > 0, "We must have at least one listen option to bind to");
            foreach (var listenOption in listenOptions)
            {
                var listener = new NamedPipeListener(listenOption);
                await listener.BindAsync(cancellationToken);
                listener.StartAccepting();
                _logger.LogInformation("Started accepting new NetNamedPipe connections for uri {baseUri}", listenOption.BaseAddress);
                _listeners.Add(listenOption.BaseAddress, listener);
            }
        }

        internal async Task StopAsync(CancellationToken cancellationToken)
        {
            foreach(var pair in _listeners)
            {
                var listenerBaseAddress = pair.Key;
                var listener = pair.Value;
                _logger.LogInformation("Stopping NetNamedPipe for uri {baseUri}", listenerBaseAddress);
                // Might want to consider not awaiting, but collecting Task's in array and then calling
                // Task.WhenAll as this currently stops them sequentially. This is probably fine as
                // stopping should be a quick action.
                await listener.StopAsync(cancellationToken);
            }
        }
    }
}
