using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace CoreWCF.Runtime
{
    internal sealed class BackoffTimeoutHelper
    {
        readonly static int maxSkewMilliseconds = 15;
        readonly static long maxDriftTicks = maxSkewMilliseconds * 2 * TimeSpan.TicksPerMillisecond;
        readonly static TimeSpan defaultInitialWaitTime = TimeSpan.FromMilliseconds(1);
        readonly static TimeSpan defaultMaxWaitTime = TimeSpan.FromMinutes(1);

        DateTime deadline;
        TimeSpan maxWaitTime;
        TimeSpan waitTime;
        IOThreadTimer backoffTimer;
        Action<object> backoffCallback;
        object backoffState;
        Random random;
        TimeSpan originalTimeout;

        internal BackoffTimeoutHelper(TimeSpan timeout)
            : this(timeout, BackoffTimeoutHelper.defaultMaxWaitTime)
        {
        }

        internal BackoffTimeoutHelper(TimeSpan timeout, TimeSpan maxWaitTime)
            : this(timeout, maxWaitTime, BackoffTimeoutHelper.defaultInitialWaitTime)
        {
        }

        internal BackoffTimeoutHelper(TimeSpan timeout, TimeSpan maxWaitTime, TimeSpan initialWaitTime)
        {
            random = new Random(GetHashCode());
            this.maxWaitTime = maxWaitTime;
            originalTimeout = timeout;
            Reset(timeout, initialWaitTime);
        }

        public TimeSpan OriginalTimeout
        {
            get
            {
                return originalTimeout;
            }
        }

        void Reset(TimeSpan timeout, TimeSpan initialWaitTime)
        {
            if (timeout == TimeSpan.MaxValue)
            {
                deadline = DateTime.MaxValue;
            }
            else
            {
                deadline = DateTime.UtcNow + timeout;
            }
            waitTime = initialWaitTime;
        }

        public bool IsExpired()
        {
            if (deadline == DateTime.MaxValue)
            {
                return false;
            }
            else
            {
                return (DateTime.UtcNow >= deadline);
            }
        }

        public void WaitAndBackoff(Action<object> callback, object state)
        {
            if (backoffCallback != callback || backoffState != state)
            {
                if (backoffTimer != null)
                {
                    backoffTimer.Cancel();
                }
                backoffCallback = callback;
                backoffState = state;
                backoffTimer = new IOThreadTimer(callback, state, false, BackoffTimeoutHelper.maxSkewMilliseconds);
            }

            TimeSpan backoffTime = WaitTimeWithDrift();
            Backoff();
            backoffTimer.Set(backoffTime);
        }

        // TODO: Consider making Async
        public void WaitAndBackoff()
        {
            Thread.Sleep(WaitTimeWithDrift());
            Backoff();
        }

        TimeSpan WaitTimeWithDrift()
        {
            return Ticks.ToTimeSpan(Math.Max(
                Ticks.FromTimeSpan(BackoffTimeoutHelper.defaultInitialWaitTime),
                Ticks.Add(Ticks.FromTimeSpan(waitTime),
                    (long)(uint)random.Next() % (2 * BackoffTimeoutHelper.maxDriftTicks + 1) - BackoffTimeoutHelper.maxDriftTicks)));
        }

        void Backoff()
        {
            if (waitTime.Ticks >= (maxWaitTime.Ticks / 2))
            {
                waitTime = maxWaitTime;
            }
            else
            {
                waitTime = TimeSpan.FromTicks(waitTime.Ticks * 2);
            }

            if (deadline != DateTime.MaxValue)
            {
                TimeSpan remainingTime = deadline - DateTime.UtcNow;
                if (waitTime > remainingTime)
                {
                    waitTime = remainingTime;
                    if (waitTime < TimeSpan.Zero)
                    {
                        waitTime = TimeSpan.Zero;
                    }
                }
            }
        }
    }

}
