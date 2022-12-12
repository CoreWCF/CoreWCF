// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels.Framing;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels
{
    internal class NamedPipeConnection
    {
        private static WaitCallback s_ThreadPoolCallback = ThreadPoolCallback;

        private ConnectionDelegate _connectionDelegate;
        private readonly CancellationTokenSource _connectionClosingCts = new CancellationTokenSource();
        private readonly TaskCompletionSource<object> _completionTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        protected readonly long _id;
        protected readonly TransportConnectionManager _transportConnectionManager;

        public NamedPipeConnection(long id, NamedPipeConnectionContext connectionContext, ConnectionDelegate connectionDelegate, TransportConnectionManager transportConnectionManager, NetNamedPipeTrace logger)
        {
            _id = id;
            ConnectionContext = connectionContext;
            _transportConnectionManager = transportConnectionManager;
            _connectionDelegate = connectionDelegate;
            Logger = logger;
            ConnectionClosedRequested = _connectionClosingCts.Token;
            ConfigureContextLogging();
        }

        private void ConfigureContextLogging()
        {
#if DEBUG
            ConnectionContext.Transport = new NamedPipeExceptionConvertingDuplexPipe(new LoggingDuplexPipe(ConnectionContext.Transport, Logger) { LoggingEnabled = true });
#else
            ConnectionContext.Transport = new NamedPipeExceptionConvertingDuplexPipe(ConnectionContext.Transport);
#endif
        }

        public Task ExecutionTask => _completionTcs.Task;

        public CancellationToken ConnectionClosedRequested { get; set; }

        public NamedPipeConnectionContext ConnectionContext { get; }

        private NetNamedPipeTrace Logger { get; }

        internal void StartDispatching()
        {
            ThreadPool.UnsafeQueueUserWorkItem(s_ThreadPoolCallback, this);
        }

        internal static void ThreadPoolCallback(object state)
        {
            _ = ((NamedPipeConnection)state).ExecuteAsync();
        }

        internal async Task ExecuteAsync()
        {
            var connectionContext = ConnectionContext;

            try
            {
                connectionContext.Start();
                Logger.ConnectionStart(connectionContext.ConnectionId);

                using (BeginConnectionScope())
                {
                    try
                    {
                        await _connectionDelegate(connectionContext);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(0, ex, "Unhandled exception while processing {ConnectionId}.", connectionContext.ConnectionId);
                    }
                }
            }
            finally
            {
                Logger.ConnectionStop(connectionContext.ConnectionId);
                
                // Dispose the transport connection, this needs to happen before removing it from the
                // connection manager so that we only signal completion of this connection after the transport
                // is properly torn down.
                await connectionContext.DisposeAsync();

                _transportConnectionManager.RemoveConnection(_id);
            }
        }

        protected IDisposable BeginConnectionScope()
        {
            if (Logger.IsEnabled(LogLevel.Critical))
            {
                return Logger.BeginScope(new ConnectionLogScope(ConnectionContext.ConnectionId));
            }

            return null;
        }

        public void RequestClose()
        {
            try
            {
                _connectionClosingCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // There's a race where the token could be disposed
                // swallow the exception and no-op
            }
        }

        public void Complete()
        {
            _completionTcs.TrySetResult(null);

            _connectionClosingCts.Dispose();
        }
    }
}
