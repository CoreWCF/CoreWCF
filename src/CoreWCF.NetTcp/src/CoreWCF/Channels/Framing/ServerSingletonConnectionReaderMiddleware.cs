using CoreWCF.Configuration;
using CoreWCF.Runtime;
using CoreWCF.Security;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace CoreWCF.Channels.Framing
{
    internal class ServerSingletonConnectionReaderMiddleware
    {
        private HandshakeDelegate _next;
        private IServiceScopeFactory _servicesScopeFactory;

        public ServerSingletonConnectionReaderMiddleware(HandshakeDelegate next, IServiceScopeFactory servicesScopeFactory)
        {
            _next = next;
            _servicesScopeFactory = servicesScopeFactory;
        }

        public async Task OnConnectedAsync(FramingConnection connection)
        {
            var be = connection.ServiceDispatcher.Binding.CreateBindingElements();
            var tbe = be.Find<TransportBindingElement>();
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
            var timeoutHelper = new TimeoutHelper(settings.ReceiveTimeout);
            var channel = new InputChannel(settings, null, _servicesScopeFactory.CreateScope().ServiceProvider);
            await channel.OpenAsync();
            var channelDispatcher = connection.ServiceDispatcher.CreateServiceChannelDispatcher(channel);

            // TODO: I think that the receive timeout starts counting at the start of the preamble on .NET Framework. This implementation basically resets the timer
            // after the preamble has completed. This probably needs to be addressed otherwise worse case you could end up taking 2X as long to timeout.
            // I believe the preamble should really use the OpenTimeout but that's not how this is implemented on .NET Framework.

            var requestContext = (StreamedFramingRequestContext)await ReceiveRequestAsync(connection, timeoutHelper.RemainingTime());
            _ = channelDispatcher.DispatchAsync(requestContext, CancellationToken.None);
            await requestContext.ReplySent;
        }

        public async Task<RequestContext> ReceiveRequestAsync(FramingConnection connection, TimeSpan timeout)
        {
            (Message requestMessage, Stream inputStream) = await ReceiveAsync(connection, timeout);
            return new StreamedFramingRequestContext(connection, requestMessage, inputStream);
        }

        public async Task<(Message,Stream)> ReceiveAsync(FramingConnection connection, TimeSpan timeout)
        {
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            ReadOnlySequence<byte> buffer = ReadOnlySequence<byte>.Empty;
            for (; ; )
            {
                var readResult = await connection.Input.ReadAsync();
                await Task.Yield();
                if (readResult.IsCompleted || readResult.Buffer.Length == 0)
                {
                    if (!readResult.IsCompleted)
                        connection.Input.AdvanceTo(readResult.Buffer.Start);
                    //EnsureDecoderAtEof(connection);
                    connection.EOF = true;
                }

                if (connection.EOF)
                {
                    await connection.DoneReceivingAsync(timeoutHelper.RemainingTime());
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
                    await connection.DoneReceivingAsync(timeoutHelper.RemainingTime());
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

                Message message = null;
                try
                {
                    // TODO: Make async
                    message = connection.MessageEncoderFactory.Encoder.ReadMessage(
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
            // TODO: RemoteIPEndPoint
            //IPEndPoint remoteEndPoint = connection.this.rawConnection.RemoteIPEndPoint;

            // pipes will return null
            //if (remoteEndPoint != null)
            //{
            //    RemoteEndpointMessageProperty remoteEndpointProperty = new RemoteEndpointMessageProperty(remoteEndPoint);
            //    message.Properties.Add(RemoteEndpointMessageProperty.Name, remoteEndpointProperty);
            //}

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
        class SingletonInputConnectionStream : Stream
        {
            private FramingConnection _connection;
            private IDefaultCommunicationTimeouts _timeouts;
            SingletonMessageDecoder decoder;
            ReadOnlySequence<byte> _buffer = ReadOnlySequence<byte>.Empty;
            bool atEof;
            int chunkBytesRemaining;
            private TimeoutHelper _timeoutHelper;

            public SingletonInputConnectionStream(FramingConnection connection,
                IDefaultCommunicationTimeouts defaultTimeouts)
            {
                _connection = connection;
                _timeouts = defaultTimeouts;
                decoder = new SingletonMessageDecoder();
                chunkBytesRemaining = 0;
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

            void AbortReader()
            {
                _connection.Abort();
            }

            public override void Close()
            {
                _connection.EOF = atEof;
            }

            // run chunk data through the decoder
            void DecodeData(ReadOnlySequence<byte> buffer)
            {
                while (buffer.Length > 0)
                {
                    int bytesRead = decoder.Decode(buffer);
                    buffer = buffer.Slice(bytesRead);
                    Fx.Assert(decoder.CurrentState == SingletonMessageDecoder.State.ReadingEnvelopeBytes || decoder.CurrentState == SingletonMessageDecoder.State.ChunkEnd, "");
                }
            }

            // run the current data through the decoder to get valid message bytes
            void DecodeSize(ref ReadOnlySequence<byte> buffer)
            {
                while (buffer.Length > 0)
                {
                    int bytesRead = decoder.Decode(buffer);

                    if (bytesRead > 0)
                    {
                        buffer = buffer.Slice(bytesRead);
                    }

                    switch (decoder.CurrentState)
                    {
                        case SingletonMessageDecoder.State.ChunkStart:
                            chunkBytesRemaining = decoder.ChunkSize;
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
                if (_buffer.Length == 0 && !atEof)
                {
                    ReadResult readResult;
                    if (!_connection.Input.TryRead(out readResult))
                    {
                        readResult = await _connection.Input.ReadAsync(token).ConfigureAwait(false);
                    }

                    if (readResult.IsCompleted)
                    {
                        atEof = true;
                        return;
                    }

                    _buffer = readResult.Buffer;
                }
            }

            public override void Flush() { /* NOP */ }

            public override int Read(byte[] buffer, int offset, int count)
            {
                // TODO: Create a ReadByte override which is optimized for that single case
                var ct = _timeoutHelper.GetCancellationToken();
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
                    catch(OperationCanceledException oce)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TimeoutException(SR.Format(SR.ReceiveRequestTimedOutNoLocalAddress, _timeoutHelper.OriginalTimeout), oce));
                    }

                    if (atEof)
                    {
                        return result;
                    }

                    if (chunkBytesRemaining > 0) // We're in the middle of a chunk.
                    {
                        // How many bytes to copy into the buffer passed to this method. The read from the input pipe might have read bytes 
                        // from the next chunk and we're not ready to consume them yet. Also we can't copy more bytes than the passed in buffer.
                        int bytesToCopy = Math.Min((int)Math.Min((int)_buffer.Length, chunkBytesRemaining), count);

                        // When copying a ReadOnlySequence to a Span, they must be the same size so create a temporary
                        // ReadOnlySequence which has the same number of bytes as we wish to copy.
                        var _fromBuffer = _buffer.Slice(_buffer.Start, bytesToCopy);

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
                        chunkBytesRemaining -= bytesToCopy;
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
                var ct = new TimeoutHelper(_timeouts.ReceiveTimeout).GetCancellationToken();
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

                    if (atEof)
                    {
                        return result;
                    }

                    if (chunkBytesRemaining > 0) // We're in the middle of a chunk.
                    {
                        // How many bytes to copy into the buffer passed to this method. The read from the input pipe might have read bytes 
                        // from the next chunk and we're not ready to consume them yet. Also we can't copy more bytes than the passed in buffer.
                        int bytesToCopy = Math.Min((int)Math.Min((int)_buffer.Length, chunkBytesRemaining), count);

                        // When copying a ReadOnlySequence to a Span, they must be the same size so create a temporary
                        // ReadOnlySequence which has the same number of bytes as we wish to copy.
                        var _fromBuffer = _buffer.Slice(_buffer.Start, bytesToCopy);

                        // keep decoder up to date
                        DecodeData(_fromBuffer);

                        // Consume those bytes from our buffer
                        _buffer = _buffer.Slice(bytesToCopy);

                        // If the buffer has been exhausted, advance the input pipe to consume them and release the buffer
                        if (_buffer.Length == 0)
                        {
                            _connection.Input.AdvanceTo(_buffer.End);
                        }

                        // Create an ArraySegment of the right size to copy the bytes to. The synchronous Read method uses a Span<byte> instead, but
                        // you can't instantiate a Span<T> in an async method but there's an implicit case of ArraySegment to Span.
                        var _toBuffer = new ArraySegment<byte>(buffer, offset, bytesToCopy);
                        _fromBuffer.CopyTo(_toBuffer);
                        // Fix up counts
                        result += bytesToCopy;
                        offset += bytesToCopy;
                        count -= bytesToCopy;
                        chunkBytesRemaining -= bytesToCopy;
                    }
                    else
                    {
                        // We are starting a new chunk. Read the size, and loop around again
                        DecodeSize(ref _buffer);
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

            void ProcessEof()
            {
                if (!atEof)
                {
                    atEof = true;
                    if (chunkBytesRemaining > 0 || decoder.CurrentState != SingletonMessageDecoder.State.End)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(decoder.CreatePrematureEOFException());
                    }
                }
            }

            public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
        }
    }
}