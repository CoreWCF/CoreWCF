using CoreWCF.Runtime;
using CoreWCF;
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using CoreWCF.Dispatcher;
using CoreWCF.Security;
using System;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Authentication.ExtendedProtection;
using System.Threading;
using System.Xml;
using System.Threading.Tasks;
using System.Diagnostics;

namespace CoreWCF.Channels
{
    delegate void ServerSingletonPreambleCallback(ServerSingletonPreambleConnectionReader serverSingletonPreambleReader);
    delegate ISingletonChannelListener SingletonPreambleDemuxCallback(ServerSingletonPreambleConnectionReader serverSingletonPreambleReader);
    interface ISingletonChannelListener
    {
        TimeSpan ReceiveTimeout { get; }
        // TODO: This is a synchronous method to dispatch a request. This might cause a method to synchronously block
        void ReceiveRequest(RequestContext requestContext, Action callback, bool canDispatchOnThisThread);
    }

    class ServerSingletonPreambleConnectionReader : InitialServerConnectionReader
    {
        ServerSingletonDecoder decoder;
        ServerSingletonPreambleCallback callback;
        IConnectionOrientedTransportFactorySettings transportSettings;
        TransportSettingsCallback transportSettingsCallback;
        SecurityMessageProperty security;
        Uri via;
        IConnection rawConnection;
        byte[] connectionBuffer;
        bool isReadPending;
        int offset;
        int size;
        TimeoutHelper receiveTimeoutHelper;
        Action<Uri> viaDelegate;
        ChannelBinding channelBindingToken;

        public ServerSingletonPreambleConnectionReader(IConnection connection, Action connectionDequeuedCallback,
            long streamPosition, int offset, int size, TransportSettingsCallback transportSettingsCallback,
            ConnectionClosedCallback closedCallback, ServerSingletonPreambleCallback callback)
            : base(connection, closedCallback)
        {
            decoder = new ServerSingletonDecoder(streamPosition, MaxViaSize, MaxContentTypeSize);
            this.offset = offset;
            this.size = size;
            this.callback = callback;
            this.transportSettingsCallback = transportSettingsCallback;
            rawConnection = connection;
            ConnectionDequeuedCallback = connectionDequeuedCallback;

        }

        public ChannelBinding ChannelBinding
        {
            get
            {
                return channelBindingToken;
            }
        }

        public int BufferOffset
        {
            get { return offset; }
        }

        public int BufferSize
        {
            get { return size; }
        }

        public ServerSingletonDecoder Decoder
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

        public IConnectionOrientedTransportFactorySettings TransportSettings
        {
            get { return transportSettings; }
        }

        public SecurityMessageProperty Security
        {
            get { return security; }
        }

        TimeSpan GetRemainingTimeout()
        {
            return receiveTimeoutHelper.RemainingTime();
        }

