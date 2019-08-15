using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using CoreWCF.Runtime;
using CoreWCF.Configuration;
using CoreWCF.Security;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace CoreWCF.Channels.Framing
{
    internal class ServerSessionConnectionReaderMiddleware
    {
        private HandshakeDelegate _next;
        private IServiceScopeFactory _servicesScopeFactory;

        public ServerSessionConnectionReaderMiddleware(HandshakeDelegate next, IServiceScopeFactory servicesScopeFactory)
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

            var channel = new ServerFramingDuplexSessionChannel(connection, settings, false, _servicesScopeFactory.CreateScope().ServiceProvider);
            await channel.OpenAsync();
            var channelDispatcher = connection.ServiceDispatcher.CreateServiceChannelDispatcher(channel);
            while (true)
            {
                Message message = await ReceiveMessageAsync(connection);
                if (message == null)
                {
                    return; // No more messages
                }
                var requestContext = new DuplexRequestContext(channel, message, connection.ServiceDispatcher.Binding);
                // TODO: Create a correctly timing out ct
                // We don't await DispatchAsync because in a concurrent service we want to read the next request before the previous
                // request has finished.
                _ = channelDispatcher.DispatchAsync(requestContext, CancellationToken.None);
                // TODO: Now there's a channel dispatcher, have that handle negotiateing a Task which completes when it's time to get the next request
                await requestContext.OperationDispatching;
            }
        }

        private async Task<Message> ReceiveMessageAsync(FramingConnection connection)
        {
            // TODO: Apply timeouts
            Message message;
            ReadOnlySequence<byte> buffer = ReadOnlySequence<byte>.Empty;
            for (; ; )
            {
                var readResult = await connection.Input.ReadAsync();
                if (readResult.IsCompleted || readResult.Buffer.Length == 0)
                {
                    if (!readResult.IsCompleted)
                        connection.Input.AdvanceTo(readResult.Buffer.Start);
                    EnsureDecoderAtEof(connection);
                    connection.EOF = true;
                }

                if (connection.EOF)
                {
                    return null;
                }

                buffer = readResult.Buffer;
                message = DecodeMessage(connection, ref buffer);
                connection.Input.AdvanceTo(buffer.Start);

                if (message != null)
                {
                    PrepareMessage(connection, message);
                    return message;
                }
                else if (connection.EOF) // could have read the END record under DecodeMessage
                {
                    return null;
                }

                if (buffer.Length != 0)
                {
                    throw Fx.AssertAndThrow("Receive: DecodeMessage() should consume the outstanding buffer or return a message.");
                }
            }
        }

        private void PrepareMessage(FramingConnection connection, Message message)
        {
            if (connection.SecurityMessageProperty != null)
            {
                message.Properties.Security = (SecurityMessageProperty)connection.SecurityMessageProperty.CreateCopy();
            }
        }

        private void EnsureDecoderAtEof(FramingConnection connection)
        {
            var decoder = connection.FramingDecoder as ServerSessionDecoder;
            if (!(decoder.CurrentState == ServerSessionDecoder.State.End || decoder.CurrentState == ServerSessionDecoder.State.EnvelopeEnd))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(decoder.CreatePrematureEOFException());
            }
        }

        private Message DecodeMessage(FramingConnection connection, ref ReadOnlySequence<byte> buffer)
        {
            int maxBufferSize = connection.MaxBufferSize;
            var decoder = (ServerSessionDecoder)connection.FramingDecoder;
            while (!connection.EOF && buffer.Length > 0)
            {
                int bytesRead = decoder.Decode(buffer);
                if (bytesRead > 0)
                {
                    if (!connection.EnvelopeBuffer.IsEmpty)
                    {
                        var remainingEnvelopeBuffer = connection.EnvelopeBuffer.Slice(connection.EnvelopeOffset, connection.EnvelopeSize - connection.EnvelopeOffset);
                        CopyBuffer(buffer, remainingEnvelopeBuffer, bytesRead);
                        connection.EnvelopeOffset += bytesRead;
                    }

                    buffer = buffer.Slice(bytesRead);
                }

                switch (decoder.CurrentState)
                {
                    case ServerSessionDecoder.State.EnvelopeStart:
                        int envelopeSize = decoder.EnvelopeSize;
                        if (envelopeSize > maxBufferSize)
                        {
                            // TODO: Remove synchronous wait. This is needed because the buffer is passed by ref.
                            connection.SendFaultAsync(FramingEncodingString.MaxMessageSizeExceededFault, connection.ServiceDispatcher.Binding.SendTimeout, TransportDefaults.MaxDrainSize).GetAwaiter().GetResult();

                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                MaxMessageSizeStream.CreateMaxReceivedMessageSizeExceededException(maxBufferSize));
                        }
                        connection.EnvelopeBuffer = connection.BufferManager.TakeBuffer(envelopeSize);
                        connection.EnvelopeSize = envelopeSize;
                        connection.EnvelopeOffset = 0;
                        break;

                    case ServerSessionDecoder.State.EnvelopeEnd:
                        if (!connection.EnvelopeBuffer.IsEmpty)
                        {
                            Message message = null;

                            try
                            {
                                message = connection.MessageEncoder.ReadMessage(
                                    new ArraySegment<byte>(connection.EnvelopeBuffer.ToArray(), 0, connection.EnvelopeSize),
                                    connection.BufferManager, 
                                    connection.FramingDecoder.ContentType);
                            }
                            catch (XmlException xmlException)
                            {
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                    new ProtocolException(SR.MessageXmlProtocolError, xmlException));
                            }

                            connection.EnvelopeBuffer = null;
                            return message;
                        }
                        break;

                    case ServerSessionDecoder.State.End:
                        connection.EOF = true;
                        break;
                }
            }

            return null;
        }

        private void CopyBuffer(ReadOnlySequence<byte> src, Memory<byte> dest, int bytesToCopy)
        {
            Fx.Assert(src.Length >= bytesToCopy, "Trying to copy more bytes than exist in src");
            // Grab only the number of bytes that we want to copy from the source sequence
            src = src.Slice(0, bytesToCopy);
            if(dest.Length < bytesToCopy)
            {
                throw new ArgumentOutOfRangeException(nameof(bytesToCopy));
            }

            var destSpan = dest.Span;
            if (src.IsSingleSegment)
            {
                var srcSpan = src.First.Span;
                srcSpan.CopyTo(destSpan);
            }
            else
            {
                foreach (var segment in src)
                {
                    var srcSpan = segment.Span;
                    if (srcSpan.Length > bytesToCopy)
                    {
                        srcSpan = srcSpan.Slice(0, bytesToCopy);
                    }
                    srcSpan.CopyTo(destSpan);
                    bytesToCopy -= srcSpan.Length;
                    if (bytesToCopy == 0)
                        return;
                    destSpan = destSpan.Slice(srcSpan.Length);
                }
            }
        }
    }
}
