// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using System;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels.Framing;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels
{
    // .NET Framework socket listener used instead of Kestrel's socket transport (see issue #1681).
    // Modeled on NamedPipeListener in CoreWCF.NetNamedPipe.
    internal class SocketListener
    {
        private readonly TcpListenOptions _options;
        private readonly IPEndPoint _endPoint;
        private readonly ConnectionDelegate _connectionDelegate;
        private readonly ILogger _logger;
        private readonly ILogger<FramingConnection> _framingConnectionLogger;
        private readonly PipeOptions _inputOptions;
        private readonly PipeOptions _outputOptions;
        private readonly int _connectionBufferSize;
        private readonly CancellationTokenSource _stoppingCts = new CancellationTokenSource();
        private readonly ConcurrentDictionary<long, SocketConnectionContext> _connections = new ConcurrentDictionary<long, SocketConnectionContext>();
        private Socket _listenSocket;
        private Task _acceptLoopTask;
        private long _nextConnectionId;

        public SocketListener(TcpListenOptions options, IPEndPoint endPoint, ConnectionDelegate connectionDelegate, ILogger logger, ILogger<FramingConnection> framingConnectionLogger)
        {
            _options = options;
            _endPoint = endPoint;
            _connectionDelegate = connectionDelegate;
            _logger = logger;
            _framingConnectionLogger = framingConnectionLogger;
            _connectionBufferSize = options.ConnectionBufferSize;
            _inputOptions = new PipeOptions(null, PipeScheduler.ThreadPool, PipeScheduler.Inline, _connectionBufferSize, _connectionBufferSize / 2, useSynchronizationContext: false);
            _outputOptions = new PipeOptions(null, PipeScheduler.Inline, PipeScheduler.ThreadPool, _connectionBufferSize, _connectionBufferSize / 2, useSynchronizationContext: false);
        }

        public IPEndPoint ListeningEndPoint { get; private set; }

        public void Bind()
        {
            var listenSocket = new Socket(_endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            if (_endPoint.Address.Equals(IPAddress.IPv6Any))
            {
                // Matches Kestrel's socket transport behavior of accepting IPv4 traffic too.
                listenSocket.DualMode = true;
            }

            listenSocket.Bind(_endPoint);
            listenSocket.Listen(512);
            _listenSocket = listenSocket;
            ListeningEndPoint = (IPEndPoint)listenSocket.LocalEndPoint;
            // Publish the bound endpoint so TcpListenOptions.IPEndpoint reports the actual port,
            // including when port 0 (dynamic port) was requested.
            _options.BoundEndPoint = ListeningEndPoint;
        }

        public void StartAccepting()
        {
            _acceptLoopTask = Task.Run(AcceptLoopAsync);
        }

        private async Task AcceptLoopAsync()
        {
            while (!_stoppingCts.IsCancellationRequested)
            {
                Socket acceptedSocket;
                try
                {
                    acceptedSocket = await Task.Factory.FromAsync(_listenSocket.BeginAccept, _listenSocket.EndAccept, null);
                }
                catch (ObjectDisposedException)
                {
                    // The listen socket was closed as part of shutdown.
                    break;
                }
                catch (SocketException) when (_stoppingCts.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException se)
                {
                    // A connection was reset while it was in the backlog. Keep accepting.
                    _logger.LogDebug(se, "Accept failed on {EndPoint}, continuing to accept connections.", ListeningEndPoint);
                    continue;
                }

                _ = HandleConnectionAsync(acceptedSocket);
            }
        }

        private async Task HandleConnectionAsync(Socket socket)
        {
            long id = Interlocked.Increment(ref _nextConnectionId);
            SocketConnectionContext connection = null;
            try
            {
                try { socket.NoDelay = true; } catch (SocketException) { }
                connection = new SocketConnectionContext(socket, _inputOptions, _outputOptions, _connectionBufferSize);
                var logger = new ConnectionIdWrappingLogger(_framingConnectionLogger, connection.ConnectionId);
                connection.Features.Set<ILogger>(logger);
                connection.Logger = logger;
                // Same transport wrapping as the Kestrel-based path in NetTcpFramingOptionsSetup.
#if DEBUG
                connection.Transport = new NetTcpExceptionConvertingDuplexPipe(new LoggingDuplexPipe(connection.Transport, logger) { LoggingEnabled = true });
#else
                connection.Transport = new NetTcpExceptionConvertingDuplexPipe(connection.Transport);
#endif
                _connections.TryAdd(id, connection);
                connection.Start();
                await _connectionDelegate(connection);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while processing net.tcp connection on {EndPoint}.", ListeningEndPoint);
            }
            finally
            {
                _connections.TryRemove(id, out _);
                if (connection != null)
                {
                    await connection.DisposeAsync();
                }
                else
                {
                    socket.Dispose();
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _stoppingCts.Cancel();
            _listenSocket?.Dispose();

            foreach (var connection in _connections.Values)
            {
                connection.Abort(new ConnectionAbortedException("The server is shutting down."));
            }

            if (_acceptLoopTask != null)
            {
                await _acceptLoopTask;
            }
        }
    }
}
#endif
