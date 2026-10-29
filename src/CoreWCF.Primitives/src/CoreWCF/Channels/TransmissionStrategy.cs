// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Diagnostics;
using CoreWCF.Runtime;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System;
using System.Xml;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    internal struct MessageAttemptInfo
    {
        private readonly long _sequenceNumber;

        public MessageAttemptInfo(Message message, long sequenceNumber, int retryCount, object state)
        {
            Message = message;
            _sequenceNumber = sequenceNumber;
            RetryCount = retryCount;
            State = state;
        }
        public readonly Message Message { get; }

        public readonly int RetryCount { get; }

        public object State { get; }

        public long GetSequenceNumber()
        {
            Fx.AssertAndThrow(_sequenceNumber > 0, "The caller is not allowed to get an invalid SequenceNumber.");
            return _sequenceNumber;
        }
    }

    internal sealed class TransmissionStrategy
    {
        private bool _aborted;
        private bool _closed;
        private int _congestionControlModeAcks;
        private readonly UniqueId _id;
        private long _last = 0;
        private int _lossWindowSize;
        private readonly int _maxWindowSize;
        private long _meanRtt;
        private int _quotaRemaining;
        private readonly ReliableMessagingVersion _reliableMessagingVersion;
        private readonly List<long> _retransmissionWindow = new List<long>();
        private readonly IOThreadTimer _retryTimer;
        private readonly bool _requestAcks;
        private long _serrRtt;
        private int _slowStartThreshold;
        private bool _startup = true;
        private readonly object _thisLock = new object();
        private long _timeout;
        private readonly Queue<IQueueAdder> _waitQueue = new Queue<IQueueAdder>();
        private readonly SlidingWindow _window;
        private int _windowSize = 1;
        private long _windowStart = 1;

        public TransmissionStrategy(ReliableMessagingVersion reliableMessagingVersion, TimeSpan initRtt,
            int maxWindowSize, bool requestAcks, UniqueId id)
        {
            if (initRtt < TimeSpan.Zero)
            {
                //if (DiagnosticUtility.ShouldTrace(TraceEventType.Warning))
                //{
                //    TraceUtility.TraceEvent(TraceEventType.Warning, TraceCode.WsrmNegativeElapsedTimeDetected,
                //    SR.TraceCodeWsrmNegativeElapsedTimeDetected, this);
                //}

                initRtt = ReliableMessagingConstants.UnknownInitiationTime;
            }

            Fx.AssertAndThrow(maxWindowSize > 0, "Argument maxWindow size must be positive.");

            _id = id;
            _maxWindowSize = _lossWindowSize = maxWindowSize;
            _meanRtt = Math.Min((long)initRtt.TotalMilliseconds, Constants.MaxMeanRtt >> Constants.TimeMultiplier) << Constants.TimeMultiplier;
            _serrRtt = _meanRtt >> 1;
            _window = new SlidingWindow(maxWindowSize);
            _slowStartThreshold = maxWindowSize;
            _timeout = Math.Max(((200 << Constants.TimeMultiplier) * 2) + _meanRtt, _meanRtt + (_serrRtt << Constants.ChebychevFactor));
            _quotaRemaining = int.MaxValue;
            _retryTimer = new IOThreadTimer(new Action<object>(OnRetryElapsed), null, true);
            _requestAcks = requestAcks;
            _reliableMessagingVersion = reliableMessagingVersion;
        }

        public bool DoneTransmitting => (_last != 0 && _windowStart == _last + 1);
        public bool HasPending => (_window.Count > 0 || _waitQueue.Count > 0);
        public long Last => _last;
        // now in 128ths of a millisecond.
        private static long Now => (Ticks.Now / TimeSpan.TicksPerMillisecond) << Constants.TimeMultiplier;
        public ComponentExceptionHandler OnException { private get; set; }
        public AsyncRetryHandler AsyncRetryTimeoutElapsed { private get; set; }
        public int QuotaRemaining => _quotaRemaining;
        private object ThisLock => _thisLock;
        public int Timeout => (int)(_timeout >> Constants.TimeMultiplier);

        public void Abort(ServiceChannelBase channel)
        {
            lock (ThisLock)
            {
                _aborted = true;

                if (_closed)
                    return;

                _closed = true;

                _retryTimer.Cancel();

                while (_waitQueue.Count > 0)
                    _waitQueue.Dequeue().Abort(channel);

                _window.Close();
            }
        }

        public Task<(bool success, MessageAttemptInfo attemptInfo)> AddAsync(Message message, CancellationToken token, object state)
        {
            return InternalAddAsync(message, false, token, state);
        }

        public async Task<MessageAttemptInfo> AddLastAsync(Message message, CancellationToken token, object state)
        {
            if (_reliableMessagingVersion != ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                throw Fx.AssertAndThrow("Last message supported only in February 2005.");
            }

            MessageAttemptInfo attemptInfo = default;
            (_, attemptInfo) = await InternalAddAsync(message, true, token, state);
            return attemptInfo;
        }

        // Must call in a lock(this.ThisLock).
        private MessageAttemptInfo AddToWindow(Message message, bool isLast, object state)
        {
            MessageAttemptInfo attemptInfo = default;
            long sequenceNumber;

            sequenceNumber = _windowStart + _window.Count;
            WsrmUtilities.AddSequenceHeader(_reliableMessagingVersion, message, _id, sequenceNumber, isLast);

            if (_requestAcks && (_window.Count == _windowSize - 1 || _quotaRemaining == 1)) // can't add any more
            {
                message.Properties.AllowOutputBatching = false;
                WsrmUtilities.AddAckRequestedHeader(_reliableMessagingVersion, message, _id);
            }

            if (_window.Count == 0)
            {
                _retryTimer.Set(Timeout);
            }

            _window.Add(message, Now, state);
            _quotaRemaining--;
            if (isLast)
                _last = sequenceNumber;

            int index = (int)(sequenceNumber - _windowStart);
            attemptInfo = new MessageAttemptInfo(_window.GetMessage(index), sequenceNumber, 0, state);

            return attemptInfo;
        }

        private bool CanAdd()
        {
            return _window.Count < _windowSize &&  // Does the message fit in the transmission window?
                _quotaRemaining > 0 &&                  // Can the receiver handle another message?
                _waitQueue.Count == 0;                 // Don't get ahead of anyone in the wait queue.
        }

        public void Close()
        {
            lock (ThisLock)
            {
                if (_closed)
                    return;

                _closed = true;
                _retryTimer.Cancel();
                Fx.AssertAndThrow(_waitQueue.Count == 0, "The reliable channel must throw prior to the call to Close() if there are outstanding send or request operations.");
                _window.Close();
            }
        }

        public void DequeuePending()
        {
            Queue<IQueueAdder> adders = null;

            lock (ThisLock)
            {
                if (_closed || _waitQueue.Count == 0)
                    return;

                int count = Math.Min(_windowSize, _quotaRemaining) - _window.Count;
                if (count <= 0)
                    return;

                count = Math.Min(count, _waitQueue.Count);
                adders = new Queue<IQueueAdder>(count);

                while (count-- > 0)
                {
                    IQueueAdder adder = _waitQueue.Dequeue();
                    adder.Complete0();
                    adders.Enqueue(adder);
                }
            }

            while (adders.Count > 0)
                adders.Dequeue().Complete1();
        }

        private bool IsAddValid()
        {
            return (!_aborted && !_closed);
        }

        public void OnRetryElapsed(object state)
        {
            try
            {
                MessageAttemptInfo attemptInfo = default;

                lock (ThisLock)
                {
                    if (_closed)
                        return;

                    if (_window.Count == 0)
                        return;

                    _window.RecordRetry(0, Now);
                    _congestionControlModeAcks = 0;
                    _slowStartThreshold = Math.Max(1, _windowSize >> 1);
                    _lossWindowSize = _windowSize;
                    _windowSize = 1;
                    _timeout <<= 1;
                    _startup = false;

                    attemptInfo = new MessageAttemptInfo(_window.GetMessage(0), _windowStart, _window.GetRetryCount(0), _window.GetState(0));
                }

                // Original code was APM based and made no effort to wait for the implementation to complete before progressing
                // if it went async. Discarding the returned Task to match behavior
                _ = AsyncRetryTimeoutElapsed(attemptInfo);

                lock (ThisLock)
                {
                    if (!_closed && (_window.Count > 0))
                    {
                        _retryTimer.Set(Timeout);
                    }
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                    throw;

                OnException(e);
            }
        }

        public void Fault(ServiceChannelBase channel)
        {
            lock (ThisLock)
            {
                if (_closed)
                    return;

                _closed = true;

                _retryTimer.Cancel();

                while (_waitQueue.Count > 0)
                    _waitQueue.Dequeue().Fault(channel);

                _window.Close();
            }
        }

        public MessageAttemptInfo GetMessageInfoForRetry(bool remove)
        {
            lock (ThisLock)
            {
                // Closed, no need to retry.
                if (_closed)
                {
                    return default;
                }

                if (remove)
                {
                    if (_retransmissionWindow.Count == 0)
                    {
                        throw Fx.AssertAndThrow("The caller is not allowed to remove a message attempt when there are no message attempts.");
                    }

                    _retransmissionWindow.RemoveAt(0);
                }

                while (_retransmissionWindow.Count > 0)
                {
                    long next = _retransmissionWindow[0];
                    if (next < _windowStart)
                    {
                        // Already removed from the window, no need to retry.
                        _retransmissionWindow.RemoveAt(0);
                    }
                    else
                    {
                        int index = (int)(next - _windowStart);
                        if (_window.GetTransferred(index))
                            _retransmissionWindow.RemoveAt(0);
                        else
                            return new MessageAttemptInfo(_window.GetMessage(index), next, _window.GetRetryCount(index), _window.GetState(index));
                    }
                }

                // Nothing left to retry.
                return default;
            }
        }

        public bool SetLast()
        {
            if (_reliableMessagingVersion != ReliableMessagingVersion.WSReliableMessaging11)
            {
                throw Fx.AssertAndThrow("SetLast supported only in 1.1.");
            }

            lock (ThisLock)
            {
                if (_last != 0)
                {
                    throw Fx.AssertAndThrow("Cannot set last more than once.");
                }

                _last = _windowStart + _window.Count - 1;
                return (_last == 0) || DoneTransmitting;
            }
        }

        private async Task<(bool success, MessageAttemptInfo attemptInfo)> InternalAddAsync(Message message, bool isLast, CancellationToken token, object state)
        {
            WaitQueueAdder adder;

            lock (ThisLock)
            {
                if (isLast && _last != 0)
                {
                    throw Fx.AssertAndThrow("Can't add more than one last message.");
                }

                if (!IsAddValid())
                    return (false, default);

                ThrowIfRollover();

                if (CanAdd())
                {
                    return (true, AddToWindow(message, isLast, state));
                }

                adder = new WaitQueueAdder(this, message, isLast, state);
                _waitQueue.Enqueue(adder);
            }

            return (true, await adder.WaitAsync(token));
        }

        public bool IsFinalAckConsistent(SequenceRangeCollection ranges)
        {
            lock (ThisLock)
            {
                if (_closed)
                {
                    return true;
                }

                // Nothing sent, ensure ack is empty.
                if ((_windowStart == 1) && (_window.Count == 0))
                {
                    return ranges.Count == 0;
                }

                // Ack is empty or first range is invalid.
                if (ranges.Count == 0 || ranges[0].Lower != 1)
                {
                    return false;
                }

                return ranges[0].Upper >= (_windowStart - 1);
            }
        }

        public void ProcessAcknowledgement(SequenceRangeCollection ranges, out bool invalidAck, out bool inconsistentAck)
        {
            invalidAck = false;
            inconsistentAck = false;
            bool newAck = false;
            bool oldAck = false;

            lock (ThisLock)
            {
                if (_closed)
                {
                    return;
                }

                long lastMessageSent = _windowStart + _window.Count - 1;
                long lastMessageAcked = _windowStart - 1;
                int transferredInWindow = _window.TransferredCount;

                for (int i = 0; i < ranges.Count; i++)
                {
                    SequenceRange range = ranges[i];

                    // Ack for a message not yet sent.
                    if (range.Upper > lastMessageSent)
                    {
                        invalidAck = true;
                        return;
                    }

                    if (((range.Lower > 1) && (range.Lower <= lastMessageAcked)) || (range.Upper < lastMessageAcked))
                    {
                        oldAck = true;
                    }

                    if (range.Upper >= _windowStart)
                    {
                        if (range.Lower <= _windowStart)
                        {
                            newAck = true;
                        }

                        if (!newAck)
                        {
                            int beginIndex = (int)(range.Lower - _windowStart);
                            int endIndex = (int)((range.Upper > lastMessageSent) ? (_window.Count - 1) : (range.Upper - _windowStart));

                            newAck = _window.GetTransferredInRangeCount(beginIndex, endIndex) < (endIndex - beginIndex + 1);
                        }

                        if (transferredInWindow > 0 && !oldAck)
                        {
                            int beginIndex = (int)((range.Lower < _windowStart) ? 0 : (range.Lower - _windowStart));
                            int endIndex = (int)((range.Upper > lastMessageSent) ? (_window.Count - 1) : (range.Upper - _windowStart));

                            transferredInWindow -= _window.GetTransferredInRangeCount(beginIndex, endIndex);
                        }
                    }
                }

                if (transferredInWindow > 0)
                    oldAck = true;
            }

            inconsistentAck = oldAck && newAck;
        }

        // Called for RequestReply.
        // Argument transferred is the request sequence number and it is assumed to be positive.
        public bool ProcessTransferred(long transferred, int quotaRemaining)
        {
            Fx.AssertAndThrow(transferred > 0, "Argument transferred must be a valid sequence number.");

            lock (ThisLock)
            {
                if (_closed)
                {
                    return false;
                }

                return ProcessTransferred(new SequenceRange(transferred), quotaRemaining);
            }
        }

        // Called for Duplex and Output
        public bool ProcessTransferred(SequenceRangeCollection ranges, int quotaRemaining)
        {
            if (ranges.Count == 0)
            {
                return false;
            }

            lock (ThisLock)
            {
                if (_closed)
                {
                    return false;
                }

                bool send = false;

                for (int rangeIndex = 0; rangeIndex < ranges.Count; rangeIndex++)
                {
                    if (ProcessTransferred(ranges[rangeIndex], quotaRemaining))
                    {
                        send = true;
                    }
                }

                return send;
            }
        }

        // It is necessary that ProcessAcknowledgement be called prior, as 
        // this method does not check for valid ack ranges.
        // This method returns true if the calling method should start sending retries 
        // obtained from GetMessageInfoForRetry.
        private bool ProcessTransferred(SequenceRange range, int quotaRemaining)
        {
            if (range.Upper < _windowStart)
            {
                if (range.Upper == _windowStart - 1 && (quotaRemaining != -1) && quotaRemaining > _quotaRemaining)
                    _quotaRemaining = quotaRemaining - Math.Min(_windowSize, _window.Count);

                return false;
            }
            else if (range.Lower <= _windowStart)
            {
                bool send = false;

                _retryTimer.Cancel();

                long slide = range.Upper - _windowStart + 1;

                // For Request Reply: Requests are transferred 1 at a time, (i.e. when the reply comes back).
                // The TransmissionStrategy only removes messages if the window start is removed.
                // Because of this, RequestReply messages transferred out of order will cause many, many retries.
                // To avoid extraneous retries we mark each message transferred, and we remove our virtual slide.
                if (slide == 1)
                {
                    for (int i = 1; i < _window.Count; i++)
                    {
                        if (_window.GetTransferred(i))
                        {
                            slide++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                long now = Now;
                long oldWindowEnd = _windowStart + _windowSize;

                for (int i = 0; i < (int)slide; i++)
                    UpdateStats(now, _window.GetLastAttemptTime(i));

                if (quotaRemaining != -1)
                {
                    int inFlightAfterAck = Math.Min(_windowSize, _window.Count) - (int)slide;
                    _quotaRemaining = quotaRemaining - Math.Max(0, inFlightAfterAck);
                }

                _window.Remove((int)slide);

                _windowStart += slide;

                int sendBeginIndex;
                if (_windowSize <= _slowStartThreshold)
                {
                    _windowSize = Math.Min(_maxWindowSize, Math.Min(_slowStartThreshold + 1, _windowSize + (int)slide));

                    if (!_startup)
                        sendBeginIndex = 0;
                    else
                        sendBeginIndex = Math.Max(0, (int)oldWindowEnd - (int)_windowStart);
                }
                else
                {
                    _congestionControlModeAcks += (int)slide;

                    // EXPERIMENTAL, needs optimizing ///
                    int segmentSize = Math.Max(1, (_lossWindowSize - _slowStartThreshold) / 8);
                    int windowGrowthAckThreshold = ((_windowSize - _slowStartThreshold) * _windowSize) / segmentSize;

                    if (_congestionControlModeAcks > windowGrowthAckThreshold)
                    {
                        _congestionControlModeAcks = 0;
                        _windowSize = Math.Min(_maxWindowSize, _windowSize + 1);
                    }

                    sendBeginIndex = Math.Max(0, (int)oldWindowEnd - (int)_windowStart);
                }

                int sendEndIndex = Math.Min(_windowSize, _window.Count);

                if (sendBeginIndex < sendEndIndex)
                {
                    send = (_retransmissionWindow.Count == 0);

                    for (int i = sendBeginIndex; i < _windowSize && i < _window.Count; i++)
                    {
                        long sequenceNumber = _windowStart + i;

                        if (!_window.GetTransferred(i) && !_retransmissionWindow.Contains(sequenceNumber))
                        {
                            _window.RecordRetry(i, Now);
                            _retransmissionWindow.Add(sequenceNumber);
                        }
                    }
                }

                if (_window.Count > 0)
                {
                    _retryTimer.Set(Timeout);
                }

                return send;
            }
            else
            {
                for (long i = range.Lower; i <= range.Upper; i++)
                {
                    _window.SetTransferred((int)(i - _windowStart));
                }
            }

            return false;
        }

        private bool RemoveAdder(IQueueAdder adder)
        {
            lock (ThisLock)
            {
                if (_closed)
                    return false;

                bool removed = false;
                for (int i = 0; i < _waitQueue.Count; i++)
                {
                    IQueueAdder current = _waitQueue.Dequeue();

                    if (ReferenceEquals(adder, current))
                    {
                        removed = true;
                    }
                    else
                    {
                        _waitQueue.Enqueue(current);
                    }
                }
                return removed;
            }
        }

        private void ThrowIfRollover()
        {
            if (_windowStart + _window.Count + _waitQueue.Count == long.MaxValue)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageNumberRolloverFault(_id).CreateException());
            }
        }

        private void UpdateStats(long now, long lastAttemptTime)
        {
            now = Math.Max(now, lastAttemptTime);
            long measuredRtt = now - lastAttemptTime;
            long error = measuredRtt - _meanRtt;
            _serrRtt = Math.Min(_serrRtt + ((Math.Abs(error) - _serrRtt) >> Constants.Gain), Constants.MaxSerrRtt);
            _meanRtt = Math.Min(_meanRtt + (error >> Constants.Gain), Constants.MaxMeanRtt);
            _timeout = Math.Max(((200 << Constants.TimeMultiplier) * 2) + _meanRtt, _meanRtt + (_serrRtt << Constants.ChebychevFactor));
        }

        private static class Constants
        {
            // Used to adjust the timeout calculation, according to Chebychev's theorem,
            // to fit ~98% of actual rtt's within our timeout.
            public const int ChebychevFactor = 2;

            // Gain of 0.125 (1/8). Shift right by 3 to apply the gain to a term.
            public const int Gain = 3;

            // 1ms == 128 of our time units. Shift left by 7 to perform the multiplication.
            public const int TimeMultiplier = 7;

            // These guarantee no overflows when calculating timeout.
            public const long MaxMeanRtt = long.MaxValue / 3;
            public const long MaxSerrRtt = MaxMeanRtt / 2;
        }

        // TODO: Investigate removing Complete1() as I believe it was only needed when using APM based waiting
        private interface IQueueAdder
        {
            void Abort(CommunicationObject communicationObject);
            void Fault(CommunicationObject communicationObject);
            void Complete0();
            void Complete1();
        }

        private class SlidingWindow
        {
            private readonly TransmissionInfo[] _buffer;
            private int _head = 0;
            private int _tail = 0;
            private readonly int _maxSize;

            public SlidingWindow(int maxSize)
            {
                _maxSize = maxSize + 1;
                _buffer = new TransmissionInfo[_maxSize];
            }

            public int Count
            {
                get
                {
                    if (_tail >= _head)
                        return (_tail - _head);
                    else
                        return (_tail - _head + _maxSize);
                }
            }

            public int TransferredCount
            {
                get
                {
                    return Count == 0 ? 0 : GetTransferredInRangeCount(0, Count - 1);
                }
            }

            public void Add(Message message, long addTime, object state)
            {
                Fx.AssertAndThrow(Count < (_maxSize - 1), "The caller is not allowed to add messages beyond the sliding window's maximum size.");

                _buffer[_tail] = new TransmissionInfo(message, addTime, state);
                _tail = (_tail + 1) % _maxSize;
            }

            private void AssertIndex(int index)
            {
                Fx.AssertAndThrow(index < Count, "Argument index must be less than Count.");
                Fx.AssertAndThrow(index >= 0, "Argument index must be positive.");
            }

            public void Close()
            {
                Remove(Count);
            }

            public long GetLastAttemptTime(int index)
            {
                AssertIndex(index);
                return _buffer[(_head + index) % _maxSize].LastAttemptTime;
            }

            public Message GetMessage(int index)
            {
                AssertIndex(index);
                if (!_buffer[(_head + index) % _maxSize].Transferred)
                    return _buffer[(_head + index) % _maxSize].Buffer.CreateMessage();
                else
                    return null;
            }

            public int GetRetryCount(int index)
            {
                AssertIndex(index);
                return _buffer[(_head + index) % _maxSize].RetryCount;
            }

            public object GetState(int index)
            {
                AssertIndex(index);
                return _buffer[(_head + index) % _maxSize].State;
            }

            public bool GetTransferred(int index)
            {
                AssertIndex(index);
                return _buffer[(_head + index) % _maxSize].Transferred;
            }

            public int GetTransferredInRangeCount(int beginIndex, int endIndex)
            {
                Fx.AssertAndThrow(beginIndex >= 0, "Argument beginIndex cannot be negative.");
                Fx.AssertAndThrow(endIndex < Count, "Argument endIndex cannot be greater than Count.");
                Fx.AssertAndThrow(endIndex >= beginIndex, "Argument endIndex cannot be less than argument beginIndex.");

                int result = 0;

                for (int index = beginIndex; index <= endIndex; index++)
                {
                    if (_buffer[(_head + index) % _maxSize].Transferred)
                        result++;
                }

                return result;
            }

            public int RecordRetry(int index, long retryTime)
            {
                AssertIndex(index);
                _buffer[(_head + index) % _maxSize].LastAttemptTime = retryTime;

                return ++_buffer[(_head + index) % _maxSize].RetryCount;
            }

            public void Remove(int count)
            {
                Fx.Assert(count <= Count, "Cannot remove more messages than the window's Count.");

                while (count-- > 0)
                {
                    _buffer[_head].Buffer.Close();
                    _buffer[_head].Buffer = null;
                    _head = (_head + 1) % _maxSize;
                }
            }

            public void SetTransferred(int index)
            {
                AssertIndex(index);
                _buffer[(_head + index) % _maxSize].Transferred = true;
            }

            private struct TransmissionInfo
            {
                internal MessageBuffer Buffer;
                internal long LastAttemptTime;
                internal int RetryCount;
                internal object State;
                internal bool Transferred;

                public TransmissionInfo(Message message, long lastAttemptTime, object state)
                {
                    Buffer = message.CreateBufferedCopy(int.MaxValue);
                    LastAttemptTime = lastAttemptTime;
                    RetryCount = 0;
                    State = state;
                    Transferred = false;
                }
            }
        }

        private class WaitQueueAdder : IQueueAdder
        {
            private readonly TaskCompletionSource<object> _taskCompletionSource = new TaskCompletionSource<object>();
            private Exception _exception;
            private readonly bool _isLast;
            private MessageAttemptInfo _attemptInfo = default;
            private readonly TransmissionStrategy _strategy;

            public WaitQueueAdder(TransmissionStrategy strategy, Message message, bool isLast, object state)
            {
                _strategy = strategy;
                _isLast = isLast;
                _attemptInfo = new MessageAttemptInfo(message, 0, 0, state);
            }

            public void Abort(CommunicationObject communicationObject)
            {
                _exception = communicationObject.CreateClosedException();
                _taskCompletionSource.TrySetResult(null);
            }

            public void Complete0()
            {
                _attemptInfo = _strategy.AddToWindow(_attemptInfo.Message, _isLast, _attemptInfo.State);
                _taskCompletionSource.TrySetResult(null);
            }

            public void Complete1()
            {
            }

            public void Fault(CommunicationObject communicationObject)
            {
                _exception = communicationObject.GetTerminalException();
                _taskCompletionSource.TrySetResult(null);
            }

            public async Task<MessageAttemptInfo> WaitAsync(CancellationToken token)  
            {
                if (!await _taskCompletionSource.Task.WaitWithCancellationAsync(token))
                {
                    if (_strategy.RemoveAdder(this) && _exception == null)
                        _exception = new TimeoutException(SR.Format(SR.TimeoutOnAddToWindow, TimeoutHelper.GetOriginalTimeout(token)));
                }

                if (_exception != null)
                {
                    _attemptInfo.Message.Close();
                    // We timed out, make sure nothing else is waiting on the TCS
                    _taskCompletionSource.TrySetResult(null);
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(_exception);
                }

                // This is safe because, Abort, Complete0, Fault, and RemoveAdder all occur under 
                // the TransmissionStrategy's lock and RemoveAdder ensures that the 
                // TransmissionStrategy will never call into this object again.
                _taskCompletionSource.TrySetResult(null);
                return _attemptInfo;
            }
        }
    }
}
