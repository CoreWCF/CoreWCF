// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;

namespace CoreWCF.Runtime
{
    internal sealed class BackoffTimeoutHelper
    {
        private static readonly int s_maxSkewMilliseconds = 15;
        private static readonly long s_maxDriftTicks = s_maxSkewMilliseconds * 2 * TimeSpan.TicksPerMillisecond;
        private static readonly TimeSpan s_defaultInitialWaitTime = TimeSpan.FromMilliseconds(1);
        private static readonly TimeSpan s_defaultMaxWaitTime = TimeSpan.FromMinutes(1);
        private DateTime _deadline;
        private TimeSpan _maxWaitTime;
        private TimeSpan _waitTime;
        private IOThreadTimer _backoffTimer;
        private Action<object> _backoffCallback;
        private object _backoffState;
        private readonly Random _random;
        private TimeSpan _originalTimeout;

        internal BackoffTimeoutHelper(TimeSpan timeout)
            : this(timeout, s_defaultMaxWaitTime)
        {
        }

        internal BackoffTimeoutHelper(TimeSpan timeout, TimeSpan maxWaitTime)
            : this(timeout, maxWaitTime, s_defaultInitialWaitTime)
        {
        }

        internal BackoffTimeoutHelper(TimeSpan timeout, TimeSpan maxWaitTime, TimeSpan initialWaitTime)
        {
            _random = new Random(GetHashCode());
            _maxWaitTime = maxWaitTime;
            _originalTimeout = timeout;
            Reset(timeout, initialWaitTime);
        }

        public TimeSpan OriginalTimeout
        {
            get
            {
                return _originalTimeout;
            }
        }

        private void Reset(TimeSpan timeout, TimeSpan initialWaitTime)
        {
            if (timeout == TimeSpan.MaxValue)
            {
                _deadline = DateTime.MaxValue;
            }
            else
            {
                _deadline = DateTime.UtcNow + timeout;
            }
            _waitTime = initialWaitTime;
        }

        public bool IsExpired()
        {
            if (_deadline == DateTime.MaxValue)
            {
                return false;
            }
            else
            {
                return (DateTime.UtcNow >= _deadline);
            }
        }

        public void WaitAndBackoff(Action<object> callback, object state)
        {
            if (_backoffCallback != callback || _backoffState != state)
            {
                if (_backoffTimer != null)
                {
                    _backoffTimer.Cancel();
                }
                _backoffCallback = callback;
                _backoffState = state;
                _backoffTimer = new IOThreadTimer(callback, state, false, s_maxSkewMilliseconds);
            }

            TimeSpan backoffTime = WaitTimeWithDrift();
            Backoff();
            _backoffTimer.Set(backoffTime);
        }

        // TODO: Consider making Async
        public void WaitAndBackoff()
        {
            Thread.Sleep(WaitTimeWithDrift());
            Backoff();
        }

        private TimeSpan WaitTimeWithDrift()
        {
            return Ticks.ToTimeSpan(Math.Max(
                Ticks.FromTimeSpan(s_defaultInitialWaitTime),
                Ticks.Add(Ticks.FromTimeSpan(_waitTime),
                    (long)(uint)_random.Next() % (2 * s_maxDriftTicks + 1) - s_maxDriftTicks)));
        }

        private void Backoff()
        {
            if (_waitTime.Ticks >= (_maxWaitTime.Ticks / 2))
            {
                _waitTime = _maxWaitTime;
            }
            else
            {
                _waitTime = TimeSpan.FromTicks(_waitTime.Ticks * 2);
            }

            if (_deadline != DateTime.MaxValue)
            {
                TimeSpan remainingTime = _deadline - DateTime.UtcNow;
                if (_waitTime > remainingTime)
                {
                    _waitTime = remainingTime;
                    if (_waitTime < TimeSpan.Zero)
                    {
                        _waitTime = TimeSpan.Zero;
                    }
                }
            }
        }
    }
}
