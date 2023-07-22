// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal delegate Task SendHandlerAsync(MessageAttemptInfo attemptInfo, CancellationToken token, bool maskUnhandledException);
    internal delegate void ComponentFaultedHandler(Exception faultException, WsrmFault fault);
    internal delegate void ComponentExceptionHandler(Exception exception);
    internal delegate Task AsyncRetryHandler(MessageAttemptInfo attemptInfo);

    internal sealed class ReliableOutputConnection
    {
        private static Action<object> s_sendRetries = new Action<object>(SendRetries);
        private readonly UniqueId _id;
        private readonly ReliableMessagingVersion _reliableMessagingVersion;
        private readonly Guard _sendGuard = new Guard(int.MaxValue);
        private TimeSpan _sendTimeout;
        private readonly InterruptibleWaitObject _shutdownHandle = new InterruptibleWaitObject(false);
        private bool _terminated = false;

        public ReliableOutputConnection(UniqueId id,
            int maxTransferWindowSize,
            MessageVersion messageVersion,
            ReliableMessagingVersion reliableMessagingVersion,
            TimeSpan initialRtt,
            bool requestAcks,
            TimeSpan sendTimeout)
        {
            _id = id;
            MessageVersion = messageVersion;
            _reliableMessagingVersion = reliableMessagingVersion;
            _sendTimeout = sendTimeout;
            Strategy = new TransmissionStrategy(reliableMessagingVersion, initialRtt, maxTransferWindowSize,
                requestAcks, id);
            Strategy.AsyncRetryTimeoutElapsed = OnRetryTimeoutElapsedAsync;
            Strategy.OnException = RaiseOnException;
        }

        public ComponentFaultedHandler Faulted;
        public ComponentExceptionHandler OnException;

        private MessageVersion MessageVersion { get; }
        public SendHandlerAsync AsyncSendHandler { private get; set; }
        public AsyncOperationWithCancellationCallback AsyncSendAckRequestedHandler { private get; set; }
        public bool Closed { get; private set; } = false;
        public long Last => Strategy.Last;
        public TransmissionStrategy Strategy { get; }
        private object ThisLock { get; } = new object();

        public void Abort(ServiceChannelBase channel)
        {
            _sendGuard.Abort();
            _shutdownHandle.Abort(channel);
            Strategy.Abort(channel);
        }

        private async Task CompleteTransferAsync(CancellationToken token)
        {
            if (_reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                Message message = Message.CreateMessage(MessageVersion, WsrmFeb2005Strings.LastMessageAction);
                message.Properties.AllowOutputBatching = false;

                // Return value ignored.
                await InternalAddMessageAsync(message, token, null, true);
            }
            else if (_reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                if (Strategy.SetLast())
                {
                    _shutdownHandle.Set();
                }
                else
                {
                    await AsyncSendAckRequestedHandler(token);
                }
            }
            else
            {
                throw Fx.AssertAndThrow("Unsupported version.");
            }
        }

        public Task<bool> AddMessageAsync(Message message, CancellationToken token, object state)
        {
            return InternalAddMessageAsync(message, token, state, false);
        }

        public bool CheckForTermination()
        {
            return Strategy.DoneTransmitting;
        }

        public async Task CloseAsync(CancellationToken token)
        {
            bool completeTransfer = false;

            lock (ThisLock)
            {
                completeTransfer = !Closed;
                Closed = true;
            }

            if (completeTransfer)
            {
                await CompleteTransferAsync(token);
            }

            await _shutdownHandle.WaitAsync(token);
            await _sendGuard.CloseAsync(token);
            Strategy.Close();
        }

        public void Fault(ServiceChannelBase channel)
        {
            _sendGuard.Abort();
            _shutdownHandle.Fault(channel);
            Strategy.Fault(channel);
        }

        private async Task<bool> InternalAddMessageAsync(Message message, CancellationToken token, object state, bool isLast)
        {
            MessageAttemptInfo attemptInfo;

            try
            {
                if (isLast)
                {
                    if (state != null)
                    {
                        throw Fx.AssertAndThrow("The isLast overload does not take a state.");
                    }

                    attemptInfo = await Strategy.AddLastAsync(message, token, null);
                }
                else
                {
                    (bool success, attemptInfo) = await Strategy.AddAsync(message, token, state);
                    if (!success) return false;
                }
            }
            catch (TimeoutException)
            {
                if (isLast)
                    RaiseFault(null, SequenceTerminatedFault.CreateCommunicationFault(_id, SR.SequenceTerminatedAddLastToWindowTimedOut, null));
                // else - RM does not fault the channel based on a timeout exception trying to add a sequenced message to the window.

                throw;
            }
            catch (Exception e)
            {
                if (!Fx.IsFatal(e))
                    RaiseFault(null, SequenceTerminatedFault.CreateCommunicationFault(_id, SR.SequenceTerminatedUnknownAddToWindowError, null));

                throw;
            }

            if (await _sendGuard.EnterAsync())
            {
                try
                {
                    await AsyncSendHandler(attemptInfo, token, false);
                }
                catch (QuotaExceededException)
                {
                    RaiseFault(null, SequenceTerminatedFault.CreateQuotaExceededFault(_id));
                    throw;
                }
                finally
                {
                    _sendGuard.Exit();
                }
            }

            return true;
        }

        public bool IsFinalAckConsistent(SequenceRangeCollection ranges)
        {
            return Strategy.IsFinalAckConsistent(ranges);
        }

        private async Task OnRetryTimeoutElapsedAsync(MessageAttemptInfo attemptInfo)
        {
            if (await _sendGuard.EnterAsync())
            {
                try
                {
                    await AsyncSendHandler(attemptInfo, TimeoutHelper.GetCancellationToken(_sendTimeout), true);
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                        throw;

                    RaiseOnException(e);
                }
                finally
                {
                    _sendGuard.Exit();
                }
            }
        }

        private void OnTransferComplete()
        {
            Strategy.DequeuePending();

            if (Strategy.DoneTransmitting)
                Terminate();
        }

        public void ProcessTransferred(long transferred, SequenceRangeCollection ranges, int quotaRemaining)
        {
            if (transferred < 0)
            {
                throw Fx.AssertAndThrow("Argument transferred must be a valid sequence number or 0 for protocol messages.");
            }

            bool invalidAck;

            // ignored, TransmissionStrategy is being used to keep track of what must be re-sent.
            // In the Request-Reply case this state may not align with acks.
            bool inconsistentAck;

            Strategy.ProcessAcknowledgement(ranges, out invalidAck, out inconsistentAck);
            invalidAck = (invalidAck || ((transferred != 0) && !ranges.Contains(transferred)));

            if (!invalidAck)
            {
                if ((transferred > 0) && Strategy.ProcessTransferred(transferred, quotaRemaining))
                {
                    ActionItem.Schedule(s_sendRetries, this);
                }
                else
                {
                    OnTransferComplete();
                }
            }
            else
            {
                WsrmFault fault = new InvalidAcknowledgementFault(_id, ranges);
                RaiseFault(fault.CreateException(), fault);
            }
        }

        public void ProcessTransferred(SequenceRangeCollection ranges, int quotaRemaining)
        {
            bool invalidAck;
            bool inconsistentAck;

            Strategy.ProcessAcknowledgement(ranges, out invalidAck, out inconsistentAck);

            if (!invalidAck && !inconsistentAck)
            {
                if (Strategy.ProcessTransferred(ranges, quotaRemaining))
                {
                    ActionItem.Schedule(s_sendRetries, this);
                }
                else
                {
                    OnTransferComplete();
                }
            }
            else
            {
                WsrmFault fault = new InvalidAcknowledgementFault(_id, ranges);
                RaiseFault(fault.CreateException(), fault);
            }
        }

        private void RaiseFault(Exception faultException, WsrmFault fault)
        {
            ComponentFaultedHandler handler = Faulted;

            if (handler != null)
                handler(faultException, fault);
        }

        private void RaiseOnException(Exception exception)
        {
            ComponentExceptionHandler handler = OnException;

            if (handler != null)
                handler(exception);
        }

        private static void SendRetries(object state)
        {
            ReliableOutputConnection outputConnection = (ReliableOutputConnection)state;
            _ = outputConnection.SendRetriesAsync();
        }

        private async Task SendRetriesAsync()
        {
            try
            {
                while (true)
                {
                    if (await _sendGuard.EnterAsync())
                    {
                        try
                        {
                            MessageAttemptInfo attemptInfo = Strategy.GetMessageInfoForRetry(false);
                            if (attemptInfo.Message == null)
                            {
                                break;
                            }
                            else
                            {
                                await AsyncSendHandler(attemptInfo, TimeoutHelper.GetCancellationToken(_sendTimeout), true);
                            }
                        }
                        finally
                        {
                            _sendGuard.Exit();
                        }
                    }

                    Strategy.DequeuePending();
                }

                OnTransferComplete();
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                    throw;

                RaiseOnException(e);
            }
        }

        public void Terminate()
        {
            lock (ThisLock)
            {
                if (_terminated)
                    return;

                _terminated = true;
            }

            _shutdownHandle.Set();
        }
    }
}