        async Task ReadAndDispatchAsync()
        {
            bool success = false;
            try
            {
                while ((size > 0 || !isReadPending) && !IsClosed)
                {
                    if (size == 0)
                    {
                        isReadPending = true;
                        size = await Connection.ReadAsync(0, connectionBuffer.Length, GetRemainingTimeout());
                        offset = 0;
                        isReadPending = false;
                        if (size == 0)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(decoder.CreatePrematureEOFException());
                        }
                    }

                    int bytesRead = decoder.Decode(connectionBuffer, offset, size);
                    if (bytesRead > 0)
                    {
                        offset += bytesRead;
                        size -= bytesRead;
                    }

                    if (decoder.CurrentState == ServerSingletonDecoder.State.PreUpgradeStart)
                    {
                        via = decoder.Via;
                        var validated = await Connection.ValidateAsync(via);

                        if (!ContinuePostValidationProcessing())
                        {
                            // This goes through the failure path (Abort) even though it doesn't throw.
                            return;
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

                // containment -- we abort ourselves for any error, no extra containment needed
            }
            finally
            {
                if (!success)
                {
                    Abort();
                }
            }
        }

        //returns false if the connection should be aborted
        bool ContinuePostValidationProcessing()
        {
            if (viaDelegate != null)
            {
                try
                {
                    viaDelegate(via);
                }
                catch (ServiceActivationException e)
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                    // return fault and close connection
                    SendFault(FramingEncodingString.ServiceActivationFailedFault);
                    return true;
                }
            }


            transportSettings = transportSettingsCallback(via);

            if (transportSettings == null)
            {
                EndpointNotFoundException e = new EndpointNotFoundException(SR.Format(SR.EndpointNotFound, decoder.Via));
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                // return fault and close connection
                SendFault(FramingEncodingString.EndpointNotFoundFault);
                return false;
            }

            // we have enough information to hand off to a channel. Our job is done
            callback(this);
            return true;
        }

        public void SendFault(string faultString)
        {
            SendFault(faultString, ref receiveTimeoutHelper);
        }

        void SendFault(string faultString, ref TimeoutHelper timeoutHelper)
        {
            InitialServerConnectionReader.SendFault(Connection, faultString,
                connectionBuffer, timeoutHelper.RemainingTime(), TransportDefaults.MaxDrainSize);
        }

        public async Task<IConnection> CompletePreambleAsync(TimeSpan timeout)
        {
            var timeoutHelper = new TimeoutHelper(timeout);
            var parent = this;
            if (!transportSettings.MessageEncoderFactory.Encoder.IsContentTypeSupported(Decoder.ContentType))
            {
                SendFault(FramingEncodingString.ContentTypeInvalidFault, ref timeoutHelper);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ProtocolException(SR.Format(
                    SR.ContentTypeMismatch, Decoder.ContentType, parent.transportSettings.MessageEncoderFactory.Encoder.ContentType)));
            }

            IStreamUpgradeChannelBindingProvider channelBindingProvider = null;
            StreamUpgradeAcceptor upgradeAcceptor = null;
            if (transportSettings.Upgrade != null)
            {
                channelBindingProvider = transportSettings.Upgrade.GetProperty<IStreamUpgradeChannelBindingProvider>();
                upgradeAcceptor = transportSettings.Upgrade.CreateUpgradeAcceptor();
            }

            var currentConnection = Connection;
            UpgradeState upgradeState = UpgradeState.None;

            while (true)
            {
                if (size == 0 && CanReadAndDecode(upgradeState))
                {
                    size = await currentConnection.ReadAsync(0, connectionBuffer.Length, timeoutHelper.RemainingTime());
                    if (size == 0)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(Decoder.CreatePrematureEOFException());
                    }
                }

                while(true)
                {
                    if (CanReadAndDecode(upgradeState))
                    {
                        int bytesRead = Decoder.Decode(connectionBuffer, offset, size);
                        if (bytesRead > 0)
                        {
                            offset += bytesRead;
                            size -= bytesRead;
                        }
                    }

                    switch (Decoder.CurrentState)
                    {
                        case ServerSingletonDecoder.State.UpgradeRequest:
                            switch (upgradeState)
                            {
                                case UpgradeState.None:
                                    //change the state so that we don't read/decode until it is safe
                                    ChangeUpgradeState(ref upgradeState, UpgradeState.VerifyingUpgradeRequest);
                                    break;
                                case UpgradeState.VerifyingUpgradeRequest:
                                    if (upgradeAcceptor == null)
                                    {
                                        SendFault(FramingEncodingString.UpgradeInvalidFault, ref timeoutHelper);
                                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                            new ProtocolException(SR.Format(SR.UpgradeRequestToNonupgradableService, Decoder.Upgrade)));
                                    }

                                    if (!upgradeAcceptor.CanUpgrade(Decoder.Upgrade))
                                    {
                                        SendFault(FramingEncodingString.UpgradeInvalidFault, ref timeoutHelper);
                                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ProtocolException(SR.Format(SR.UpgradeProtocolNotSupported, Decoder.Upgrade)));
                                    }

                                    ChangeUpgradeState(ref upgradeState, UpgradeState.WritingUpgradeAck);
                                    // accept upgrade
                                    await currentConnection.WriteAsync(ServerSingletonEncoder.UpgradeResponseBytes, 0, ServerSingletonEncoder.UpgradeResponseBytes.Length,
                                        true, timeoutHelper.RemainingTime());
                                    ChangeUpgradeState(ref upgradeState, UpgradeState.UpgradeAckSent);
                                    break;
                                case UpgradeState.UpgradeAckSent:
                                    IConnection connectionToUpgrade = currentConnection;
                                    if (size > 0)
                                    {
                                        connectionToUpgrade = new PreReadConnection(connectionToUpgrade, connectionBuffer, offset, size);
                                    }
                                    ChangeUpgradeState(ref upgradeState, UpgradeState.BeginUpgrade);
                                    break;
                                case UpgradeState.BeginUpgrade:
                                    try
                                    {
                                        currentConnection = await InitialServerConnectionReader.UpgradeConnectionAsync(currentConnection, upgradeAcceptor, transportSettings);
                                        connectionBuffer = currentConnection.AsyncReadBuffer;
                                        if (channelBindingProvider != null &&
                                            channelBindingProvider.IsChannelBindingSupportEnabled &&
                                            channelBindingToken == null)//first one wins in the case of multiple upgrades.
                                        {
                                            channelBindingToken = channelBindingProvider.GetChannelBinding(upgradeAcceptor, ChannelBindingKind.Endpoint);
                                        }
                                        ChangeUpgradeState(ref upgradeState, UpgradeState.EndUpgrade);
                                        ChangeUpgradeState(ref upgradeState, UpgradeState.UpgradeComplete);
                                    }
                                    catch (Exception exception)
                                    {
                                        if (Fx.IsFatal(exception))
                                            throw;

                                        WriteAuditFailure(upgradeAcceptor as StreamSecurityUpgradeAcceptor, exception);
                                        throw;
                                    }
                                    break;
                                case UpgradeState.UpgradeComplete:
                                    //Client is doing more than one upgrade, reset the state
                                    ChangeUpgradeState(ref upgradeState, UpgradeState.VerifyingUpgradeRequest);
                                    break;
                            }
                            break;
                        case ServerSingletonDecoder.State.Start:
                            SetupSecurityIfNecessary(upgradeAcceptor);

                            if (upgradeState == UpgradeState.UpgradeComplete //We have done at least one upgrade, but we are now done.
                                || upgradeState == UpgradeState.None)//no upgrade, just send the preample end bytes
                            {
                                ChangeUpgradeState(ref upgradeState, UpgradeState.WritingPreambleEnd);
                                // we've finished the preamble. Ack and return.
                                await currentConnection.WriteAsync(ServerSessionEncoder.AckResponseBytes, 0, ServerSessionEncoder.AckResponseBytes.Length,
                                            true, timeoutHelper.RemainingTime());
                                //terminal state
                                ChangeUpgradeState(ref upgradeState, UpgradeState.PreambleEndSent);
                            }

                            //we are done, this.currentConnection is the upgraded connection                                
                            return currentConnection;
                    }

