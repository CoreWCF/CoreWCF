// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO.Pipelines;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels.Framing;
using CoreWCF.Security;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels
{
    [SupportedOSPlatform("windows")]
    internal class NamedPipeListener
    {
        private NamedPipeListenOptions _options;
        private ILogger<NamedPipeListener> _logger;
        private ConnectionDelegate _connectionDelegate;
        private bool _isListening;
        private PipeSharedMemory _sharedMemory;
        private Task[] _tasks;
        private CancellationTokenSource _acceptPumpCancellationTokenSource;
        private bool _firstConnection;
        private ILogger<FramingConnection> _framingConnectionLogger;
        private TransportConnectionManager _transportConnectionManager;
        private readonly PipeOptions _inputOptions;
        private readonly PipeOptions _outputOptions;
        private readonly int _connectionBufferSize;

        public object ThisLock { get; } = new object();

        public NamedPipeListener(NamedPipeListenOptions options)
        {
            _options = options;
            _logger = options.ApplicationServices.GetRequiredService<ILogger<NamedPipeListener>>();
            _framingConnectionLogger = options.ApplicationServices.GetService<ILogger<FramingConnection>>();
            _transportConnectionManager = new TransportConnectionManager();
            options.UseConnectionHandler<NetMessageFramingConnectionHandler>();
            _connectionDelegate = ((IConnectionBuilder)options).Build();
            PipeUri.Validate(options.BaseAddress);
            _inputOptions = new PipeOptions(null, PipeScheduler.ThreadPool, PipeScheduler.Inline, options.ConnectionBufferSize, options.ConnectionBufferSize / 2, useSynchronizationContext: false);
            _outputOptions = new PipeOptions(null, PipeScheduler.Inline, PipeScheduler.ThreadPool, options.ConnectionBufferSize, options.ConnectionBufferSize / 2, useSynchronizationContext: false);
            _connectionBufferSize = (int)_inputOptions.PauseWriterThreshold;
        }

        private void AddNamedPipeListenOptionsToConnectionContext(NamedPipeListenOptions options)
        {
            options.Use(next =>
            {
                return (ConnectionContext context) =>
                {
                    context.Features.Set<NetFramingListenOptions>(options);
                    return next(context);
                };
            });
        }

        // Creates the memory mapped file and the first named pipe instance.
        internal Task BindAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) { return Task.FromCanceled(cancellationToken); }
            Listen();
            return Task.CompletedTask;
        }

        private void Listen()
        {
            lock (ThisLock)
            {
                if (!_isListening)
                {
                    string sharedMemoryName = PipeUri.BuildSharedMemoryName(_options.BaseAddress, _options.HostNameComparisonMode, true);
                    if (!PipeSharedMemory.TryCreate(_options.InternalAllowedUsers, _options.BaseAddress, sharedMemoryName, _logger, out _sharedMemory))
                    {
                        PipeSharedMemory tempSharedMemory = null;

                        // first see if we're in RANU by creating a unique Uri in the global namespace
                        Uri tempUri = new Uri(_options.BaseAddress, Guid.NewGuid().ToString());
                        string tempSharedMemoryName = PipeUri.BuildSharedMemoryName(tempUri, _options.HostNameComparisonMode, true);
                        if (PipeSharedMemory.TryCreate(_options.InternalAllowedUsers, tempUri, tempSharedMemoryName, _logger, out tempSharedMemory))
                        {
                            _logger.LogDebug("Failed to create shared memory {sharedMemoryName} for pipe Uri {pipeUri} as it's already in use by another process", sharedMemoryName, _options.BaseAddress);
                            // we're not RANU, throw PipeNameInUse
                            tempSharedMemory.Dispose();
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                PipeSharedMemory.CreatePipeNameInUseException(UnsafeNativeMethods.ERROR_ACCESS_DENIED, _options.BaseAddress));
                        }
                        else
                        {
                            _logger.LogDebug("Failed to create global shared memory {sharedMemoryName} for pipe Uri {pipeUri}, attempting to create in local session", sharedMemoryName, _options.BaseAddress);
                            // try the session namespace since we're RANU
                            bool success = false;
                            sharedMemoryName = PipeUri.BuildSharedMemoryName(_options.BaseAddress, _options.HostNameComparisonMode, false);
                            try
                            {
                                _sharedMemory = PipeSharedMemory.Create(_options.InternalAllowedUsers, _options.BaseAddress, sharedMemoryName, _logger);
                                success = true;
                            }
                            finally
                            {
                                if (!success)
                                {
                                    _logger.LogDebug("Failed to create local sesion shared memory {sharedMemoryName} for pipe Uri {pipeUri}", sharedMemoryName, _options.BaseAddress);
                                }
                            }
                        }
                    }

                    _logger.LogDebug("Created shared memory {sharedMemoryName} for pipe Uri {pipeUri}", sharedMemoryName, _options.BaseAddress);
                    _isListening = true;
                }
            }
        }

        internal void StartAccepting()
        {
            _tasks = new Task[_options.MaxPendingAccepts];
            _acceptPumpCancellationTokenSource = new CancellationTokenSource();
            for (int i = 0; i < _options.MaxPendingAccepts; i++)
            {
                _tasks[i] = StartPumpAsync(_acceptPumpCancellationTokenSource.Token);
            }
        }

        private async Task StartPumpAsync(CancellationToken token)
        {
            var loggerFactory = _options.ApplicationServices.GetRequiredService<ILoggerFactory>();
            while (!token.IsCancellationRequested)
            {
                var pipe = PipeStreamHelper.CreatePipeStream(_options, _sharedMemory.PipeName, ref _firstConnection);
                await pipe.WaitForConnectionAsync();
                // Add the connection to the connection manager before we queue it for execution
                var connectionContext = new NamedPipeConnectionContext(pipe, _inputOptions, _outputOptions, _options.ConnectionBufferSize);
                var logger = new ConnectionIdWrappingLogger(_framingConnectionLogger, connectionContext.ConnectionId);
                connectionContext.Features.Set<ILogger>(logger);
                var id = _transportConnectionManager.GetNewConnectionId();
                var trace = new NetNamedPipeTrace(loggerFactory, logger);
                connectionContext.Logger = trace;
                var connection = new NamedPipeConnection(id, connectionContext, _connectionDelegate, _transportConnectionManager, trace);
                _transportConnectionManager.AddConnection(id, connection);
                trace.ConnectionAccepted(connectionContext.ConnectionId);
                connection.StartDispatching();
            }
        }

        internal Task StopAsync(CancellationToken cancellationToken)
        {
            // TODO: Signal the main dispatcher we're closing down and give it some time to cleanly
            // close the channels. If it doesn't close them in time, we'll close the connection from
            // underneath it to avoid leaking the connection.
            // For now we just close the connection from underneath it.
            _sharedMemory?.Dispose();
            _sharedMemory = null;
            return _transportConnectionManager.CloseAllConnectionsAsync(cancellationToken);
        }
    }
}
