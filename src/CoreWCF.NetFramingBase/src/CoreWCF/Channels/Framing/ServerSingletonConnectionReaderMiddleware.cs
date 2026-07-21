// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Configuration;
using CoreWCF.Runtime;
using CoreWCF.Security;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Channels.Framing
{
    internal class ServerSingletonConnectionReaderMiddleware
    {
        private readonly HandshakeDelegate _next;
        private readonly Hashtable _serviceChannelDispatcherCache = new Hashtable();
        private readonly IServiceScopeFactory _servicesScopeFactory;
        private readonly AsyncLock _lock = new AsyncLock();

        public ServerSingletonConnectionReaderMiddleware(HandshakeDelegate next, IServiceScopeFactory servicesScopeFactory)
        {
            _next = next;
            _servicesScopeFactory = servicesScopeFactory;
        }

        public async Task OnConnectedAsync(FramingConnection connection)
        {
            IServiceChannelDispatcher channelDispatcher;
            if (_serviceChannelDispatcherCache.ContainsKey(connection.ServiceDispatcher))
            {
                channelDispatcher = (IServiceChannelDispatcher)_serviceChannelDispatcherCache[connection.ServiceDispatcher];
            }
            else
            {
                await using (await _lock.TakeLockAsync())
                {
                    if (_serviceChannelDispatcherCache.ContainsKey(connection.ServiceDispatcher))
                    {
                        channelDispatcher = (IServiceChannelDispatcher)_serviceChannelDispatcherCache[connection.ServiceDispatcher];
                    }
                    else
                    {
                        BindingElementCollection be = connection.ServiceDispatcher.Binding.CreateBindingElements();
                        TransportBindingElement tbe = be.Find<TransportBindingElement>();
                        ITransportFactorySettings settings = new NetFramingTransportSettings
                        {
                            CloseTimeout = connection.ServiceDispatcher.Binding.CloseTimeout,
                            OpenTimeout = connection.ServiceDispatcher.Binding.OpenTimeout,
                            ReceiveTimeout = connection.ServiceDispatcher.Binding.ReceiveTimeout,
                            SendTimeout = connection.ServiceDispatcher.Binding.SendTimeout,
                            ManualAddressing = tbe.ManualAddressing,
                            BufferManager = connection.BufferManager,
                            MaxReceivedMessageSize = tbe.MaxReceivedMessageSize,
                            MessageEncoderFactory = connection.MessageEncoderFactory
                        };
                        // Even though channel is reused for multiple connections, there are some scoped dependencies used so a scope is needed
                        var replyChannel = new ConnectionOrientedTransportReplyChannel(settings, null, _servicesScopeFactory.CreateScope().ServiceProvider);
                        channelDispatcher = await connection.ServiceDispatcher.CreateServiceChannelDispatcherAsync(replyChannel);
                        _serviceChannelDispatcherCache[connection.ServiceDispatcher] = channelDispatcher;
                    }
                }
            }

            // TODO: I think that the receive timeout starts counting at the start of the preamble on .NET Framework. This implementation basically resets the timer
            // after the preamble has completed. This probably needs to be addressed otherwise worse case you could end up taking 2X as long to timeout.
            // I believe the preamble should really use the OpenTimeout but that's not how this is implemented on .NET Framework.
            var timeoutHelper = new TimeoutHelper(connection.ServiceDispatcher.Binding.ReceiveTimeout);
            StreamedFramingRequestContext requestContext = await ReceiveRequestAsync(connection, timeoutHelper.RemainingTime());
            await channelDispatcher.DispatchAsync(requestContext);
            await requestContext.ReplySent;
        }

        public async Task<StreamedFramingRequestContext> ReceiveRequestAsync(FramingConnection connection, TimeSpan timeout)
        {
            (Message requestMessage, Stream inputStream) = await ReceiveAsync(connection, timeout);
            return new StreamedFramingRequestContext(connection, requestMessage, inputStream);
        }

        public async Task<(Message, Stream)> ReceiveAsync(FramingConnection connection, TimeSpan timeout)
        {
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            ReadOnlySequence<byte> buffer = ReadOnlySequence<byte>.Empty;
            for (; ; )
            {
                ReadResult readResult = await connection.Input.ReadAsync();
                await Task.Yield();
                if (readResult.IsCompleted || readResult.Buffer.Length == 0)
                {
                    if (!readResult.IsCompleted)
                    {
                        connection.Input.AdvanceTo(readResult.Buffer.Start);
                    }
                    //EnsureDecoderAtEof(connection);
                    connection.EOF = true;
                }

                if (connection.EOF)
                {
                    return (null, null);
                }

                buffer = readResult.Buffer;
                bool atEnvelopeStart = DecodeBytes(connection, ref buffer);
                connection.Input.AdvanceTo(buffer.Start);
                if (atEnvelopeStart)
                {
                    break;
                }


                if (connection.EOF)
                {
                    return (null, null);
                }
            }

            // we're ready to read a message
            Stream connectionStream = new SingletonInputConnectionStream(connection, connection.ServiceDispatcher.Binding);
            Stream inputStream = new MaxMessageSizeStream(connectionStream, connection.MaxReceivedMessageSize);
            //using (ServiceModelActivity activity = DiagnosticUtility.ShouldUseActivity ? ServiceModelActivity.CreateBoundedActivity(true) : null)
            //{
            //    if (DiagnosticUtility.ShouldUseActivity)
            //    {
            //        ServiceModelActivity.Start(activity, SR.GetString(SR.ActivityProcessingMessage, TraceUtility.RetrieveMessageNumber()), ActivityType.ProcessMessage);
            //    }

            Message message;
            try
            {
                message = await connection.MessageEncoderFactory.Encoder.ReadMessageAsync(
                    inputStream, connection.MaxBufferSize, connection.FramingDecoder.ContentType);
            }
            catch (XmlException xmlException)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ProtocolException(SR.MessageXmlProtocolError, xmlException));
            }

            //if (DiagnosticUtility.ShouldUseActivity)
            //{
            //    TraceUtility.TransferFromTransport(message);
            //}

            PrepareMessage(connection, message);

            return (message, inputStream);
            //}
        }

        private void PrepareMessage(FramingConnection connection, Message message)
        {
            message.Properties.Via = connection.Via;
            message.Properties.Security = (connection.SecurityMessageProperty != null) ? (SecurityMessageProperty)connection.SecurityMessageProperty.CreateCopy() : null;

            IPEndPoint remoteEndPoint = connection.RemoteEndpoint;

            // pipes will return null
            if (remoteEndPoint != null)
            {
                var remoteEndpointProperty = new RemoteEndpointMessageProperty(remoteEndPoint);
                message.Properties.Add(RemoteEndpointMessageProperty.Name, remoteEndpointProperty);
            }

            // TODO: ChannelBindingToken
            //if (this.channelBindingToken != null)
            //{
            //    ChannelBindingMessageProperty property = new ChannelBindingMessageProperty(this.channelBindingToken, false);
            //    property.AddTo(message);
            //    property.Dispose(); //message.Properties.Add() creates a copy...
            //}
        }

        private bool DecodeBytes(FramingConnection connection, ref ReadOnlySequence<byte> buffer)
        {
            var decoder = connection.FramingDecoder as ServerSingletonDecoder;
            Fx.Assert(decoder != null, "FramingDecoder must be a non-null ServerSingletonDecoder");
            while (!connection.EOF && buffer.Length > 0)
            {
                int bytesRead = decoder.Decode(buffer);
                if (bytesRead > 0)
                {
                    buffer = buffer.Slice(bytesRead);
                }

                switch (decoder.CurrentState)
                {
                    case ServerSingletonDecoder.State.EnvelopeStart:
                        // we're at the envelope
                        return true;

                    case ServerSingletonDecoder.State.End:
                        connection.EOF = true;
                        return false;
                }
            }

            return false;
        }

        // ensures that the reader is notified at end-of-stream, and takes care of the framing chunk headers
        private class SingletonInputConnectionStream : Stream
        {
            private readonly FramingConnection _connection;
            private readonly IDefaultCommunicationTimeouts _timeouts;
            private readonly SingletonMessageDecoder _decoder;
            private ReadOnlySequence<byte> _buffer = ReadOnlySequence<byte>.Empty;
            private bool _atEof;
            private int _chunkBytesRemaining;
            private TimeoutHelper _timeoutHelper;

            public SingletonInputConnectionStream(FramingConnection connection,
                IDefaultCommunicationTimeouts defaultTimeouts)
            {
                _connection = connection;
                _timeouts = defaultTimeouts;
                _decoder = new SingletonMessageDecoder(connection.Logger);
                _chunkBytesRemaining = 0;
                _timeoutHelper = new TimeoutHelper(_timeouts.ReceiveTimeout);
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));

            public override long Position
            {
                get
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));
                }
                set
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));
                }
            }

            private void AbortReader()
            {
                _connection.Abort();
            }

            public override void Close()
            {
                _connection.EOF = _atEof;
            }

            // run chunk data through the decoder
            private void DecodeData(ReadOnlySequence<byte> buffer)
            {
                while (buffer.Length > 0)
                {
                    int bytesRead = _decoder.Decode(buffer);
                    buffer = buffer.Slice(bytesRead);
                    Fx.Assert(_decoder.CurrentState == SingletonMessageDecoder.State.ReadingEnvelopeBytes || _decoder.CurrentState == SingletonMessageDecoder.State.ChunkEnd, "");
                }
            }

            // run the current data through the decoder to get valid message bytes
            private void DecodeSize(ref ReadOnlySequence<byte> buffer)
            {
                while (buffer.Length > 0)
                {
                    int bytesRead = _decoder.Decode(buffer);

                    if (bytesRead > 0)
                    {
                        buffer = buffer.Slice(bytesRead);
                    }

                    switch (_decoder.CurrentState)
                    {
                        case SingletonMessageDecoder.State.ChunkStart:
                            _chunkBytesRemaining = _decoder.ChunkSize;
                            return;
                        case SingletonMessageDecoder.State.End:
                            ProcessEof();
                            return;
                    }
                }
            }

            private void EnsureBuffer(CancellationToken token)
            {
                EnsureBufferAsync(token).GetAwaiter().GetResult();
            }

            private async Task EnsureBufferAsync(CancellationToken token)
            {
                if (_buffer.Length == 0 && !_atEof)
                {
                    if (!_connection.Input.TryRead(out ReadResult readResult))
                    {
                        readResult = await _connection.Input.ReadAsync(token).ConfigureAwait(false);
                    }

                    if (readResult.IsCompleted)
                    {
                        _atEof = true;
                        return;
                    }

                    _buffer = readResult.Buffer;
                }
            }

            public override void Flush() { /* NOP */ }

            public override int Read(byte[] buffer, int offset, int count)
            {
                // TODO: Create a ReadByte override which is optimized for that single case
                CancellationToken ct = _timeoutHelper.GetCancellationToken();
                int result = 0;
                while (true)
                {
                    if (count == 0)
                    {
                        return result;
                    }

                    try
                    {
                        EnsureBuffer(ct);
                    }
                    catch (OperationCanceledException oce)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TimeoutException(SR.Format(SR.ReceiveRequestTimedOutNoLocalAddress, _timeoutHelper.OriginalTimeout), oce));
                    }

                    if (_atEof)
                    {
                        return result;
                    }

                    if (_chunkBytesRemaining > 0) // We're in the middle of a chunk.
                    {
                        // How many bytes to copy into the buffer passed to this method. The read from the input pipe might have read bytes
                        // from the next chunk and we're not ready to consume them yet. Also we can't copy more bytes than the passed in buffer.
                        int bytesToCopy = Math.Min((int)Math.Min((int)_buffer.Length, _chunkBytesRemaining), count);

                        // When copying a ReadOnlySequence to a Span, they must be the same size so create a temporary
                        // ReadOnlySequence which has the same number of bytes as we wish to copy.
                        ReadOnlySequence<byte> _fromBuffer = _buffer.Slice(_buffer.Start, bytesToCopy);

                        // keep decoder up to date
                        DecodeData(_fromBuffer);

                        // Consume those bytes from our buffer
                        _buffer = _buffer.Slice(bytesToCopy);

                        // TODO: Possible perf improvement would be to call ReadAsync and save the Task<ReadResult> with the presumption that it will
                        // likely have been completed before the next call to avoid blocking waiting for the Task to complete.

                        // Create Span of the right size to copy the bytes to.
                        var _toBuffer = new Span<byte>(buffer, offset, bytesToCopy);
                        _fromBuffer.CopyTo(_toBuffer);
                        // Fix up counts
                        result += bytesToCopy;
                        offset += bytesToCopy;
                        count -= bytesToCopy;
                        _chunkBytesRemaining -= bytesToCopy;
                    }
                    else
                    {
                        // We are starting a new chunk. Read the size, and loop around again
                        DecodeSize(ref _buffer);
                    }

                    // If the buffer has been exhausted, advance the input pipe to consume them and release the buffer
                    if (_buffer.Length == 0)
                    {
                        _connection.Input.AdvanceTo(_buffer.End);
                    }

                    //if (atEof)
                    //{
                    //    _connection.Input.Complete();
                    //}
                }
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                CancellationToken ct = TimeoutHelper.GetCancellationToken(_timeouts.ReceiveTimeout);
                int result = 0;
                while (true)
                {
                    if (count == 0)
                    {
                        return result;
                    }

                    try
                    {
                        await EnsureBufferAsync(ct);
                    }
                    catch (OperationCanceledException oce)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TimeoutException(SR.Format(SR.ReceiveRequestTimedOutNoLocalAddress, _timeoutHelper.OriginalTimeout), oce));
                    }

                    if (_atEof)
                    {
                        return result;
                    }

                    if (_chunkBytesRemaining > 0) // We're in the middle of a chunk.
                    {
                        // How many bytes to copy into the buffer passed to this method. The read from the input pipe might have read bytes
                        // from the next chunk and we're not ready to consume them yet. Also we can't copy more bytes than the passed in buffer.
                        int bytesToCopy = Math.Min((int)Math.Min((int)_buffer.Length, _chunkBytesRemaining), count);

                        // When copying a ReadOnlySequence to a Span, they must be the same size so create a temporary
                        // ReadOnlySequence which has the same number of bytes as we wish to copy.
                        ReadOnlySequence<byte> _fromBuffer = _buffer.Slice(_buffer.Start, bytesToCopy);

                        // keep decoder up to date
                        DecodeData(_fromBuffer);

                        // Consume those bytes from our buffer
                        _buffer = _buffer.Slice(bytesToCopy);
                        
                        // Create an ArraySegment of the right size to copy the bytes to. The synchronous Read method uses a Span<byte> instead, but
                        // you can't instantiate a Span<T> in an async method but there's an implicit case of ArraySegment to Span.
                        var _toBuffer = new ArraySegment<byte>(buffer, offset, bytesToCopy);
                        _fromBuffer.CopyTo(_toBuffer);
                        // Fix up counts
                        result += bytesToCopy;
                        offset += bytesToCopy;
                        count -= bytesToCopy;
                        _chunkBytesRemaining -= bytesToCopy;
                    }
                    else
                    {
                        // We are starting a new chunk. Read the size, and loop around again
                        DecodeSize(ref _buffer);
                    }

                    // If the buffer has been exhausted, advance the input pipe to consume them and release the buffer
                    if (_buffer.Length == 0)
                    {
                        _connection.Input.AdvanceTo(_buffer.End);
                    }
                }
            }

            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            {
                return ReadAsync(buffer, offset, count).ToApm(callback, state);
            }

            public override int EndRead(IAsyncResult result)
            {
                return result.ToApmEnd<int>();
            }

            public override long Seek(long offset, SeekOrigin origin) => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));

            public override void SetLength(long value) => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));

            private void ProcessEof()
            {
                if (!_atEof)
                {
                    _atEof = true;
                    if (_chunkBytesRemaining > 0 || _decoder.CurrentState != SingletonMessageDecoder.State.End)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(_decoder.CreatePrematureEOFException());
                    }
                }
            }

            public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
        }
    }
}
