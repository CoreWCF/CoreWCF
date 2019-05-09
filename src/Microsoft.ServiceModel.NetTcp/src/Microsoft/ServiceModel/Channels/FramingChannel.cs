using Microsoft.Runtime;
using Microsoft.ServiceModel;
using Microsoft.ServiceModel.Diagnostics;
using Microsoft.ServiceModel.Security;
using Microsoft.Runtime.Diagnostics;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Security;
using System.Security.Authentication.ExtendedProtection;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel.Channels
{
    abstract class FramingDuplexSessionChannel : TransportDuplexSessionChannel
    {
        IConnection connection;
        bool exposeConnectionProperty;

        FramingDuplexSessionChannel(ChannelManagerBase manager, IConnectionOrientedTransportFactorySettings settings,
            EndpointAddress localAddress, Uri localVia, EndpointAddress remoteAddresss, Uri via, bool exposeConnectionProperty)
            : base(manager, settings, localAddress, localVia, remoteAddresss, via)
        {
            this.exposeConnectionProperty = exposeConnectionProperty;
        }

        protected FramingDuplexSessionChannel(ChannelManagerBase factory, IConnectionOrientedTransportFactorySettings settings,
            EndpointAddress remoteAddresss, Uri via, bool exposeConnectionProperty)
            : this(factory, settings, EndpointAddress.AnonymousAddress, settings.MessageVersion.Addressing.AnonymousUri(),
            remoteAddresss, via, exposeConnectionProperty)
        {
            Session = FramingConnectionDuplexSession.CreateSession(this, settings.Upgrade);
        }

        protected FramingDuplexSessionChannel(ConnectionOrientedTransportChannelListener channelListener,
            EndpointAddress localAddress, Uri localVia, bool exposeConnectionProperty)
            : this(channelListener, channelListener, localAddress, localVia,
            EndpointAddress.AnonymousAddress, channelListener.MessageVersion.Addressing.AnonymousUri(), exposeConnectionProperty)
        {
            Session = FramingConnectionDuplexSession.CreateSession(this, channelListener.Upgrade);
        }

        protected IConnection Connection
        {
            get
            {
                return connection;
            }
            set
            {
                connection = value;
            }
        }

        protected override bool IsStreamedOutput
        {
            get { return false; }
        }

        protected override Task CloseOutputSessionCoreAsync(CancellationToken token)
        {
            var timeout = TimeoutHelper.GetOriginalTimeout(token);
            return Connection.WriteAsync(SessionEncoder.EndBytes, 0, SessionEncoder.EndBytes.Length, true, timeout);
        }

        protected override Task CompleteCloseAsync(CancellationToken token)
        {
            ReturnConnectionIfNecessary(false, token);
            return Task.CompletedTask;
        }

        protected override void PrepareMessage(Message message)
        {
            if (exposeConnectionProperty)
            {
                message.Properties[ConnectionMessageProperty.Name] = connection;
            }
            base.PrepareMessage(message);
        }

        protected override void OnSendCore(Message message, TimeSpan timeout)
        {
            bool allowOutputBatching;
            ArraySegment<byte> messageData;
            allowOutputBatching = message.Properties.AllowOutputBatching;
            messageData = EncodeMessage(message);
            Connection.Write(messageData.Array, messageData.Offset, messageData.Count, !allowOutputBatching,
                timeout, BufferManager);
        }

        protected override Task CloseOutputAsync(CancellationToken token)
        {
            var timeout = TimeoutHelper.GetOriginalTimeout(token);
            return Connection.WriteAsync(SessionEncoder.EndBytes, 0, SessionEncoder.EndBytes.Length,
                    true, timeout);
        }

        //protected override void FinishWritingMessage()
        //{
        //    this.Connection.EndWrite();
        //}

        protected override Task StartWritingBufferedMessageAsync(Message message, ArraySegment<byte> messageData, bool allowOutputBatching, CancellationToken token)
        {
            var timeout = TimeoutHelper.GetOriginalTimeout(token);
            return Connection.WriteAsync(messageData.Array, messageData.Offset, messageData.Count,
                    !allowOutputBatching, timeout);
        }

        protected override Task StartWritingStreamedMessageAsync(Message message, CancellationToken token)
        {
            Fx.Assert(false, "Streamed output should never be called in this channel.");
            return Task.FromException(Fx.Exception.AsError(new InvalidOperationException()));
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
                StreamUpgradeProvider upgrade)
            {
                StreamSecurityUpgradeProvider security = upgrade as StreamSecurityUpgradeProvider;
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

    // used by StreamedFramingRequestChannel and ClientFramingDuplexSessionChannel
    class ConnectionUpgradeHelper
    {
        public static async Task DecodeFramingFaultAsync(ClientFramingDecoder decoder, IConnection connection,
            Uri via, string contentType, TimeoutHelper timeoutHelper)
        {
            ValidateReadingFaultString(decoder);

            int offset = 0;
            byte[] faultBuffer = Fx.AllocateByteArray(FaultStringDecoder.FaultSizeQuota);
            int size = await connection.ReadAsync(0, Math.Min(FaultStringDecoder.FaultSizeQuota, connection.AsyncReadBufferSize),
                    timeoutHelper.RemainingTime());

            while (size > 0)
            {
                int bytesDecoded = decoder.Decode(connection.AsyncReadBuffer, offset, size);
                offset += bytesDecoded;
                size -= bytesDecoded;

                if (decoder.CurrentState == ClientFramingDecoderState.Fault)
                {
                    ConnectionUtilities.CloseNoThrow(connection, timeoutHelper.RemainingTime());
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        FaultStringDecoder.GetFaultException(decoder.Fault, via.ToString(), contentType));
                }
                else
                {
                    if (decoder.CurrentState != ClientFramingDecoderState.ReadingFaultString)
                    {
                        throw Fx.AssertAndThrow("invalid framing client state machine");
                    }
                    if (size == 0)
                    {
                        offset = 0;
                        size = await connection.ReadAsync(0, Math.Min(FaultStringDecoder.FaultSizeQuota, connection.AsyncReadBufferSize),
                                timeoutHelper.RemainingTime());
                    }
                }
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(decoder.CreatePrematureEOFException());
        }

        static void ValidateReadingFaultString(ClientFramingDecoder decoder)
        {
            if (decoder.CurrentState != ClientFramingDecoderState.ReadingFaultString)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                    SR.Format(SR.ServerRejectedUpgradeRequest)));
            }
        }

        static bool ValidateUpgradeResponse(byte[] buffer, int count, ClientFramingDecoder decoder)
        {
            if (count == 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.ServerRejectedUpgradeRequest), decoder.CreatePrematureEOFException()));
            }

            // decode until the framing byte has been processed (it always will be)
            while (decoder.Decode(buffer, 0, count) == 0)
            {
                // do nothing
            }

            if (decoder.CurrentState != ClientFramingDecoderState.UpgradeResponse) // we have a problem
            {
                return false;
            }

            return true;
        }
    }
}
