// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using System;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoreWCF.Channels
{
    // .NET Framework replacement for Kestrel's socket transport connection. Kestrel 2.x types
    // deriving from ConnectionContext fail to load when Microsoft.AspNetCore.Connections.Abstractions
    // >= 8.0 is in the app (see issue #1681), so CoreWCF owns the socket pumping here.
    // Modeled on NamedPipeConnectionContext in CoreWCF.NetNamedPipe.
    internal class SocketConnectionContext : DefaultConnectionContext, IAsyncDisposable
    {
        private static readonly ConnectionAbortedException s_sendGracefullyCompletedException = new ConnectionAbortedException("The socket transport's send loop completed gracefully.");

        private readonly Socket _socket;
        private readonly NetworkStream _stream;
        private readonly int _connectionBufferSize;
        private readonly IDuplexPipe _originalTransport;
        private byte[] _readBuffer;
        private byte[] _writeBuffer;

        private readonly CancellationTokenSource _connectionClosedTokenSource = new CancellationTokenSource();
        private volatile bool _aborted;
        private bool _connectionShutdown;
        private bool _connectionClosed;
        private Exception _shutdownReason;
        private readonly object _shutdownLock = new object();
        internal Task _receivingTask = Task.CompletedTask;
        internal Task _sendingTask = Task.CompletedTask;

        internal SocketConnectionContext(Socket socket, PipeOptions inputOptions, PipeOptions outputOptions, int connectionBufferSize)
        {
            _socket = socket;
            _stream = new NetworkStream(socket, ownsSocket: false);
            var input = new Pipe(inputOptions);
            var output = new Pipe(outputOptions);
            Transport = _originalTransport = DuplexPipe.CreateTransport(input, output);
            Application = DuplexPipe.CreateApplication(input, output);
            _connectionBufferSize = connectionBufferSize;
            LocalEndPoint = socket.LocalEndPoint;
            RemoteEndPoint = socket.RemoteEndPoint;
            ConnectionClosed = _connectionClosedTokenSource.Token;
        }

        public PipeWriter Input => Application.Output;
        public PipeReader Output => Application.Input;

        public ILogger Logger { get; internal set; } = NullLogger.Instance;

        public void Start()
        {
            try
            {
                // Spawn send and receive logic
                _receivingTask = DoReceiveAsync();
                _sendingTask = DoSendAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected exception in SocketConnectionContext.Start.");
            }
        }

        private async Task DoReceiveAsync()
        {
            Exception error = null;

            try
            {
                var input = Input;
                while (true)
                {
                    // Ensure we have some reasonable amount of buffer space
                    var buffer = input.GetMemory(_connectionBufferSize);
                    int bytesReceived;
                    if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> byteArray))
                    {
                        bytesReceived = await _stream.ReadAsync(byteArray.Array, byteArray.Offset, byteArray.Count);
                    }
                    else
                    {
                        _readBuffer ??= Fx.AllocateByteArray(_connectionBufferSize);
                        bytesReceived = await _stream.ReadAsync(_readBuffer, 0, _readBuffer.Length);
                        new Memory<byte>(_readBuffer, 0, bytesReceived).CopyTo(buffer);
                    }

                    if (bytesReceived == 0)
                    {
                        // Read completed, remote end closed the connection.
                        break;
                    }

                    input.Advance(bytesReceived);

                    var result = await input.FlushAsync();

                    if (result.IsCompleted || result.IsCanceled)
                    {
                        // Pipe consumer is shut down, so we stop writing
                        break;
                    }
                }
            }
            catch (ObjectDisposedException ex)
            {
                // This exception should always be ignored because _shutdownReason should be set.
                error = ex;
                if (!_connectionShutdown)
                {
                    // This is unexpected if the socket hasn't been disposed yet.
                    Logger.LogError(ex, "Unexpected socket read error on connection {ConnectionId}.", ConnectionId);
                }
            }
            catch (System.IO.IOException ex)
            {
                // Socket read failures surface as IOException from NetworkStream. Expected during shutdown/abort.
                error = ex;
                if (!_connectionShutdown)
                {
                    Logger.LogDebug(ex, "Socket read ended with IO error on connection {ConnectionId}.", ConnectionId);
                }
            }
            catch (Exception ex)
            {
                // This is unexpected.
                error = ex;
                Logger.LogError(ex, "Unexpected socket receive loop error on connection {ConnectionId}.", ConnectionId);
            }
            finally
            {
                // If Shutdown() has already been called, assume that was the reason ProcessReceives() exited.
                Input.Complete(_shutdownReason ?? error);

                FireConnectionClosed();
            }
        }

        private async Task DoSendAsync()
        {
            Exception shutdownReason = null;
            Exception unexpectedError = null;

            try
            {
                while (true)
                {
                    var result = await Output.ReadAsync();

                    if (result.IsCanceled)
                    {
                        break;
                    }
                    var buffer = result.Buffer;

                    if (buffer.IsSingleSegment)
                    {
                        // Fast path when the buffer is a single segment.
                        await WriteAsync(buffer.First);
                    }
                    else
                    {
                        foreach (var segment in buffer)
                        {
                            await WriteAsync(segment);
                        }
                    }

                    Output.AdvanceTo(buffer.End);

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            }
            catch (ObjectDisposedException ex)
            {
                // This should always be ignored since Shutdown() must have already been called by Abort().
                shutdownReason = ex;
            }
            catch (System.IO.IOException ex)
            {
                shutdownReason = ex;
            }
            catch (Exception ex)
            {
                shutdownReason = ex;
                unexpectedError = ex;
                Logger.LogError(ex, "Unexpected socket send loop error on connection {ConnectionId}.", ConnectionId);
            }
            finally
            {
                Shutdown(shutdownReason);

                // Complete the output after shutting down the socket
                Output.Complete(unexpectedError);

                // Cancel any pending flushes so that the input loop is un-paused
                Input.CancelPendingFlush();
            }
        }

        private async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer)
        {
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> byteArray))
            {
                await _stream.WriteAsync(byteArray.Array, byteArray.Offset, byteArray.Count);
            }
            else
            {
                _writeBuffer ??= Fx.AllocateByteArray(_connectionBufferSize);
                while (buffer.Length > _connectionBufferSize)
                {
                    buffer.Slice(0, _connectionBufferSize).CopyTo(_writeBuffer);
                    await _stream.WriteAsync(_writeBuffer, 0, _connectionBufferSize);
                    buffer = buffer.Slice(_connectionBufferSize);
                }

                buffer.CopyTo(_writeBuffer);
                await _stream.WriteAsync(_writeBuffer, 0, buffer.Length);
            }
        }

        private void Shutdown(Exception shutdownReason)
        {
            lock (_shutdownLock)
            {
                if (_connectionShutdown)
                {
                    return;
                }

                _connectionShutdown = true;

                // shutdownReason should only be null if the output was completed gracefully.
                _shutdownReason = shutdownReason ?? s_sendGracefullyCompletedException;
                Logger.LogDebug("Connection {ConnectionId} disconnecting: {Reason}", ConnectionId, _shutdownReason.Message);

                try
                {
                    if (_aborted)
                    {
                        // Send a RST so the client observes the abort immediately instead of a
                        // half-open connection (same behavior as the Kestrel-based transport,
                        // see issue #1345).
                        _socket.LingerState = new LingerOption(true, 0);
                        _socket.Dispose();
                    }
                    else
                    {
                        _socket.Shutdown(SocketShutdown.Both);
                    }
                }
                catch
                {
                    // Ignore any errors from socket teardown since we're closing the connection anyway.
                }
            }
        }

        private void FireConnectionClosed()
        {
            // Guard against scheduling this multiple times
            lock (_shutdownLock)
            {
                if (_connectionClosed)
                {
                    return;
                }

                _connectionClosed = true;
            }

            try
            {
                _connectionClosedTokenSource.Cancel();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected exception cancelling the connection closed token.");
            }
        }

        public override void Abort(ConnectionAbortedException abortReason)
        {
            // Cancel the send loop, which then shuts the socket down and unblocks the receive loop.
            _aborted = true;
            Application.Input.CancelPendingRead();
        }

        public override async ValueTask DisposeAsync()
        {
            _originalTransport.Input.Complete();
            _originalTransport.Output.Complete();

            try
            {
                // Now wait for both loops to complete
                await _receivingTask;
                await _sendingTask;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected exception while completing the socket connection pumps.");
            }
            finally
            {
                _stream.Dispose();
                _socket.Dispose();
                _connectionClosedTokenSource.Dispose();
            }
        }

        private class DuplexPipe : IDuplexPipe
        {
            public static IDuplexPipe CreateTransport(Pipe input, Pipe output)
            {
                return new DuplexPipe { Input = input.Reader, Output = output.Writer };
            }

            public static IDuplexPipe CreateApplication(Pipe input, Pipe output)
            {
                return new DuplexPipe { Input = output.Reader, Output = input.Writer };
            }

            public PipeReader Input { get; private set; }
            public PipeWriter Output { get; private set; }
        }
    }
}
#endif