                    if (size == 0)
                    {
                        break;
                    }
                }
            }
        }

        void ChangeUpgradeState(ref UpgradeState upgradeState, UpgradeState newState)
        {
            switch (newState)
            {
                case UpgradeState.None:
                    throw Fx.AssertAndThrow("Invalid State Transition: currentState=" + upgradeState + ", newState=" + newState);
                case UpgradeState.VerifyingUpgradeRequest:
                    if (upgradeState != UpgradeState.None //starting first upgrade
                        && upgradeState != UpgradeState.UpgradeComplete)//completing one upgrade and starting another
                    {
                        throw Fx.AssertAndThrow("Invalid State Transition: currentState=" + upgradeState + ", newState=" + newState);
                    }
                    break;
                case UpgradeState.WritingUpgradeAck:
                    if (upgradeState != UpgradeState.VerifyingUpgradeRequest)
                    {
                        throw Fx.AssertAndThrow("Invalid State Transition: currentState=" + upgradeState + ", newState=" + newState);
                    }
                    break;
                case UpgradeState.UpgradeAckSent:
                    if (upgradeState != UpgradeState.WritingUpgradeAck)
                    {
                        throw Fx.AssertAndThrow("Invalid State Transition: currentState=" + upgradeState + ", newState=" + newState);
                    }
                    break;
                case UpgradeState.BeginUpgrade:
                    if (upgradeState != UpgradeState.UpgradeAckSent)
                    {
                        throw Fx.AssertAndThrow("Invalid State Transition: currentState=" + upgradeState + ", newState=" + newState);
                    }
                    break;
                case UpgradeState.EndUpgrade:
                    if (upgradeState != UpgradeState.BeginUpgrade)
                    {
                        throw Fx.AssertAndThrow("Invalid State Transition: currentState=" + upgradeState + ", newState=" + newState);
                    }
                    break;
                case UpgradeState.UpgradeComplete:
                    if (upgradeState != UpgradeState.EndUpgrade)
                    {
                        throw Fx.AssertAndThrow("Invalid State Transition: currentState=" + upgradeState + ", newState=" + newState);
                    }
                    break;
                case UpgradeState.WritingPreambleEnd:
                    if (upgradeState != UpgradeState.None //no upgrade being used
                        && upgradeState != UpgradeState.UpgradeComplete)//upgrades are now complete, end the preamble handshake.
                    {
                        throw Fx.AssertAndThrow("Invalid State Transition: currentState=" + upgradeState + ", newState=" + newState);
                    }
                    break;
                case UpgradeState.PreambleEndSent:
                    if (upgradeState != UpgradeState.WritingPreambleEnd)
                    {
                        throw Fx.AssertAndThrow("Invalid State Transition: currentState=" + upgradeState + ", newState=" + newState);
                    }
                    break;
                default:
                    throw Fx.AssertAndThrow("Unexpected Upgrade State: " + newState);
            }

            upgradeState = newState;
        }

        private static bool CanReadAndDecode(UpgradeState upgradeState)
        {
                    //ok to read/decode before we start the upgrade
                    //and between UpgradeComplete/WritingPreambleAck
                    return upgradeState == UpgradeState.None
                        || upgradeState == UpgradeState.UpgradeComplete;
        }

        enum UpgradeState
        {
            None,
            VerifyingUpgradeRequest,
            WritingUpgradeAck,
            UpgradeAckSent,
            BeginUpgrade,
            EndUpgrade,
            UpgradeComplete,
            WritingPreambleEnd,
            PreambleEndSent,
        }

        void SetupSecurityIfNecessary(StreamUpgradeAcceptor upgradeAcceptor)
        {
            StreamSecurityUpgradeAcceptor securityUpgradeAcceptor = upgradeAcceptor as StreamSecurityUpgradeAcceptor;
            if (securityUpgradeAcceptor != null)
            {
                security = securityUpgradeAcceptor.GetRemoteSecurity();
                if (security == null)
                {
                    Exception securityFailedException = new ProtocolException(
                    SR.Format(SR.RemoteSecurityNotNegotiatedOnStreamUpgrade, Via));
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(securityFailedException);
                }
            }
        }

        #region Transport Security Auditing
        void WriteAuditFailure(StreamSecurityUpgradeAcceptor securityUpgradeAcceptor, Exception exception)
        {
        }
        #endregion

        public Task StartReadingAsync(Action<Uri> viaDelegate, TimeSpan timeout)
        {
            this.viaDelegate = viaDelegate;
            receiveTimeoutHelper = new TimeoutHelper(timeout);
            connectionBuffer = Connection.AsyncReadBuffer;
            return ReadAndDispatchAsync();
        }
    }

    abstract class SingletonConnectionReader
    {
        IConnection connection;
        bool doneReceiving;
        bool doneSending;
        bool isAtEof;
        bool isClosed;
        SecurityMessageProperty security;
        object thisLock = new object();
        int offset;
        int size;
        IConnectionOrientedTransportFactorySettings transportSettings;
        Uri via;
        Stream inputStream;

        protected SingletonConnectionReader(IConnection connection, int offset, int size, SecurityMessageProperty security,
            IConnectionOrientedTransportFactorySettings transportSettings, Uri via)
        {
            this.connection = connection;
            this.offset = offset;
            this.size = size;
            this.security = security;
            this.transportSettings = transportSettings;
            this.via = via;
        }

        protected IConnection Connection
        {
            get
            {
                return connection;
            }
        }

        protected object ThisLock
        {
            get
            {
                return thisLock;
            }
        }

        protected virtual string ContentType
        {
            get { return null; }
        }

        protected abstract long StreamPosition { get; }

        public void Abort()
        {
            connection.Abort();
        }

        public Task DoneReceivingAsync(bool atEof)
        {
            return DoneReceivingAsync(atEof, new TimeoutHelper(transportSettings.CloseTimeout).GetCancellationToken());
        }

        Task DoneReceivingAsync(bool atEof, CancellationToken token)
        {
            if (!doneReceiving)
            {
                isAtEof = atEof;
                doneReceiving = true;

                if (doneSending)
                {
                    return CloseAsync(token);
                }
            }

            return Task.CompletedTask;
        }

        public async Task CloseAsync(CancellationToken token)
        {
            lock (ThisLock)
            {
                if (isClosed)
                {
                    return;
                }

                isClosed = true;
            }

            bool success = false;
            try
            {
                // first drain our stream if necessary
                if (inputStream != null)
                {
                    byte[] dummy = Fx.AllocateByteArray(transportSettings.ConnectionBufferSize);
                    while (!isAtEof)
                    {
                        int bytesRead = await inputStream.ReadAsync(dummy, 0, dummy.Length, token);
                        if (bytesRead == 0)
                        {
                            isAtEof = true;
                        }
                    }
                }
                await OnCloseAsync(token);
                success = true;
            }
            finally
            {
                if (!success)
                {
                    Abort();
                }
            }
        }

        protected abstract Task OnCloseAsync(CancellationToken token);

        public Task DoneSendingAsync(CancellationToken token)
        {
            doneSending = true;
            if (doneReceiving)
            {
                return CloseAsync(token);
            }

            return Task.CompletedTask;
        }

        protected abstract bool DecodeBytes(byte[] buffer, ref int offset, ref int size, ref bool isAtEof);

        protected virtual void PrepareMessage(Message message)
        {
            message.Properties.Via = via;
            message.Properties.Security = (security != null) ? (SecurityMessageProperty)security.CreateCopy() : null;
        }

        public async Task<RequestContext> ReceiveRequestAsync(CancellationToken token)
        {
            Message requestMessage = await ReceiveAsync(token);
            return new StreamedFramingRequestContext(this, requestMessage);
        }

        public async Task<Message> ReceiveAsync(CancellationToken token)
        {
            //byte[] buffer = Fx.AllocateByteArray(connection.AsyncReadBufferSize);

            //if (size > 0)
            //{
            //    Buffer.BlockCopy(connection.AsyncReadBuffer, offset, buffer, offset, size);
            //}
            var timeoutHelper = new TimeoutHelper(TimeoutHelper.GetOriginalTimeout(token));
            for (;;)
            {
                if (DecodeBytes(connection.AsyncReadBuffer, ref offset, ref size, ref isAtEof))
                {
                    break;
                }

                if (isAtEof)
                {
                    await DoneReceivingAsync(true, token);
                    return null;
                }

                if (size == 0)
                {
                    offset = 0;
                    size = await connection.ReadAsync(0, connection.AsyncReadBufferSize, timeoutHelper.RemainingTime());
                    if (size == 0)
                    {
                        await DoneReceivingAsync(true, token);
                        return null;
                    }
                }
            }

            // we're ready to read a message
            IConnection singletonConnection = connection;
            if (size > 0)
            {
                singletonConnection = new PreReadConnection(singletonConnection, offset, size);
            }

            Stream connectionStream = new SingletonInputConnectionStream(this, singletonConnection, transportSettings);
            inputStream = new MaxMessageSizeStream(connectionStream, transportSettings.MaxReceivedMessageSize);
            Message message = null;
            try
            {
                message = transportSettings.MessageEncoderFactory.Encoder.ReadMessage(
                    inputStream, transportSettings.MaxBufferSize, ContentType);
            }
            catch (XmlException xmlException)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ProtocolException(SR.Format(SR.MessageXmlProtocolError), xmlException));
            }

            PrepareMessage(message);

            return message;
        }

        class StreamedFramingRequestContext : RequestContextBase
        {
            IConnection connection;
            SingletonConnectionReader parent;
            IConnectionOrientedTransportFactorySettings settings;
            TimeoutHelper timeoutHelper;

            public StreamedFramingRequestContext(SingletonConnectionReader parent, Message requestMessage)
                : base(requestMessage, parent.transportSettings.CloseTimeout, parent.transportSettings.SendTimeout)
            {
                this.parent = parent;
                connection = parent.connection;
                settings = parent.transportSettings;
            }

            protected override void OnAbort()
            {
                parent.Abort();
            }

            protected override Task OnCloseAsync(CancellationToken token)
            {
                return parent.CloseAsync(token);
            }

            protected override async Task OnReplyAsync(Message message, CancellationToken token)
            {
                // TODO: Support ICompressedMessageEncoder
                //ICompressedMessageEncoder compressedMessageEncoder = this.settings.MessageEncoderFactory.Encoder as ICompressedMessageEncoder;
                //if (compressedMessageEncoder != null && compressedMessageEncoder.CompressionEnabled)
                //{
                //    compressedMessageEncoder.AddCompressedMessageProperties(message, this.parent.ContentType);
                //}

                timeoutHelper = new TimeoutHelper(TimeoutHelper.GetOriginalTimeout(token));
                await StreamingConnectionHelper.WriteMessageAsync(message, connection, false, settings, token);
                await parent.DoneSendingAsync(token);
            }
        }

        // ensures that the reader is notified at end-of-stream, and takes care of the framing chunk headers
        class SingletonInputConnectionStream : ConnectionStream
        {
            SingletonMessageDecoder decoder;
            SingletonConnectionReader reader;
            bool atEof;
            byte[] chunkBuffer; // used for when we have overflow
            int chunkBufferOffset;
            int chunkBufferSize;
            int chunkBytesRemaining;

            public SingletonInputConnectionStream(SingletonConnectionReader reader, IConnection connection,
                IDefaultCommunicationTimeouts defaultTimeouts)
                : base(connection, defaultTimeouts)
            {
                this.reader = reader;
                decoder = new SingletonMessageDecoder(reader.StreamPosition);
                chunkBytesRemaining = 0;
                chunkBuffer = new byte[IntEncoder.MaxEncodedSize];
            }

            void AbortReader()
            {
                reader.Abort();
            }

            public override void Close()
            {
                reader.DoneReceivingAsync(atEof).GetAwaiter().GetResult();
            }

            // run chunk data through the decoder
            void DecodeData(byte[] buffer, int offset, int size)
            {
                while (size > 0)
                {
                    int bytesRead = decoder.Decode(buffer, offset, size);
                    offset += bytesRead;
                    size -= bytesRead;
                    Fx.Assert(decoder.CurrentState == SingletonMessageDecoder.State.ReadingEnvelopeBytes || decoder.CurrentState == SingletonMessageDecoder.State.ChunkEnd, "");
                }
            }

            // run the current data through the decoder to get valid message bytes
            void DecodeSize(byte[] buffer, ref int offset, ref int size)
            {
                while (size > 0)
                {
                    int bytesRead = decoder.Decode(buffer, offset, size);

                    if (bytesRead > 0)
                    {
                        offset += bytesRead;
                        size -= bytesRead;
                    }

                    switch (decoder.CurrentState)
                    {
                        case SingletonMessageDecoder.State.ChunkStart:
                            chunkBytesRemaining = decoder.ChunkSize;

                            // if we have overflow and we're not decoding out of our buffer, copy over
                            if (size > 0 && !object.ReferenceEquals(buffer, chunkBuffer))
                            {
                                Fx.Assert(size <= chunkBuffer.Length, "");
                                Buffer.BlockCopy(buffer, offset, chunkBuffer, 0, size);
                                chunkBufferOffset = 0;
                                chunkBufferSize = size;
                            }
                            return;

                        case SingletonMessageDecoder.State.End:
                            ProcessEof();
                            return;
                    }
                }
            }

            async Task<int> ReadCoreAsync(byte[] buffer, int offset, int count, CancellationToken token)
            {
                int bytesRead = -1;
                try
                {
                    bytesRead = await base.ReadAsync(buffer, offset, count, token);
                    if (bytesRead == 0)
                    {
                        ProcessEof();
                    }
                }
                finally
                {
                    if (bytesRead == -1)  // there was an exception
                    {
                        AbortReader();
                    }
                }

                return bytesRead;
            }

            int ReadCore(byte[] buffer, int offset, int count)
            {
                int bytesRead = -1;
                try
                {
                    bytesRead = base.Read(buffer, offset, count);
                    if (bytesRead == 0)
                    {
                        ProcessEof();
                    }
                }
                finally
                {
                    if (bytesRead == -1)  // there was an exception
                    {
                        AbortReader();
                    }
                }

                return bytesRead;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            {
                int result = 0;
                while (true)
                {
                    if (count == 0)
                    {
                        return result;
                    }

                    if (atEof)
                    {
                        return result;
                    }

                    // first deal with any residual carryover
                    if (chunkBufferSize > 0)
                    {
                        int bytesToCopy = Math.Min(chunkBytesRemaining,
                            Math.Min(chunkBufferSize, count));

                        Buffer.BlockCopy(chunkBuffer, chunkBufferOffset, buffer, offset, bytesToCopy);
                        // keep decoder up to date
                        DecodeData(chunkBuffer, chunkBufferOffset, bytesToCopy);

                        chunkBufferOffset += bytesToCopy;
                        chunkBufferSize -= bytesToCopy;
                        chunkBytesRemaining -= bytesToCopy;
                        if (chunkBytesRemaining == 0 && chunkBufferSize > 0)
                        {
                            DecodeSize(chunkBuffer, ref chunkBufferOffset, ref chunkBufferSize);
                        }

                        result += bytesToCopy;
                        offset += bytesToCopy;
                        count -= bytesToCopy;
                    }
                    else if (chunkBytesRemaining > 0)
                    {
                        // We're in the middle of a chunk. Try and include the next chunk size as well

                        int bytesToRead = count;
                        if (int.MaxValue - chunkBytesRemaining >= IntEncoder.MaxEncodedSize)
                        {
                            bytesToRead = Math.Min(count, chunkBytesRemaining + IntEncoder.MaxEncodedSize);
                        }

                        int bytesRead = ReadCore(buffer, offset, bytesToRead);

                        // keep decoder up to date
                        DecodeData(buffer, offset, Math.Min(bytesRead, chunkBytesRemaining));

                        if (bytesRead > chunkBytesRemaining)
                        {
                            result += chunkBytesRemaining;
                            int overflowCount = bytesRead - chunkBytesRemaining;
                            int overflowOffset = offset + chunkBytesRemaining;
                            chunkBytesRemaining = 0;
                            // read at least part of the next chunk, and put any overflow in this.chunkBuffer
                            DecodeSize(buffer, ref overflowOffset, ref overflowCount);
                        }
                        else
                        {
                            result += bytesRead;
                            chunkBytesRemaining -= bytesRead;
                        }

                        return result;
                    }
                    else
                    {
                        // Final case: we have a new chunk. Read the size, and loop around again
                        if (count < IntEncoder.MaxEncodedSize)
                        {
                            // we don't have space for MaxEncodedSize, so it's worth the copy cost to read into a temp buffer
                            chunkBufferOffset = 0;
                            chunkBufferSize = await ReadCoreAsync(chunkBuffer, 0, chunkBuffer.Length, token);
                            DecodeSize(chunkBuffer, ref chunkBufferOffset, ref chunkBufferSize);
                        }
                        else
                        {
                            int bytesRead = ReadCore(buffer, offset, IntEncoder.MaxEncodedSize);
                            int sizeOffset = offset;
                            DecodeSize(buffer, ref sizeOffset, ref bytesRead);
                        }
                    }
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int result = 0;
                while (true)
                {
                    if (count == 0)
                    {
                        return result;
                    }

                    if (atEof)
                    {
                        return result;
                    }

                    // first deal with any residual carryover
                    if (chunkBufferSize > 0)
                    {
                        int bytesToCopy = Math.Min(chunkBytesRemaining,
                            Math.Min(chunkBufferSize, count));

                        Buffer.BlockCopy(chunkBuffer, chunkBufferOffset, buffer, offset, bytesToCopy);
                        // keep decoder up to date
                        DecodeData(chunkBuffer, chunkBufferOffset, bytesToCopy);

                        chunkBufferOffset += bytesToCopy;
                        chunkBufferSize -= bytesToCopy;
                        chunkBytesRemaining -= bytesToCopy;
                        if (chunkBytesRemaining == 0 && chunkBufferSize > 0)
                        {
                            DecodeSize(chunkBuffer, ref chunkBufferOffset, ref chunkBufferSize);
                        }

                        result += bytesToCopy;
                        offset += bytesToCopy;
                        count -= bytesToCopy;
                    }
                    else if (chunkBytesRemaining > 0)
                    {
                        // We're in the middle of a chunk. Try and include the next chunk size as well

                        int bytesToRead = count;
                        if (int.MaxValue - chunkBytesRemaining >= IntEncoder.MaxEncodedSize)
                        {
                            bytesToRead = Math.Min(count, chunkBytesRemaining + IntEncoder.MaxEncodedSize);
                        }

                        int bytesRead = ReadCore(buffer, offset, bytesToRead);

                        // keep decoder up to date
                        DecodeData(buffer, offset, Math.Min(bytesRead, chunkBytesRemaining));

                        if (bytesRead > chunkBytesRemaining)
                        {
                            result += chunkBytesRemaining;
                            int overflowCount = bytesRead - chunkBytesRemaining;
                            int overflowOffset = offset + chunkBytesRemaining;
                            chunkBytesRemaining = 0;
                            // read at least part of the next chunk, and put any overflow in this.chunkBuffer
                            DecodeSize(buffer, ref overflowOffset, ref overflowCount);
                        }
                        else
                        {
                            result += bytesRead;
                            chunkBytesRemaining -= bytesRead;
                        }

                        return result;
                    }
                    else
                    {
                        // Final case: we have a new chunk. Read the size, and loop around again
                        if (count < IntEncoder.MaxEncodedSize)
                        {
                            // we don't have space for MaxEncodedSize, so it's worth the copy cost to read into a temp buffer
                            chunkBufferOffset = 0;
                            chunkBufferSize = ReadCore(chunkBuffer, 0, chunkBuffer.Length);
                            DecodeSize(chunkBuffer, ref chunkBufferOffset, ref chunkBufferSize);
                        }
                        else
                        {
                            int bytesRead = ReadCore(buffer, offset, IntEncoder.MaxEncodedSize);
                            int sizeOffset = offset;
                            DecodeSize(buffer, ref sizeOffset, ref bytesRead);
                        }
                    }
                }
            }

            void ProcessEof()
            {
                if (!atEof)
                {
                    atEof = true;
                    if (chunkBufferSize > 0 || chunkBytesRemaining > 0
                        || decoder.CurrentState != SingletonMessageDecoder.State.End)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(decoder.CreatePrematureEOFException());
                    }

                    reader.DoneReceivingAsync(true).GetAwaiter().GetResult();
                }
            }
        }
    }

    static class StreamingConnectionHelper
    {
        public static async Task WriteMessageAsync(Message message, IConnection connection, bool isRequest,
            IConnectionOrientedTransportFactorySettings settings, CancellationToken token)
        {
            // TODO: Switch to using the token as this is supposed to use remaining time
            var timeoutHelper = new TimeoutHelper(TimeoutHelper.GetOriginalTimeout(token));

            byte[] endBytes = null;
            if (message != null)
            {
                MessageEncoder messageEncoder = settings.MessageEncoderFactory.Encoder;
                byte[] envelopeStartBytes = SingletonEncoder.EnvelopeStartBytes;

                bool writeStreamed;
                if (isRequest)
                {
                    endBytes = SingletonEncoder.EnvelopeEndFramingEndBytes;
                    writeStreamed = TransferModeHelper.IsRequestStreamed(settings.TransferMode);
                }
                else
                {
                    endBytes = SingletonEncoder.EnvelopeEndBytes;
                    writeStreamed = TransferModeHelper.IsResponseStreamed(settings.TransferMode);
                }

                if (writeStreamed)
                {
                    await connection.WriteAsync(envelopeStartBytes, 0, envelopeStartBytes.Length, false, timeoutHelper.RemainingTime());
                    Stream connectionStream = new StreamingOutputConnectionStream(connection, settings);
                    Stream writeTimeoutStream = new TimeoutStream(connectionStream, timeoutHelper.RemainingTime());
                    messageEncoder.WriteMessage(message, writeTimeoutStream);
                }
                else
                {
                    ArraySegment<byte> messageData = messageEncoder.WriteMessage(message,
                        int.MaxValue, settings.BufferManager, envelopeStartBytes.Length + IntEncoder.MaxEncodedSize);
                    messageData = SingletonEncoder.EncodeMessageFrame(messageData);
                    Buffer.BlockCopy(envelopeStartBytes, 0, messageData.Array, messageData.Offset - envelopeStartBytes.Length,
                        envelopeStartBytes.Length);
                    await connection.WriteAsync(messageData.Array, messageData.Offset - envelopeStartBytes.Length,
                        messageData.Count + envelopeStartBytes.Length, true, timeoutHelper.RemainingTime(), settings.BufferManager);
                }
            }
            else if (isRequest) // context handles response end bytes
            {
                endBytes = SingletonEncoder.EndBytes;
            }

            if (endBytes != null)
            {
                await connection.WriteAsync(endBytes, 0, endBytes.Length,
                    true, timeoutHelper.RemainingTime());
            }
        }

        // overrides ConnectionStream to add a Framing int at the beginning of each record
        class StreamingOutputConnectionStream : ConnectionStream
        {
            byte[] encodedSize;

            public StreamingOutputConnectionStream(IConnection connection, IDefaultCommunicationTimeouts timeouts)
                : base(connection, timeouts)
            {
                encodedSize = new byte[IntEncoder.MaxEncodedSize];
            }
            void WriteChunkSize(int size)
            {
                if (size > 0)
                {
                    int bytesEncoded = IntEncoder.Encode(size, encodedSize, 0);
                    base.Connection.Write(encodedSize, 0, bytesEncoded, false, TimeSpan.FromMilliseconds(WriteTimeout));
                }
            }

            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            {
                WriteChunkSize(count);
                return base.BeginWrite(buffer, offset, count, callback, state);
            }

            public override void WriteByte(byte value)
            {
                WriteChunkSize(1);
                base.WriteByte(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                WriteChunkSize(count);
                base.Write(buffer, offset, count);
            }
        }
    }
}
