// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using Microsoft.AspNetCore.Connections;
using PipeOptions = System.IO.Pipelines.PipeOptions;

namespace CoreWCF.Channels
{
    // When we eventually move to .NET 6, implement IThreadPoolWorkItem
    internal class NamedPipeConnectionContext : DefaultConnectionContext, IAsyncDisposable
    {
        private static readonly ConnectionAbortedException s_sendGracefullyCompletedException = new ConnectionAbortedException("The named pipe transport's send loop completed gracefully.");

        private NamedPipeServerStream _pipe;
        private string _connectionId;
        private IDuplexPipe _originalTransport;
        private int _connectionBufferSize;
        private byte[] _readBuffer;

        private readonly CancellationTokenSource _connectionClosedTokenSource = new CancellationTokenSource();
        private bool _connectionShutdown;
        private bool _connectionClosed;
        private Exception? _shutdownReason;
        private bool _streamDisconnected;
        private readonly object _shutdownLock = new object();
        internal Task _receivingTask = Task.CompletedTask;
        internal Task _sendingTask = Task.CompletedTask;
        private byte[] _writeBuffer;

        internal NamedPipeConnectionContext(NamedPipeServerStream pipe, PipeOptions inputOptions, PipeOptions outputOptions, int connectionBufferSize) : base()
        {
            _pipe = pipe;
            var input = new Pipe(inputOptions);
            var output = new Pipe(outputOptions);
            Transport = _originalTransport = DuplexPipe.CreateTransport(input, output);
            Application = DuplexPipe.CreateApplication(input, output);
            _connectionBufferSize = connectionBufferSize;
        }

        public override string ConnectionId
        {
            get => _connectionId ??= CorrelationIdGenerator.GetNextId();
            set => _connectionId = value;
        }

        public PipeWriter Input => Application.Output;
        public PipeReader Output => Application.Input;

        public NetNamedPipeTrace Logger { get; internal set; }

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
                Logger.LogConnectionError(0, ex, $"Unexpected exception in {nameof(NamedPipeConnection)}.{nameof(Start)}.");
            }
        }

        private async Task DoReceiveAsync()
        {
            Exception? error = null;

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
                        bytesReceived = await _pipe.ReadAsync(byteArray.Array, byteArray.Offset, byteArray.Count);
                    }
                    else
                    {
                        _readBuffer ??= Fx.AllocateByteArray(_connectionBufferSize);
                        bytesReceived = await _pipe.ReadAsync(_readBuffer, 0, _readBuffer.Length);
                        new Memory<byte>(_readBuffer, 0, bytesReceived).CopyTo(buffer);
                    }

                    if (bytesReceived == 0)
                    {
                        // Read completed.
                        Logger.ConnectionReadEnd(ConnectionId);
                        break;
                    }

                    input.Advance(bytesReceived);

                    var flushTask = Input.FlushAsync();

                    var paused = !flushTask.IsCompleted;

                    if (paused)
                    {
                        Logger.ConnectionPause(ConnectionId);
                    }

                    var result = await flushTask;

                    if (paused)
                    {
                        Logger.ConnectionResume(ConnectionId);
                    }

                    if (result.IsCompleted || result.IsCanceled)
                    {
                        // Pipe consumer is shut down, do we stop writing
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
                    Logger.ConnectionError(ConnectionId, error);
                }
            }
            catch (Exception ex)
            {
                // This is unexpected.
                error = ex;
                Logger.ConnectionError(ConnectionId, error);
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
            Exception? shutdownReason = null;
            Exception? unexpectedError = null;

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
            catch (Exception ex)
            {
                shutdownReason = ex;
                unexpectedError = ex;
                Logger.ConnectionError(ConnectionId, unexpectedError);
            }
            finally
            {
                Shutdown(shutdownReason);

                // Complete the output after disposing the socket
                Output.Complete(unexpectedError);

                // Cancel any pending flushes so that the input loop is un-paused
                Input.CancelPendingFlush();
            }
        }

        private async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer)
        {
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> byteArray))
            {
                await _pipe.WriteAsync(byteArray.Array, byteArray.Offset, byteArray.Count);
            }
            else
            {
                _writeBuffer ??= Fx.AllocateByteArray(_connectionBufferSize);
                if (buffer.Length <= _connectionBufferSize)
                {
                    buffer.CopyTo(_writeBuffer);
                    await _pipe.WriteAsync(_writeBuffer, 0, buffer.Length);
                }
                else
                {
                    while(buffer.Length > _connectionBufferSize)
                    {
                        buffer.Slice(0, _connectionBufferSize).CopyTo(_writeBuffer);
                        await _pipe.WriteAsync(_writeBuffer, 0, _connectionBufferSize);
                        buffer = buffer.Slice(_connectionBufferSize);
                    }
                    buffer.CopyTo(_writeBuffer);
                    await _pipe.WriteAsync(_writeBuffer, 0, buffer.Length);
                }
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

                // Make sure to close the connection only after the _aborted flag is set.
                // Without this, the RequestsCanBeAbortedMidRead test will sometimes fail when
                // a BadHttpRequestException is thrown instead of a TaskCanceledException.
                _connectionShutdown = true;

                // shutdownReason should only be null if the output was completed gracefully, so no one should ever
                // ever observe the nondescript ConnectionAbortedException except for connection middleware attempting
                // to half close the connection which is currently unsupported.
                _shutdownReason = shutdownReason ?? s_sendGracefullyCompletedException;
                Logger.ConnectionDisconnect(ConnectionId, _shutdownReason.Message);

                try
                {
                    _pipe.Disconnect();
                    _streamDisconnected = true;
                }
                catch
                {
                    // Ignore any errors from NamedPipeStream.Disconnect() since we're tearing down the connection anyway.
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

            CancelConnectionClosedToken();
        }

        private void CancelConnectionClosedToken()
        {
            try
            {
                _connectionClosedTokenSource.Cancel();
            }
            catch (Exception ex)
            {
                Logger.LogConnectionError(0, ex, $"Unexpected exception in {nameof(NamedPipeConnectionContext)}.{nameof(CancelConnectionClosedToken)}.");
            }
        }

        public async ValueTask DisposeAsync()
        {
            _originalTransport.Input.Complete();
            _originalTransport.Output.Complete();

            try
            {
                // Now wait for both to complete
                await _receivingTask;
                await _sendingTask;
            }
            catch (Exception ex)
            {
                Logger.LogConnectionError(0, ex, $"Unexpected exception in {nameof(NamedPipeConnection)}.{nameof(Start)}.");
                _pipe.Dispose();
                return;
            }

            // TODO: Consider pooling NamedPipeServerStream instances
            _pipe.Dispose();
        }

        public override void Abort(ConnectionAbortedException abortReason)
        {
            Debug.Assert(Application != null);
            Application.Input.CancelPendingRead();
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
