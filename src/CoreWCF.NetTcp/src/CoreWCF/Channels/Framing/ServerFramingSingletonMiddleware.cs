using CoreWCF.Configuration;
using CoreWCF.Runtime;
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels.Framing
{
    internal class ServerFramingSingletonMiddleware
    {
        private HandshakeDelegate _next;

        public ServerFramingSingletonMiddleware(HandshakeDelegate next)
        {
            _next = next;
        }

        public async Task OnConnectedAsync(FramingConnection connection)
        {
            var receiveTimeout = connection.ServiceDispatcher.Binding.ReceiveTimeout;
            var timeoutHelper = new TimeoutHelper(receiveTimeout);
            bool success = false;
            try
            {
                var decoder = connection.FramingDecoder as ServerSingletonDecoder;
                Fx.Assert(decoder != null, "FramingDecoder must be non-null and an instance of ServerSessionDecoder");

                // first validate our content type
                //ValidateContentType(connection, decoder);
                UpgradeState upgradeState = UpgradeState.None;
                // next read any potential upgrades and finish consuming the preamble
                ReadOnlySequence<byte> buffer = ReadOnlySequence<byte>.Empty;
                while (true)
                {
                    if (buffer.Length == 0 && CanReadAndDecode(upgradeState))
                    {
                        var readResult = await connection.Input.ReadAsync();
                        buffer = readResult.Buffer;
                        if (readResult.IsCompleted)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(decoder.CreatePrematureEOFException());
                        }
                    }

                    while (true)
                    {
                        if (CanReadAndDecode(upgradeState))
                        {
                            Fx.Assert(buffer.Length > 0, "There must be something in the buffer to decode");
                            int bytesDecoded = decoder.Decode(buffer);
                            if (bytesDecoded > 0)
                            {
                                buffer = buffer.Slice(bytesDecoded);
                                if (buffer.Length == 0)
                                {
                                    connection.Input.AdvanceTo(buffer.Start);
                                }
                            }
                        }

                        switch (decoder.CurrentState)
                        {
                            case ServerSingletonDecoder.State.UpgradeRequest:
                                switch (upgradeState)
                                {
                                    case UpgradeState.None:
                                        //change the state so that we don't read/decode until it is safe
                                        ChangeUpgradeState(ref upgradeState, UpgradeState.VerifyingUpgradeRequest);
                                        break;
                                    case UpgradeState.VerifyingUpgradeRequest:
                                        if (connection.StreamUpgradeAcceptor == null)
                                        {
                                            await connection.SendFaultAsync(FramingEncodingString.UpgradeInvalidFault, timeoutHelper.RemainingTime(), TransportDefaults.MaxDrainSize);
                                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                                new ProtocolException(SR.Format(SR.UpgradeRequestToNonupgradableService, decoder.Upgrade)));
                                        }

                                        if (!connection.StreamUpgradeAcceptor.CanUpgrade(decoder.Upgrade))
                                        {
                                            await connection.SendFaultAsync(FramingEncodingString.UpgradeInvalidFault, timeoutHelper.RemainingTime(), TransportDefaults.MaxDrainSize);
                                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ProtocolException(SR.Format(SR.UpgradeProtocolNotSupported, decoder.Upgrade)));
                                        }

                                        ChangeUpgradeState(ref upgradeState, UpgradeState.WritingUpgradeAck);
                                        // accept upgrade
                                        await connection.Output.WriteAsync(ServerSingletonEncoder.UpgradeResponseBytes, timeoutHelper.GetCancellationToken());
                                        await connection.Output.FlushAsync(timeoutHelper.GetCancellationToken());
                                        ChangeUpgradeState(ref upgradeState, UpgradeState.UpgradeAckSent);
                                        break;
                                    case UpgradeState.UpgradeAckSent:
                                        // This state was used to capture any extra read bytes into PreReadConnection but we don't need to do that when using pipes.
                                        // This extra state transition has been left here to maintain the same state transitions as on .NET Framework to make comparison easier.
                                        ChangeUpgradeState(ref upgradeState, UpgradeState.BeginUpgrade);
                                        break;
                                    case UpgradeState.BeginUpgrade:
                                        // Set input pipe so that the next read will return all the unconsumed bytes.
                                        // If all bytes have already been consumed so the buffer has 0 length, AdvanceTo would throw
                                        // as it's already been called.
                                        if (buffer.Length > 0)
                                        {
                                            connection.Input.AdvanceTo(buffer.Start);
                                        }

                                        buffer = ReadOnlySequence<byte>.Empty;
                                        try
                                        {
                                            await UpgradeConnectionAsync(connection);
                                            ChangeUpgradeState(ref upgradeState, UpgradeState.EndUpgrade);
                                        }
                                        catch (Exception exception)
                                        {
                                            if (Fx.IsFatal(exception))
                                                throw;

                                            throw;
                                        }
                                        break;
                                    case UpgradeState.EndUpgrade:
                                        //Must be a different state here than UpgradeComplete so that we don't try to read from the connection
                                        ChangeUpgradeState(ref upgradeState, UpgradeState.UpgradeComplete);
                                        break;
                                    case UpgradeState.UpgradeComplete:
                                        //Client is doing more than one upgrade, reset the state
                                        ChangeUpgradeState(ref upgradeState, UpgradeState.VerifyingUpgradeRequest);
                                        break;
                                }
                                break;
                            case ServerSingletonDecoder.State.Start:
                                SetupSecurityIfNecessary(connection);
                                if (upgradeState == UpgradeState.UpgradeComplete //We have done at least one upgrade, but we are now done.
                                    || upgradeState == UpgradeState.None)//no upgrade, just send the preample end bytes
                                {
                                    ChangeUpgradeState(ref upgradeState, UpgradeState.WritingPreambleEnd);
                                    // we've finished the preamble. Ack and return.
                                    await connection.Output.WriteAsync(ServerSessionEncoder.AckResponseBytes);
                                    await connection.Output.FlushAsync();
                                    //terminal state
                                    ChangeUpgradeState(ref upgradeState, UpgradeState.PreambleEndSent);
                                }
                                // If all bytes have already been consumed so the buffer has 0 length, AdvanceTo would throw
                                // as it's already been called.
                                if (buffer.Length > 0)
                                {
                                    connection.Input.AdvanceTo(buffer.Start);
                                }

                                success = true;
                                await _next(connection);
                                return;
                        }

                        if (buffer.Length == 0)
                        {
                            break;
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

        private bool CanReadAndDecode(UpgradeState upgradeState)
        {
            //ok to read/decode before we start the upgrade
            //and between UpgradeComplete/WritingPreambleAck
            return upgradeState == UpgradeState.None
                || upgradeState == UpgradeState.UpgradeComplete;
        }

        void ChangeUpgradeState(ref UpgradeState currentState, UpgradeState newState)
        {
            switch (newState)
            {
                case UpgradeState.None:
                    throw Fx.AssertAndThrow("Invalid State Transition: currentState=" + currentState + ", newState=" + newState);
                case UpgradeState.VerifyingUpgradeRequest:
                    if (currentState != UpgradeState.None //starting first upgrade
                        && currentState != UpgradeState.UpgradeComplete)//completing one upgrade and starting another
                    {
                        throw Fx.AssertAndThrow("Invalid State Transition: currentState=" + currentState + ", newState=" + newState);
                    }
                    break;
                case UpgradeState.WritingUpgradeAck:
                    if (currentState != UpgradeState.VerifyingUpgradeRequest)
                    {
                        throw Fx.AssertAndThrow("Invalid State Transition: currentState=" + currentState + ", newState=" + newState);
                    }
                    break;
                case UpgradeState.UpgradeAckSent:
                    if (currentState != UpgradeState.WritingUpgradeAck)
                    {
                        throw Fx.AssertAndThrow("Invalid State Transition: currentState=" + currentState + ", newState=" + newState);
                    }
                    break;
                case UpgradeState.BeginUpgrade:
                    if (currentState != UpgradeState.UpgradeAckSent)
                    {
                        throw Fx.AssertAndThrow("Invalid State Transition: currentState=" + currentState + ", newState=" + newState);
                    }
                    break;
                case UpgradeState.EndUpgrade:
                    if (currentState != UpgradeState.BeginUpgrade)
                    {
                        throw Fx.AssertAndThrow("Invalid State Transition: currentState=" + currentState + ", newState=" + newState);
                    }
                    break;
                case UpgradeState.UpgradeComplete:
                    if (currentState != UpgradeState.EndUpgrade)
                    {
                        throw Fx.AssertAndThrow("Invalid State Transition: currentState=" + currentState + ", newState=" + newState);
                    }
                    break;
                case UpgradeState.WritingPreambleEnd:
                    if (currentState != UpgradeState.None //no upgrade being used
                        && currentState != UpgradeState.UpgradeComplete)//upgrades are now complete, end the preamble handshake.
                    {
                        throw Fx.AssertAndThrow("Invalid State Transition: currentState=" + currentState + ", newState=" + newState);
                    }
                    break;
                case UpgradeState.PreambleEndSent:
                    if (currentState != UpgradeState.WritingPreambleEnd)
                    {
                        throw Fx.AssertAndThrow("Invalid State Transition: currentState=" + currentState + ", newState=" + newState);
                    }
                    break;
                default:
                    throw Fx.AssertAndThrow("Unexpected Upgrade State: " + newState);
            }

            currentState = newState;
        }

        public static async Task UpgradeConnectionAsync(FramingConnection connection)
        {
            connection.RawStream = new RawStream(connection.Input, connection.Output);
            var upgradeAcceptor = connection.StreamUpgradeAcceptor;
            var stream = await upgradeAcceptor.AcceptUpgradeAsync(connection.RawStream);
            CreatePipelineFromStream(connection, stream);
        }

        private static void CreatePipelineFromStream(FramingConnection connection, Stream stream)
        {
            var wrappedPipeline = new StreamDuplexPipe(connection.Transport, stream);
            connection.Transport = wrappedPipeline;
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
    }
}