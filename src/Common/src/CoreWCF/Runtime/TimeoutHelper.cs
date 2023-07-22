// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace CoreWCF.Runtime
{
    internal struct TimeoutHelper
    {
        // This is a brief plan on how to recover original timeout from CancellationToken
        // The coalescing is needed to prevent hammering at the timer queue otherwise we
        // get a lot of contention on it. It's really only the timer that needs to be coalesced.
        // 1. Change the coalescing to keep track of IOThreadTimer object
        // 2. Create a class which derives from/encapsulates an IOThreadTimer which can be used to
        //    register CancellationTokenSource objects to call cancel when the timer fires. This
        //    is the class that we coalesce.
        // 3. Create RecoverableTimeoutCancellationTokenSource which derives from Cancellation token source.
        //    It has the following behavior:
        //    a. Takes the derived IOThreadTimer from (2) in the constructor and registers cancelling itself
        //       when the timer fires.
        //    b. Takes the requested timeout in the constructor and saves it.
        //    c. Override the GetHashCode method to return a value representing the original timeout value.
        //       CancellationToken defers to the owning CancellationTokenSource to implement GetHashCode()
        // 4. Add a TimeoutHelper method which returns a TimeSpan from a CancellationToken. It queries the
        //    GetHashCode method to get the original requested timeout.
        public static readonly TimeSpan MaxWait = TimeSpan.FromMilliseconds(int.MaxValue);
        private static readonly CancellationToken s_precancelledToken = new CancellationToken(true);

        private bool _cancellationTokenInitialized;
        private bool _deadlineSet;

        private CancellationToken _cancellationToken;
        private DateTime _deadline;
        private TimeSpan _originalTimeout;

        public TimeoutHelper(TimeSpan timeout)
        {
            Fx.Assert(timeout >= TimeSpan.Zero || timeout == Timeout.InfiniteTimeSpan,
                $"timeout must be non-negative or {Timeout.InfiniteTimeSpan}");

            _cancellationToken = default;
            _cancellationTokenInitialized = false;
            _originalTimeout = timeout;
            _deadline = DateTime.MaxValue;
            _deadlineSet = (timeout == TimeSpan.MaxValue || timeout == Timeout.InfiniteTimeSpan);
        }

        // This is cheaper than using new TimeoutHelper(timeout).GetCancellationToken()
        // as it doesn't require calculating a deadline and then requesting the remaining time.
        public static CancellationToken GetCancellationToken(TimeSpan timeout)
        {
            if (timeout >= MaxWait || timeout == Timeout.InfiniteTimeSpan)
            {
                return default;
            }
            else if (timeout > TimeSpan.Zero)
            {
                return TimeoutTokenSource.FromTimeout((int)timeout.TotalMilliseconds);
            }
            else
            {
                return s_precancelledToken;
            }
        }

        public CancellationToken GetCancellationToken()
        {
            if (!_cancellationTokenInitialized)
            {
                TimeSpan timeout = RemainingTime();
                if (timeout >= MaxWait || timeout == Timeout.InfiniteTimeSpan)
                {
                    _cancellationToken = CancellationToken.None;
                }
                else if (timeout > TimeSpan.Zero)
                {
                    _cancellationToken = TimeoutTokenSource.FromTimeout((int)timeout.TotalMilliseconds);
                }
                else
                {
                    _cancellationToken = s_precancelledToken;
                }
                _cancellationTokenInitialized = true;
            }

            return _cancellationToken;
        }


        public TimeSpan OriginalTimeout
        {
            get { return _originalTimeout; }
        }

        public static bool IsTooLarge(TimeSpan timeout)
        {
            return (timeout > MaxWait) && (timeout != TimeSpan.MaxValue);
        }

        public static TimeSpan FromMilliseconds(int milliseconds)
        {
            if (milliseconds == Timeout.Infinite)
            {
                return TimeSpan.MaxValue;
            }
            else
            {
                return TimeSpan.FromMilliseconds(milliseconds);
            }
        }

        public static int ToMilliseconds(TimeSpan timeout)
        {
            if (timeout == TimeSpan.MaxValue)
            {
                return Timeout.Infinite;
            }
            else
            {
                long ticks = Ticks.FromTimeSpan(timeout);
                if (ticks / TimeSpan.TicksPerMillisecond > int.MaxValue)
                {
                    return int.MaxValue;
                }
                return Ticks.ToMilliseconds(ticks);
            }
        }

        public static TimeSpan Min(TimeSpan val1, TimeSpan val2)
        {
            if (val1 > val2)
            {
                return val2;
            }
            else
            {
                return val1;
            }
        }

        public static TimeSpan Add(TimeSpan timeout1, TimeSpan timeout2)
        {
            return Ticks.ToTimeSpan(Ticks.Add(Ticks.FromTimeSpan(timeout1), Ticks.FromTimeSpan(timeout2)));
        }

        public static DateTime Add(DateTime time, TimeSpan timeout)
        {
            if (timeout >= TimeSpan.Zero && DateTime.MaxValue - time <= timeout)
            {
                return DateTime.MaxValue;
            }
            if (timeout <= TimeSpan.Zero && DateTime.MinValue - time >= timeout)
            {
                return DateTime.MinValue;
            }
            return time + timeout;
        }

        public static DateTime Subtract(DateTime time, TimeSpan timeout)
        {
            return Add(time, TimeSpan.Zero - timeout);
        }

        public static TimeSpan Divide(TimeSpan timeout, int factor)
        {
            if (timeout == TimeSpan.MaxValue)
            {
                return TimeSpan.MaxValue;
            }

            return Ticks.ToTimeSpan((Ticks.FromTimeSpan(timeout) / factor) + 1);
        }

        public TimeSpan RemainingTime()
        {
            if (!_deadlineSet)
            {
                SetDeadline();
                return _originalTimeout;
            }
            else if (_deadline == DateTime.MaxValue)
            {
                return TimeSpan.MaxValue;
            }
            else
            {
                TimeSpan remaining = _deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    return TimeSpan.Zero;
                }
                else
                {
                    return remaining;
                }
            }
        }

        public TimeSpan ElapsedTime()
        {
            return _originalTimeout - RemainingTime();
        }

        private void SetDeadline()
        {
            Fx.Assert(!_deadlineSet, "TimeoutHelper deadline set twice.");
            _deadline = DateTime.UtcNow + _originalTimeout;
            _deadlineSet = true;
        }

        public static TimeSpan GetOriginalTimeout(CancellationToken token)
        {
            return RecoverableTimeoutCancellationTokenSource.GetOriginalTimeout(token);
        }

        public static void ThrowIfNegativeArgument(TimeSpan timeout)
        {
            ThrowIfNegativeArgument(timeout, "timeout");
        }

        public static void ThrowIfNegativeArgument(TimeSpan timeout, string argumentName)
        {
            if (timeout < TimeSpan.Zero)
            {
                throw Fx.Exception.ArgumentOutOfRange(argumentName, timeout, SRCommon.Format(SRCommon.TimeoutMustBeNonNegative, argumentName, timeout));
            }
        }

        public static void ThrowIfNonPositiveArgument(TimeSpan timeout)
        {
            ThrowIfNonPositiveArgument(timeout, "timeout");
        }

        public static void ThrowIfNonPositiveArgument(TimeSpan timeout, string argumentName)
        {
            if (timeout <= TimeSpan.Zero)
            {
                throw Fx.Exception.ArgumentOutOfRange(argumentName, timeout, SRCommon.Format(SRCommon.TimeoutMustBePositive, argumentName, timeout));
            }
        }

        public static bool WaitOne(WaitHandle waitHandle, TimeSpan timeout)
        {
            ThrowIfNegativeArgument(timeout);
            if (timeout == TimeSpan.MaxValue)
            {
                waitHandle.WaitOne();
                return true;
            }
            else
            {
                // http://msdn.microsoft.com/en-us/library/85bbbxt9(v=vs.110).aspx 
                // with exitContext was used in Desktop which is not supported in Net Native or CoreClr
                return waitHandle.WaitOne(timeout);
            }
        }

        internal static TimeoutException CreateEnterTimedOutException(TimeSpan timeout)
        {
            return new TimeoutException(SRCommon.Format(SRCommon.LockTimeoutExceptionMessage, timeout));
        }
    }

    /// <summary>
    /// This class coalesces timeout tokens because cancelation tokens with timeouts are more expensive to expose.
    /// Disposing too many such tokens will cause thread contentions in high throughput scenario.
    ///
    /// Tokens with target cancelation time 15ms apart would resolve to the same instance.
    /// </summary>
    internal static class TimeoutTokenSource
    {
        /// <summary>
        /// These are constants use to calculate timeout coalescing, for more description see method FromTimeoutAsync
        /// </summary>
        private const int CoalescingFactor = 15;
        private const int GranularityFactor = 2000;
        private const int SegmentationFactor = CoalescingFactor * GranularityFactor;

        private static readonly ConcurrentDictionary<long, CancellationTokenSourceIOThreadTimer> s_timerCache =
            new ConcurrentDictionary<long, CancellationTokenSourceIOThreadTimer>();

        private static readonly Action<object> s_deregisterTimer = (object state) =>
        {
            long targetTime = (long)state;
            s_timerCache.TryRemove(targetTime, out CancellationTokenSourceIOThreadTimer ignored);
        };

        public static CancellationToken FromTimeout(int millisecondsTimeout)
        {
            // Note that CancellationTokenSource constructor requires input to be >= -1,
            // restricting millisecondsTimeout to be >= -1 would enforce that
            if (millisecondsTimeout < -1)
            {
                throw new ArgumentOutOfRangeException("Invalid millisecondsTimeout value " + millisecondsTimeout);
            }

            // To prevent s_tokenCache growing too large, we have to adjust the granularity of the our coalesce depending
            // on the value of millisecondsTimeout. The coalescing span scales proportionally with millisecondsTimeout which
            // would guarantee constant s_tokenCache size in the case where similar millisecondsTimeout values are accepted.
            // If the method is given a wildly different millisecondsTimeout values all the time, the dictionary would still
            // only grow logarithmically with respect to the range of the input values

            uint currentTime = (uint)Environment.TickCount;
            long targetTime = millisecondsTimeout + currentTime;

            // Formula for our coalescing span:
            // Divide millisecondsTimeout by SegmentationFactor and take the highest bit and then multiply CoalescingFactor back
            int segmentValue = millisecondsTimeout / SegmentationFactor;
            int coalescingSpanMs = CoalescingFactor;
            while (segmentValue > 0)
            {
                segmentValue >>= 1;
                coalescingSpanMs <<= 1;
            }
            targetTime = ((targetTime + (coalescingSpanMs - 1)) / coalescingSpanMs) * coalescingSpanMs;

            if (!s_timerCache.TryGetValue(targetTime, out CancellationTokenSourceIOThreadTimer ctsTimer))
            {
                ctsTimer = new CancellationTokenSourceIOThreadTimer();

                // only a single thread may succeed adding its timer into the cache
                if (s_timerCache.TryAdd(targetTime, ctsTimer))
                {
                    // Clean up cache when timer fires
                    ctsTimer.SetCompletionCallback(s_deregisterTimer, targetTime);
                    ctsTimer.Set((int)(targetTime - currentTime));
                }
                else
                {
                    // for threads that failed when calling TryAdd, there should be one already in the cache
                    if (!s_timerCache.TryGetValue(targetTime, out ctsTimer))
                    {
                        // In unlikely scenario the timer has already fired, we would not find it in cache.
                        // In this case we would simply create a CTS which doesn't use the coalesced timer. 
                        var cts = new RecoverableTimeoutCancellationTokenSource(millisecondsTimeout);
                        cts.CancelAfter(millisecondsTimeout);
                        return cts.Token;
                    }
                }
            }

            var tokenSource = new RecoverableTimeoutCancellationTokenSource(millisecondsTimeout);
            ctsTimer.RegisterTokenSourceForCancellation(tokenSource);
            return tokenSource.Token;
        }
    }
}
