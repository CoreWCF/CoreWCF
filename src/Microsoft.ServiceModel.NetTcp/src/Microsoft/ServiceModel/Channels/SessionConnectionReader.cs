using System;
using System.Diagnostics;
using System.Net;
using Microsoft.Runtime;
using System.Runtime.CompilerServices;
using System.Security.Authentication.ExtendedProtection;
using Microsoft.ServiceModel;
using Microsoft.ServiceModel.Description;
using Microsoft.ServiceModel.Diagnostics;
using Microsoft.ServiceModel.Dispatcher;
using Microsoft.ServiceModel.Security;
using System.Threading;
using System.Xml;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel.Channels
{
    delegate void ServerSessionPreambleCallback(ServerSessionPreambleConnectionReader serverSessionPreambleReader);
    delegate void ServerSessionPreambleDemuxCallback(ServerSessionPreambleConnectionReader serverSessionPreambleReader, ConnectionDemuxer connectionDemuxer);
    interface ISessionPreambleHandler
    {
        void HandleServerSessionPreamble(ServerSessionPreambleConnectionReader serverSessionPreambleReader,
            ConnectionDemuxer connectionDemuxer);
    }

    // reads everything we need in order to match a channel (i.e. up to the via) 
    class ServerSessionPreambleConnectionReader : InitialServerConnectionReader
    {
        ServerSessionDecoder decoder;
        byte[] connectionBuffer;
        int offset;
        int size;
        TransportSettingsCallback transportSettingsCallback;
        ServerSessionPreambleCallback callback;
        IConnectionOrientedTransportFactorySettings settings;
        Uri via;
        Action<Uri> viaDelegate;
        TimeoutHelper receiveTimeoutHelper;
        IConnection rawConnection;

        public ServerSessionPreambleConnectionReader(IConnection connection, Action connectionDequeuedCallback,
            long streamPosition, int offset, int size, TransportSettingsCallback transportSettingsCallback,
            ConnectionClosedCallback closedCallback, ServerSessionPreambleCallback callback)
            : base(connection, closedCallback)
        {
            rawConnection = connection;
            decoder = new ServerSessionDecoder(streamPosition, MaxViaSize, MaxContentTypeSize);
            this.offset = offset;
            this.size = size;
            this.transportSettingsCallback = transportSettingsCallback;
            this.callback = callback;
            ConnectionDequeuedCallback = connectionDequeuedCallback;
        }

        public int BufferOffset
        {
            get { return offset; }
        }

        public int BufferSize
        {
            get { return size; }
        }

        public ServerSessionDecoder Decoder
        {
            get { return decoder; }
        }

        public IConnection RawConnection
        {
            get { return rawConnection; }
        }

        public Uri Via
        {
            get { return via; }
        }

        TimeSpan GetRemainingTimeout()
        {
            return receiveTimeoutHelper.RemainingTime();
        }

        async void ContinueReadingAsync()
        {
            bool success = false;
            try
            {
                for (;;)
                {
                    if (size == 0)
                    {
                        offset = 0;
                        size = await Connection.ReadAsync(0, connectionBuffer.Length, GetRemainingTimeout());
                        if (size == 0)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(decoder.CreatePrematureEOFException());
                        }
                    }

                    int bytesDecoded = decoder.Decode(connectionBuffer, offset, size);
                    if (bytesDecoded > 0)
                    {
                        offset += bytesDecoded;
                        size -= bytesDecoded;
                    }

                    if (decoder.CurrentState == ServerSessionDecoder.State.PreUpgradeStart)
                    {
                        via = decoder.Via;
                        if(await Connection.ValidateAsync(via))
                        {
                            settings = transportSettingsCallback(via);

                            if (settings == null)
                            {
                                EndpointNotFoundException e = new EndpointNotFoundException(SR.Format(SR.EndpointNotFound, decoder.Via));
                                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);

                                SendFault(FramingEncodingString.EndpointNotFoundFault);
                                // This goes through the failure path (Abort) even though it doesn't throw.
                                return;
                            }

                            // we have enough information to hand off to a channel. Our job is done
                            callback(this);
                        }

                        break; //exit loop, set success=true;
                    }
                }
                success = true;
            }
            catch (CommunicationException exception)
            {
                DiagnosticUtility.TraceHandledException(exception, TraceEventType.Information);
            }
            catch (TimeoutException exception)
            {
                DiagnosticUtility.TraceHandledException(exception, TraceEventType.Information);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                if (!ExceptionHandlerHelper.HandleTransportExceptionHelper(e))
                {
                    throw;
                }
                // containment -- all exceptions abort the reader, no additional containment action necessary
            }
            finally
            {
                if (!success)
                {
                    Abort();
                }
            }
        }

        public void SendFault(string faultString)
        {
            InitialServerConnectionReader.SendFault(
                Connection, faultString, connectionBuffer, GetRemainingTimeout(),
                TransportDefaults.MaxDrainSize);
            base.Close(GetRemainingTimeout());
        }

        public void StartReading(Action<Uri> viaDelegate, TimeSpan receiveTimeout)
        {
            // TODO: It looks like viaDelegate is only used for port sharing, remove
            this.viaDelegate = viaDelegate;
            receiveTimeoutHelper = new TimeoutHelper(receiveTimeout);
            connectionBuffer = Connection.AsyncReadBuffer;
            ContinueReadingAsync();
        }

        public IDuplexSessionChannel CreateDuplexSessionChannel(ConnectionOrientedTransportChannelListener channelListener, EndpointAddress localAddress, bool exposeConnectionProperty, ConnectionDemuxer connectionDemuxer)
        {
            return new ServerFramingDuplexSessionChannel(channelListener, this, localAddress, exposeConnectionProperty, connectionDemuxer);
        }

        class ServerFramingDuplexSessionChannel : FramingDuplexSessionChannel
        {
            ConnectionOrientedTransportChannelListener channelListener;
            ConnectionDemuxer connectionDemuxer;
            ServerSessionConnectionReader sessionReader;
            ServerSessionDecoder decoder;
            IConnection rawConnection;
            byte[] connectionBuffer;
            int offset;
            int size;
            StreamUpgradeAcceptor upgradeAcceptor;
            IStreamUpgradeChannelBindingProvider channelBindingProvider;

            public ServerFramingDuplexSessionChannel(ConnectionOrientedTransportChannelListener channelListener, ServerSessionPreambleConnectionReader preambleReader,
                EndpointAddress localAddress, bool exposeConnectionProperty, ConnectionDemuxer connectionDemuxer)
                : base(channelListener, localAddress, preambleReader.Via, exposeConnectionProperty)
            {
                this.channelListener = channelListener;
                this.connectionDemuxer = connectionDemuxer;
                Connection = preambleReader.Connection;
                decoder = preambleReader.Decoder;
                connectionBuffer = preambleReader.connectionBuffer;
                offset = preambleReader.BufferOffset;
                size = preambleReader.BufferSize;
                rawConnection = preambleReader.RawConnection;
                StreamUpgradeProvider upgrade = channelListener.Upgrade;
                if (upgrade != null)
                {
                    channelBindingProvider = upgrade.GetProperty<IStreamUpgradeChannelBindingProvider>();
                    upgradeAcceptor = upgrade.CreateUpgradeAcceptor();
                }
            }

            protected override void ReturnConnectionIfNecessary(bool abort, CancellationToken token)
            {
                var timeout = TimeoutHelper.GetOriginalTimeout(token);
                IConnection localConnection = null;
                if (sessionReader != null)
                {
                    lock (ThisLock)
                    {
                        localConnection = sessionReader.GetRawConnection();
                    }
                }

                if (localConnection != null)
                {
                    if (abort)
                    {
                        localConnection.Abort();
                    }
                    else
                    {
                        connectionDemuxer.ReuseConnection(localConnection, timeout);
                    }
                    connectionDemuxer = null;
                }
            }

            public override T GetProperty<T>()
            {
                if (typeof(T) == typeof(IChannelBindingProvider))
                {
                    return (T)(object)channelBindingProvider;
                }

                return base.GetProperty<T>();
            }

            protected override void PrepareMessage(Message message)
            {
                channelListener.RaiseMessageReceived();
                base.PrepareMessage(message);
            }

            // perform security handshake and ACK connection
            protected override async Task OnOpenAsync(CancellationToken token)
            {
                bool success = false;
                try
                {
                    // TODO: Sort out the timeout here
                    var timeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(30));
                    // first validate our content type
                    ValidateContentType(ref timeoutHelper);

                    // next read any potential upgrades and finish consuming the preamble
                    for (;;)
                    {
                        if (size == 0)
                        {
                            offset = 0;
                            size = await Connection.ReadAsync(0, connectionBuffer.Length, timeoutHelper.RemainingTime());
                            if (size == 0)
                            {
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(decoder.CreatePrematureEOFException());
                            }
                        }

                        for (;;)
                        {
                            DecodeBytes();
                            switch (decoder.CurrentState)
                            {
                                case ServerSessionDecoder.State.UpgradeRequest:
                                    ProcessUpgradeRequest(ref timeoutHelper);

                                    // accept upgrade
                                    await Connection.WriteAsync(ServerSessionEncoder.UpgradeResponseBytes, 0, ServerSessionEncoder.UpgradeResponseBytes.Length, true, timeoutHelper.RemainingTime());

                                    IConnection connectionToUpgrade = Connection;
                                    if (size > 0)
                                    {
                                        // TODO: Switch to using PreReadConnection constructor which doesn't take a buffer. This is currently causing an extra buffer allocation.
                                        connectionToUpgrade = new PreReadConnection(connectionToUpgrade, connectionBuffer, offset, size);
                                    }

                                    try
                                    {
                                        Connection = await InitialServerConnectionReader.UpgradeConnectionAsync(connectionToUpgrade, upgradeAcceptor, this);

                                        if (channelBindingProvider != null && channelBindingProvider.IsChannelBindingSupportEnabled)
                                        {
                                            SetChannelBinding(channelBindingProvider.GetChannelBinding(upgradeAcceptor, ChannelBindingKind.Endpoint));
                                        }

                                        connectionBuffer = Connection.AsyncReadBuffer;
                                    }
                                    catch (Exception exception)
                                    {
                                        if (Fx.IsFatal(exception))
                                            throw;

                                        // Audit Authentication Failure
                                        WriteAuditFailure(upgradeAcceptor as StreamSecurityUpgradeAcceptor, exception);
                                        throw;
                                    }
                                    break;

                                case ServerSessionDecoder.State.Start:
                                    SetupSecurityIfNecessary();

                                    // we've finished the preamble. Ack and return.
                                    await Connection.WriteAsync(ServerSessionEncoder.AckResponseBytes, 0,
                                        ServerSessionEncoder.AckResponseBytes.Length, true, timeoutHelper.RemainingTime());
                                    SetupSessionReader();
                                    success = true;
                                    return;
                            }

                            if (size == 0)
                                break;
                        }
                    }
                }
                finally
                {
                    if (!success)
                    {
                        Connection.Abort();
                    }
                }
            }

            void AcceptUpgradedConnection(IConnection upgradedConnection)
            {
                Connection = upgradedConnection;

                if (channelBindingProvider != null && channelBindingProvider.IsChannelBindingSupportEnabled)
                {
                    SetChannelBinding(channelBindingProvider.GetChannelBinding(upgradeAcceptor, ChannelBindingKind.Endpoint));
                }

                connectionBuffer = Connection.AsyncReadBuffer;
            }

            void ValidateContentType(ref TimeoutHelper timeoutHelper)
            {
                MessageEncoder = channelListener.MessageEncoderFactory.CreateSessionEncoder();

                if (!MessageEncoder.IsContentTypeSupported(decoder.ContentType))
                {
                    SendFault(FramingEncodingString.ContentTypeInvalidFault, ref timeoutHelper);
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ProtocolException(SR.Format(
                        SR.ContentTypeMismatch, decoder.ContentType, MessageEncoder.ContentType)));
                }

                // TODO: Support ICompressedMessageEncoder
                //ICompressedMessageEncoder compressedMessageEncoder = this.MessageEncoder as ICompressedMessageEncoder;
                //if (compressedMessageEncoder != null && compressedMessageEncoder.CompressionEnabled)
                //{
                //    compressedMessageEncoder.SetSessionContentType(this.decoder.ContentType);
                //}
            }

            void DecodeBytes()
            {
                int bytesDecoded = decoder.Decode(connectionBuffer, offset, size);
                if (bytesDecoded > 0)
                {
                    offset += bytesDecoded;
                    size -= bytesDecoded;
                }
            }

            void ProcessUpgradeRequest(ref TimeoutHelper timeoutHelper)
            {
                if (upgradeAcceptor == null)
                {
                    SendFault(FramingEncodingString.UpgradeInvalidFault, ref timeoutHelper);
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new ProtocolException(SR.Format(SR.UpgradeRequestToNonupgradableService, decoder.Upgrade)));
                }

                if (!upgradeAcceptor.CanUpgrade(decoder.Upgrade))
                {
                    SendFault(FramingEncodingString.UpgradeInvalidFault, ref timeoutHelper);
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new ProtocolException(SR.Format(SR.UpgradeProtocolNotSupported, decoder.Upgrade)));
                }
            }

            void SendFault(string faultString, ref TimeoutHelper timeoutHelper)
            {
                InitialServerConnectionReader.SendFault(Connection, faultString,
                    connectionBuffer, timeoutHelper.RemainingTime(), TransportDefaults.MaxDrainSize);
            }

            void SetupSecurityIfNecessary()
            {
                StreamSecurityUpgradeAcceptor securityUpgradeAcceptor = upgradeAcceptor as StreamSecurityUpgradeAcceptor;
                if (securityUpgradeAcceptor != null)
                {
                    RemoteSecurity = securityUpgradeAcceptor.GetRemoteSecurity();

                    if (RemoteSecurity == null)
                    {
                        Exception securityFailedException = new ProtocolException(
                            SR.Format(SR.RemoteSecurityNotNegotiatedOnStreamUpgrade, Via));
                        WriteAuditFailure(securityUpgradeAcceptor, securityFailedException);
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(securityFailedException);
                    }
                    else
                    {
                        // Audit Authentication Success
                       // WriteAuditEvent(securityUpgradeAcceptor, AuditLevel.Success, null);
                    }
                }
            }

            void SetupSessionReader()
            {
                sessionReader = new ServerSessionConnectionReader(this);
                base.SetMessageSource(sessionReader);
            }

            #region Transport Security Auditing
            void WriteAuditFailure(StreamSecurityUpgradeAcceptor securityUpgradeAcceptor, Exception exception)
            {
                //try
                //{
                //    WriteAuditEvent(securityUpgradeAcceptor, AuditLevel.Failure, exception);
                //}
                //catch (Exception auditException)
                //{
                //    if (Fx.IsFatal(auditException))
                //    {
                //        throw;
                //    }

                //    DiagnosticUtility.TraceHandledException(auditException, TraceEventType.Error);
                //}
            }

            //void WriteAuditEvent(StreamSecurityUpgradeAcceptor securityUpgradeAcceptor, AuditLevel auditLevel, Exception exception)
            //{
            //    if ((this.channelListener.AuditBehavior.MessageAuthenticationAuditLevel & auditLevel) != auditLevel)
            //    {
            //        return;
            //    }

            //    if (securityUpgradeAcceptor == null)
            //    {
            //        return;
            //    }

            //    String primaryIdentity = String.Empty;
            //    SecurityMessageProperty clientSecurity = securityUpgradeAcceptor.GetRemoteSecurity();
            //    if (clientSecurity != null)
            //    {
            //        primaryIdentity = GetIdentityNameFromContext(clientSecurity);
            //    }

            //    ServiceSecurityAuditBehavior auditBehavior = this.channelListener.AuditBehavior;

            //    if (auditLevel == AuditLevel.Success)
            //    {
            //        SecurityAuditHelper.WriteTransportAuthenticationSuccessEvent(auditBehavior.AuditLogLocation,
            //            auditBehavior.SuppressAuditFailure, null, this.LocalVia, primaryIdentity);
            //    }
            //    else
            //    {
            //        SecurityAuditHelper.WriteTransportAuthenticationFailureEvent(auditBehavior.AuditLogLocation,
            //            auditBehavior.SuppressAuditFailure, null, this.LocalVia, primaryIdentity, exception);
            //    }
            //}

            //[MethodImpl(MethodImplOptions.NoInlining)]
            //static string GetIdentityNameFromContext(SecurityMessageProperty clientSecurity)
            //{
            //    return SecurityUtils.GetIdentityNamesFromContext(
            //        clientSecurity.ServiceSecurityContext.AuthorizationContext);
            //}
            #endregion

            class ServerSessionConnectionReader : SessionConnectionReader
            {
                ServerSessionDecoder decoder;
                int maxBufferSize;
                BufferManager bufferManager;
                MessageEncoder messageEncoder;
                string contentType;
                IConnection rawConnection;

                public ServerSessionConnectionReader(ServerFramingDuplexSessionChannel channel)
                    : base(channel.Connection, channel.rawConnection, channel.offset, channel.size, channel.RemoteSecurity)
                {
                    decoder = channel.decoder;
                    contentType = decoder.ContentType;
                    maxBufferSize = channel.channelListener.MaxBufferSize;
                    bufferManager = channel.channelListener.BufferManager;
                    messageEncoder = channel.MessageEncoder;
                    rawConnection = channel.rawConnection;
                }

                protected override void EnsureDecoderAtEof()
                {
                    if (!(decoder.CurrentState == ServerSessionDecoder.State.End || decoder.CurrentState == ServerSessionDecoder.State.EnvelopeEnd))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(decoder.CreatePrematureEOFException());
                    }
                }

                protected override Message DecodeMessage(byte[] buffer, ref int offset, ref int size, ref bool isAtEof, TimeSpan timeout)
                {
                    while (!isAtEof && size > 0)
                    {
                        int bytesRead = decoder.Decode(buffer, offset, size);
                        if (bytesRead > 0)
                        {
                            if (EnvelopeBuffer != null)
                            {
                                if (!object.ReferenceEquals(buffer, EnvelopeBuffer))
                                {
                                    System.Buffer.BlockCopy(buffer, offset, EnvelopeBuffer, EnvelopeOffset, bytesRead);
                                }
                                EnvelopeOffset += bytesRead;
                            }

                            offset += bytesRead;
                            size -= bytesRead;
                        }

                        switch (decoder.CurrentState)
                        {
                            case ServerSessionDecoder.State.EnvelopeStart:
                                int envelopeSize = decoder.EnvelopeSize;
                                if (envelopeSize > maxBufferSize)
                                {
                                    base.SendFault(FramingEncodingString.MaxMessageSizeExceededFault, timeout);

                                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                        MaxMessageSizeStream.CreateMaxReceivedMessageSizeExceededException(maxBufferSize));
                                }
                                EnvelopeBuffer = bufferManager.TakeBuffer(envelopeSize);
                                EnvelopeOffset = 0;
                                EnvelopeSize = envelopeSize;
                                break;

                            case ServerSessionDecoder.State.EnvelopeEnd:
                                if (EnvelopeBuffer != null)
                                {
                                    Message message = null;

                                    try
                                    {
                                        message = messageEncoder.ReadMessage(new ArraySegment<byte>(EnvelopeBuffer, 0, EnvelopeSize), bufferManager, contentType);
                                    }
                                    catch (XmlException xmlException)
                                    {
                                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                            new ProtocolException(SR.Format(SR.MessageXmlProtocolError), xmlException));
                                    }

                                    EnvelopeBuffer = null;
                                    return message;
                                }
                                break;

                            case ServerSessionDecoder.State.End:
                                isAtEof = true;
                                break;
                        }
                    }

                    return null;
                }

                protected override void PrepareMessage(Message message)
                {
                    base.PrepareMessage(message);

                    IPEndPoint remoteEndPoint = rawConnection.RemoteIPEndPoint;
                    // pipes will return null
                    if (remoteEndPoint != null)
                    {
                        RemoteEndpointMessageProperty remoteEndpointProperty = new RemoteEndpointMessageProperty(remoteEndPoint);
                        message.Properties.Add(RemoteEndpointMessageProperty.Name, remoteEndpointProperty);
                    }
                }
            }
        }
    }

    abstract class SessionConnectionReader : IMessageSource
    {
        bool isAtEOF;
        bool usingAsyncReadBuffer;
        IConnection connection;
        byte[] buffer;
        int offset;
        int size;
        byte[] envelopeBuffer;
        int envelopeOffset;
        int envelopeSize;
        bool readIntoEnvelopeBuffer;
        Message pendingMessage;
        Exception pendingException;
        SecurityMessageProperty security;
        // Raw connection that we will revert to after end handshake
        IConnection rawConnection;

        protected SessionConnectionReader(IConnection connection, IConnection rawConnection,
            int offset, int size, SecurityMessageProperty security)
        {
            this.offset = offset;
            this.size = size;
            if (size > 0)
            {
                buffer = connection.AsyncReadBuffer;
            }
            this.connection = connection;
            this.rawConnection = rawConnection;
            this.security = security;
        }

        Message DecodeMessage(TimeSpan timeout)
        {
            if (!readIntoEnvelopeBuffer)
            {
                return DecodeMessage(buffer, ref offset, ref size, ref isAtEOF, timeout);
            }
            else
            {
                // decode from the envelope buffer
                int dummyOffset = envelopeOffset;
                return DecodeMessage(envelopeBuffer, ref dummyOffset, ref size, ref isAtEOF, timeout);
            }
        }

        protected abstract Message DecodeMessage(byte[] buffer, ref int offset, ref int size, ref bool isAtEof, TimeSpan timeout);

        protected byte[] EnvelopeBuffer
        {
            get { return envelopeBuffer; }
            set { envelopeBuffer = value; }
        }

        protected int EnvelopeOffset
        {
            get { return envelopeOffset; }
            set { envelopeOffset = value; }
        }

        protected int EnvelopeSize
        {
            get { return envelopeSize; }
            set { envelopeSize = value; }
        }

        public IConnection GetRawConnection()
        {
            IConnection result = null;
            if (rawConnection != null)
            {
                result = rawConnection;
                rawConnection = null;
                if (size > 0)
                {
                    PreReadConnection preReadConnection = result as PreReadConnection;
                    if (preReadConnection != null) // make sure we don't keep wrapping
                    {
                        preReadConnection.AddPreReadData(buffer, offset, size);
                    }
                    else
                    {
                        result = new PreReadConnection(result, buffer, offset, size);
                    }
                }
            }

            return result;
        }

        public async Task<Message> ReceiveAsync(CancellationToken token)
        {
            Message message = GetPendingMessage();

            if (message != null)
            {
                return message;
            }

            TimeoutHelper timeoutHelper = new TimeoutHelper(TimeoutHelper.GetOriginalTimeout(token));
            while (true)
            {
                if (isAtEOF)
                {
                    return null;
                }

                if (size > 0)
                {
                    message = DecodeMessage(timeoutHelper.RemainingTime());

                    if (message != null)
                    {
                        PrepareMessage(message);
                        return message;
                    }
                    else if (isAtEOF) // could have read the END record under DecodeMessage
                    {
                        return null;
                    }
                }

                if (size != 0)
                {
                    throw Fx.AssertAndThrow("Receive: DecodeMessage() should consume the outstanding buffer or return a message.");
                }

                if (!usingAsyncReadBuffer)
                {
                    buffer = connection.AsyncReadBuffer;
                    usingAsyncReadBuffer = true;
                }

                int bytesRead = await connection.ReadAsync(0, buffer.Length, timeoutHelper.RemainingTime());

                HandleReadComplete(bytesRead, false);
            }
        }

        Message GetPendingMessage()
        {
            if (pendingException != null)
            {
                Exception exception = pendingException;
                pendingException = null;
                throw TraceUtility.ThrowHelperError(exception, pendingMessage);
            }

            if (pendingMessage != null)
            {
                Message message = pendingMessage;
                pendingMessage = null;
                return message;
            }

            return null;
        }

        public async Task<bool> WaitForMessageAsync(CancellationToken token)
        {
            try
            {
                Message message = await ReceiveAsync(token);
                pendingMessage = message;
                return true;
            }
            catch (TimeoutException e)
            {
                pendingException = e;
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                return false;
            }
        }

        protected abstract void EnsureDecoderAtEof();

        void HandleReadComplete(int bytesRead, bool readIntoEnvelopeBuffer)
        {
            this.readIntoEnvelopeBuffer = readIntoEnvelopeBuffer;

            if (bytesRead == 0)
            {
                EnsureDecoderAtEof();
                isAtEOF = true;
            }
            else
            {
                offset = 0;
                size = bytesRead;
            }
        }

        protected virtual void PrepareMessage(Message message)
        {
            if (security != null)
            {
                message.Properties.Security = (SecurityMessageProperty)security.CreateCopy();
            }
        }

        protected void SendFault(string faultString, TimeSpan timeout)
        {
            byte[] drainBuffer = new byte[128];
            InitialServerConnectionReader.SendFault(
                connection, faultString, drainBuffer, timeout,
                TransportDefaults.MaxDrainSize);
        }
    }
}
