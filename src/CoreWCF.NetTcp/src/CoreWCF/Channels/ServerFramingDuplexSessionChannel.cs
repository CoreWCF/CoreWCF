using Microsoft.Extensions.DependencyInjection;
using CoreWCF.Runtime;
using CoreWCF.Security;
using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels.Framing;
using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Buffers;
using System.Xml;

namespace CoreWCF.Channels
{
    internal class ServerFramingDuplexSessionChannel : FramingDuplexSessionChannel
    {
        StreamUpgradeAcceptor upgradeAcceptor;
        private IServiceProvider _serviceProvider;
        IStreamUpgradeChannelBindingProvider channelBindingProvider;
        private CancellationTokenRegistration _applicationStoppingRegistration;

        public ServerFramingDuplexSessionChannel(FramingConnection connection, ITransportFactorySettings settings,
            bool exposeConnectionProperty, IServiceProvider serviceProvider)
            : base(connection, settings, exposeConnectionProperty)
        {
            Connection = connection;
            upgradeAcceptor = connection.StreamUpgradeAcceptor;
            _serviceProvider = serviceProvider;
            SetMessageSource(new ServerSessionConnectionMessageSource(connection));
        }

        protected override void ReturnConnectionIfNecessary(bool abort, CancellationToken token)
        {
            // TODO: Put connection back into the beginning of the middleware stack
            //    IConnection localConnection = null;
            //    if (this.sessionReader != null)
            //    {
            //        lock (ThisLock)
            //        {
            //            localConnection = this.sessionReader.GetRawConnection();
            //        }
            //    }

            //    if (localConnection != null)
            //    {
            //        if (abort)
            //        {
            //            localConnection.Abort();
            //        }
            //        else
            //        {
            //            this.connectionDemuxer.ReuseConnection(localConnection, timeout);
            //        }
            //        this.connectionDemuxer = null;
            //    }
        }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IChannelBindingProvider))
            {
                return (T)(object)channelBindingProvider;
            }

            T service = _serviceProvider.GetService<T>();
            if (service != null)
            {
                return service;
            }

            return base.GetProperty<T>();
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            return Task.CompletedTask; // NO-OP
        }

        protected override void OnOpened()
        {
            base.OnOpened();

            var appLifetime = _serviceProvider.GetRequiredService<IApplicationLifetime>();
            _applicationStoppingRegistration = appLifetime.ApplicationStopping.Register(() =>
            {
                _ = CloseAsync();
            });
        }

        protected override void OnClosing()
        {
            base.OnClosing();
            _applicationStoppingRegistration.Dispose();
        }

        internal class ServerSessionConnectionMessageSource : IMessageSource
        {
            private FramingConnection _connection;

            public ServerSessionConnectionMessageSource(FramingConnection connection)
            {
                _connection = connection;
            }

            public async Task<Message> ReceiveAsync(CancellationToken token)
            {
                // TODO: Apply timeouts
                Message message;
                ReadOnlySequence<byte> buffer = ReadOnlySequence<byte>.Empty;
                for (; ; )
                {
                    var readResult = await _connection.Input.ReadAsync();
                    if (readResult.IsCompleted || readResult.Buffer.Length == 0)
                    {
                        if (!readResult.IsCompleted)
                            _connection.Input.AdvanceTo(readResult.Buffer.Start);
                        EnsureDecoderAtEof();
                        _connection.EOF = true;
                    }

                    if (_connection.EOF)
                    {
                        return null;
                    }

                    buffer = readResult.Buffer;
                    message = DecodeMessage(ref buffer);
                    _connection.Input.AdvanceTo(buffer.Start);

                    if (message != null)
                    {
                        PrepareMessage(message);
                        return message;
                    }
                    else if (_connection.EOF) // could have read the END record under DecodeMessage
                    {
                        return null;
                    }

                    if (buffer.Length != 0)
                    {
                        throw Fx.AssertAndThrow("Receive: DecodeMessage() should consume the outstanding buffer or return a message.");
                    }
                }
            }

            public Task<bool> WaitForMessageAsync(CancellationToken token)
            {
                throw new NotImplementedException();
            }

            private void EnsureDecoderAtEof()
            {
                var decoder = _connection.FramingDecoder as ServerSessionDecoder;
                if (!(decoder.CurrentState == ServerSessionDecoder.State.End || decoder.CurrentState == ServerSessionDecoder.State.EnvelopeEnd))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(decoder.CreatePrematureEOFException());
                }
            }

            private Message DecodeMessage(ref ReadOnlySequence<byte> buffer)
            {
                int maxBufferSize = _connection.MaxBufferSize;
                var decoder = (ServerSessionDecoder)_connection.FramingDecoder;
                while (!_connection.EOF && buffer.Length > 0)
                {
                    int bytesRead = decoder.Decode(buffer);
                    if (bytesRead > 0)
                    {
                        if (!_connection.EnvelopeBuffer.IsEmpty)
                        {
                            var remainingEnvelopeBuffer = _connection.EnvelopeBuffer.Slice(_connection.EnvelopeOffset, _connection.EnvelopeSize - _connection.EnvelopeOffset);
                            CopyBuffer(buffer, remainingEnvelopeBuffer, bytesRead);
                            _connection.EnvelopeOffset += bytesRead;
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
                                _connection.SendFaultAsync(FramingEncodingString.MaxMessageSizeExceededFault, _connection.ServiceDispatcher.Binding.SendTimeout, TransportDefaults.MaxDrainSize).GetAwaiter().GetResult();

                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                    MaxMessageSizeStream.CreateMaxReceivedMessageSizeExceededException(maxBufferSize));
                            }

                            _connection.EnvelopeBuffer = _connection.BufferManager.TakeBuffer(envelopeSize);
                            _connection.EnvelopeSize = envelopeSize;
                            _connection.EnvelopeOffset = 0;
                            break;

                        case ServerSessionDecoder.State.EnvelopeEnd:
                            if (!_connection.EnvelopeBuffer.IsEmpty)
                            {
                                Message message = null;

                                try
                                {
                                    message = _connection.MessageEncoder.ReadMessage(
                                        new ArraySegment<byte>(_connection.EnvelopeBuffer.ToArray(), 0, _connection.EnvelopeSize),
                                        _connection.BufferManager,
                                        _connection.FramingDecoder.ContentType);
                                }
                                catch (XmlException xmlException)
                                {
                                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                        new ProtocolException(SR.MessageXmlProtocolError, xmlException));
                                }

                                _connection.EnvelopeBuffer = null;
                                return message;
                            }
                            break;

                        case ServerSessionDecoder.State.End:
                            _connection.EOF = true;
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
                if (dest.Length < bytesToCopy)
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

            private void PrepareMessage(Message message)
            {
                if (_connection.SecurityMessageProperty != null)
                {
                    message.Properties.Security = (SecurityMessageProperty)_connection.SecurityMessageProperty.CreateCopy();
                }

                IPEndPoint remoteEndPoint = _connection.RemoteEndpoint;

                // pipes will return null
                if (remoteEndPoint != null)
                {
                    var remoteEndpointProperty = new RemoteEndpointMessageProperty(remoteEndPoint);
                    message.Properties.Add(RemoteEndpointMessageProperty.Name, remoteEndpointProperty);
                }
            }
        }
    }

    internal abstract class FramingDuplexSessionChannel : TransportDuplexSessionChannel
    {
        bool exposeConnectionProperty;

        private FramingDuplexSessionChannel(ITransportFactorySettings settings,
            EndpointAddress localAddress, Uri localVia, EndpointAddress remoteAddress, Uri via, bool exposeConnectionProperty)
            : base(settings, localAddress, localVia, remoteAddress, via)
        {
            this.exposeConnectionProperty = exposeConnectionProperty;
        }

        protected FramingDuplexSessionChannel(FramingConnection connection, ITransportFactorySettings settings, bool exposeConnectionProperty)
    : this(settings, new EndpointAddress(connection.ServiceDispatcher.BaseAddress), connection.Via,
    EndpointAddress.AnonymousAddress, connection.MessageEncoder.MessageVersion.Addressing.AnonymousUri, exposeConnectionProperty)
        {
            Session = FramingConnectionDuplexSession.CreateSession(this, connection.StreamUpgradeAcceptor);
        }

        protected FramingConnection Connection { get; set; }

        protected override bool IsStreamedOutput
        {
            get { return false; }
        }

        protected override async Task CloseOutputSessionCoreAsync(CancellationToken token)
        {
            Connection.RawStream?.StartUnwrapRead();
            try
            {
                await Connection.Output.WriteAsync(SessionEncoder.EndBytes, token);
                await Connection.Output.FlushAsync();
            }
            finally
            {
                if (Connection.RawStream != null)
                {
                    await Connection.RawStream.FinishUnwrapReadAsync();
                    Connection.RawStream = null;
                    Connection.Output.Complete();
                    Connection.Input.Complete();
                }
            }
        }

        protected override Task CompleteCloseAsync(CancellationToken token)
        {
            ReturnConnectionIfNecessary(false, token);
            return Task.CompletedTask;
        }

        protected override async Task OnSendCoreAsync(Message message, CancellationToken token)
        {
            bool allowOutputBatching;
            ArraySegment<byte> messageData;
            allowOutputBatching = message.Properties.AllowOutputBatching;
            messageData = EncodeMessage(message);
            await Connection.Output.WriteAsync(messageData, token);
            await Connection.Output.FlushAsync();
        }

        protected override async Task CloseOutputAsync(CancellationToken token)
        {
            await Connection.Output.WriteAsync(SessionEncoder.EndBytes, token);
            await Connection.Output.FlushAsync();
        }

        protected override ArraySegment<byte> EncodeMessage(Message message)
        {
            ArraySegment<byte> messageData = MessageEncoder.WriteMessage(message,
                int.MaxValue, BufferManager, SessionEncoder.MaxMessageFrameSize);

            messageData = SessionEncoder.EncodeMessageFrame(messageData);

            return messageData;
        }

        class FramingConnectionDuplexSession : ConnectionDuplexSession
        {

            FramingConnectionDuplexSession(FramingDuplexSessionChannel channel)
                : base(channel)
            {
            }

            public static FramingConnectionDuplexSession CreateSession(FramingDuplexSessionChannel channel,
                StreamUpgradeAcceptor upgradeAcceptor)
            {
                StreamSecurityUpgradeAcceptor security = upgradeAcceptor as StreamSecurityUpgradeAcceptor;
                if (security == null)
                {
                    return new FramingConnectionDuplexSession(channel);
                }
                else
                {
                    return new SecureConnectionDuplexSession(channel);
                }
            }

            class SecureConnectionDuplexSession : FramingConnectionDuplexSession, ISecuritySession
            {
                EndpointIdentity remoteIdentity;

                public SecureConnectionDuplexSession(FramingDuplexSessionChannel channel)
                    : base(channel)
                {
                    // empty
                }

                EndpointIdentity ISecuritySession.RemoteIdentity
                {
                    get
                    {
                        if (remoteIdentity == null)
                        {
                            SecurityMessageProperty security = Channel.RemoteSecurity;
                            if (security != null && security.ServiceSecurityContext != null &&
                                security.ServiceSecurityContext.IdentityClaim != null &&
                                security.ServiceSecurityContext.PrimaryIdentity != null)
                            {
                                remoteIdentity = EndpointIdentity.CreateIdentity(
                                    security.ServiceSecurityContext.IdentityClaim);
                            }
                        }

                        return remoteIdentity;
                    }
                }
            }
        }
    }
}
