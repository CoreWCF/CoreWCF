using Microsoft.AspNetCore.Connections;
using CoreWCF.Runtime;
using CoreWCF.Configuration;
using CoreWCF.Security;
using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CoreWCF.Channels.Framing
{
    internal class ServerFramingDuplexSessionMiddleware
    {
        private HandshakeDelegate _next;

        public ServerFramingDuplexSessionMiddleware(HandshakeDelegate next)
        {
            _next = next;
        }

        public async Task OnConnectedAsync(FramingConnection connection)
        {
            bool success = false;
            try
            {
                var decoder = connection.FramingDecoder as ServerSessionDecoder;
                Fx.Assert(decoder != null, "FramingDecoder must be non-null and an instance of ServerSessionDecoder");
                // first validate our content type
                ValidateContentType(connection, decoder);

                // next read any potential upgrades and finish consuming the preamble
                ReadOnlySequence<byte> buffer;
                while (true)
                {
                    var readResult = await connection.Input.ReadAsync();
                    buffer = readResult.Buffer;
                    if (readResult.IsCompleted)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(decoder.CreatePrematureEOFException());
                    }

                    while (buffer.Length > 0)
                    {
                        int bytesDecoded = decoder.Decode(buffer);
                        if (bytesDecoded > 0)
                        {
                            buffer = buffer.Slice(bytesDecoded);
                        }

                        switch (decoder.CurrentState)
                        {
                            case ServerSessionDecoder.State.UpgradeRequest:
                                ProcessUpgradeRequest(connection, decoder);

                                // accept upgrade
                                await connection.Output.WriteAsync(ServerSessionEncoder.UpgradeResponseBytes);
                                await connection.Output.FlushAsync();
                                //await context.Transport.Output.WriteAsync
                                //Connection.Write(ServerSessionEncoder.UpgradeResponseBytes, 0, ServerSessionEncoder.UpgradeResponseBytes.Length, true, timeoutHelper.RemainingTime());

                                try
                                {
                                    connection.Input.AdvanceTo(buffer.Start);
                                    buffer = ReadOnlySequence<byte>.Empty;
                                    await UpgradeConnectionAsync(connection);
                                    // TODO: ChannelBinding
                                    //if (this.channelBindingProvider != null && this.channelBindingProvider.IsChannelBindingSupportEnabled)
                                    //{
                                    //    this.SetChannelBinding(this.channelBindingProvider.GetChannelBinding(this.upgradeAcceptor, ChannelBindingKind.Endpoint));
                                    //}

                                    //this.connectionBuffer = Connection.AsyncReadBuffer;
                                }
                                catch (Exception exception)
                                {
                                    if (Fx.IsFatal(exception))
                                        throw;

                                    // Audit Authentication Failure
                                    //WriteAuditFailure(upgradeAcceptor as StreamSecurityUpgradeAcceptor, exception);
                                    throw;
                                }
                                break;

                            case ServerSessionDecoder.State.Start:
                                SetupSecurityIfNecessary(connection);

                                // we've finished the preamble. Ack and continue to the next middleware.
                                await connection.Output.WriteAsync(ServerSessionEncoder.AckResponseBytes);
                                await connection.Output.FlushAsync();
                                connection.Input.AdvanceTo(buffer.Start);
                                success = true;
                                await _next(connection);
                                return;
                        }
                    }
                }
            }
            finally
            {
                if (!success)
                {
                    connection.Abort();
                }
            }
        }

        private static void ValidateContentType(FramingConnection connection, FramingDecoder decoder)
        {
            var messageEncoderFactory = connection.MessageEncoderFactory;
            MessageEncoder messageEncoder = messageEncoderFactory.CreateSessionEncoder();
            connection.MessageEncoder = messageEncoder;
            
            if (!messageEncoder.IsContentTypeSupported(decoder.ContentType))
            {
                // TODO: Send fault response
                //SendFault(FramingEncodingString.ContentTypeInvalidFault, ref timeoutHelper);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ProtocolException(SR.Format(
                    SR.ContentTypeMismatch, decoder.ContentType, messageEncoder.ContentType)));
            }

            ICompressedMessageEncoder compressedMessageEncoder = messageEncoder as ICompressedMessageEncoder;
            if (compressedMessageEncoder != null && compressedMessageEncoder.CompressionEnabled)
            {
                compressedMessageEncoder.SetSessionContentType(decoder.ContentType);
            }
        }

        private static void ProcessUpgradeRequest(FramingConnection connection, ServerSessionDecoder decoder)
        {
            var upgradeAcceptor = connection.StreamUpgradeAcceptor;
            if (upgradeAcceptor == null)
            {
                // TODO: SendFault
                //SendFault(FramingEncodingString.UpgradeInvalidFault, ref timeoutHelper);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ProtocolException(SR.Format(SR.UpgradeRequestToNonupgradableService, decoder.Upgrade)));
            }

            if (!upgradeAcceptor.CanUpgrade(decoder.Upgrade))
            {
                // TODO: SendFault
                //SendFault(FramingEncodingString.UpgradeInvalidFault, ref timeoutHelper);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ProtocolException(SR.Format(SR.UpgradeProtocolNotSupported, decoder.Upgrade)));
            }
        }

        public static async Task UpgradeConnectionAsync(FramingConnection connection)
        {
            connection.RawStream = new RawStream(connection.Input, connection.Output);
            var upgradeAcceptor = connection.StreamUpgradeAcceptor;
            var stream = await upgradeAcceptor.AcceptUpgradeAsync(connection.RawStream);
            CreatePipelineFromStream(connection, stream);
        }

        private static void SetupSecurityIfNecessary(FramingConnection connection)
        {
            StreamSecurityUpgradeAcceptor securityUpgradeAcceptor = connection.StreamUpgradeAcceptor as StreamSecurityUpgradeAcceptor;
            if (securityUpgradeAcceptor != null)
            {
                var remoteSecurity = securityUpgradeAcceptor.GetRemoteSecurity();

                if (remoteSecurity == null)
                {
                    Exception securityFailedException = new ProtocolException(
                        SR.Format(SR.RemoteSecurityNotNegotiatedOnStreamUpgrade, connection.Via));
                    //WriteAuditFailure(securityUpgradeAcceptor, securityFailedException);
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(securityFailedException);
                }
                else
                {
                    connection.SecurityMessageProperty = remoteSecurity;
                    // Audit Authentication Success
                    //WriteAuditEvent(securityUpgradeAcceptor, AuditLevel.Success, null);
                }
            }
        }

        private static void CreatePipelineFromStream(FramingConnection connection, Stream stream)
        {
            var wrappedPipeline = new StreamDuplexPipe(connection.Transport, stream);
            connection.Transport = wrappedPipeline;
        }
    }
}