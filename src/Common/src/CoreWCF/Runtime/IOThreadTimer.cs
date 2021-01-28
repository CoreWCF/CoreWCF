// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.Contracts;
using System.Threading;

namespace CoreWCF.Runtime
{
    // IOThreadTimer has several characterstics that are important for performance:
    // - Timers that expire benefit from being scheduled to run on IO threads using IOThreadScheduler.Schedule.
    // - The timer "waiter" thread thread is only allocated if there are set timers.
    // - The timer waiter thread itself is an IO thread, which allows it to go away if there is no need for it,
    //   and allows it to be reused for other purposes.
    // - After the timer count goes to zero, the timer waiter thread remains active for a bounded amount
    //   of time to wait for additional timers to be set.
    // - Timers are stored in an array-based priority queue to reduce the amount of time spent in updates, and
    //   to always provide O(1) access to the minimum timer (the first one that will expire).
    // - The standard textbook priority queue data structure is extended to allow efficient Delete in addition to 
    //   DeleteMin for efficient handling of canceled timers.
    // - Timers that are typically set, then immediately canceled (such as a retry timer, 
    //   or a flush timer), are tracked separately from more stable timers, to avoid having 
    //   to update the waitable timer in the typical case when a timer is canceled.  Whether 
    //   a timer instance follows this pattern is specified when the timer is constructed.
    // - Extending a timer by a configurable time delta (maxSkew) does not involve updating the
    //   waitable timer, or taking a lock.
    // - Timer instances are relatively cheap.  They share "heavy" resources like the waiter thread and 
    //   waitable timer handle.
    // - Setting or canceling a timer does not typically involve any allocations.

    internal class IOThreadTimer
    {
        private const int maxSkewInMillisecondsDefault = 100;
        private Action<object> _callback;
        private object _callbackState;
        private long _dueTime;
        private int _index;
        private readonly long _maxSkew;
        private readonly TimerGroup _timerGroup;

        public IOThreadTimer(Action<object> callback, object callbackState, bool isTypicallyCanceledShortlyAfterBeingSet)
            : this(callback, callbackState, isTypicallyCanceledShortlyAfterBeingSet, maxSkewInMillisecondsDefault)
        {
        }

        public IOThreadTimer(Action<object> callback, object callbackState, bool isTypicallyCanceledShortlyAfterBeingSet, int maxSkewInMilliseconds)
        {
            _callback = callback;
            _callbackState = callbackState;
            _maxSkew = Ticks.FromMilliseconds(maxSkewInMilliseconds);
            _timerGroup =
                (isTypicallyCanceledShortlyAfterBeingSet ? TimerManager.Value.VolatileTimerGroup : TimerManager.Value.StableTimerGroup);
        }

        public bool Cancel()
        {
            return TimerManager.Value.Cancel(this);
        }

        public void Set(TimeSpan timeFromNow)
        {
            if (timeFromNow != TimeSpan.MaxValue)
            {
                SetAt(Ticks.Add(Ticks.Now, Ticks.FromTimeSpan(timeFromNow)));
            }
        }

        public void Set(int millisecondsFromNow)
        {
            SetAt(Ticks.Add(Ticks.Now, Ticks.FromMilliseconds(millisecondsFromNow)));
        }

        public void SetAt(long dueTime)
        {
            TimerManager.Value.Set(this, dueTime);
        }

        protected void Reinitialize(Action<object> callback, object callbackState)
        {
            _callback = callback;
            _callbackState = callbackState;
        }

        internal static void KillTimers()
        {
            TimerManager.Value.Kill();
        }

        private class TimerManager
        {
            private const long maxTimeToWaitForMoreTimers = 1000 * TimeSpan.TicksPerMillisecond;
            private static readonly TimerManager s_value = new TimerManager();
            private readonly Action<object> _onWaitCallback;
            private readonly TimerGroup _volatileTimerGroup;
            private readonly WaitableTimer[] _waitableTimers;
            private bool _waitScheduled;

            public TimerManager()
            {
                _onWaitCallback = new Action<object>(OnWaitCallback);
                StableTimerGroup = new TimerGroup();
                _volatileTimerGroup = new TimerGroup();
                _waitableTimers = new WaitableTimer[] { StableTimerGroup.WaitableTimer, _volatileTimerGroup.WaitableTimer };
            }

            private object ThisLock
            {
                get { return this; }
            }

            public static TimerManager Value
            {
                get
                {
                    return TimerManager.s_value;
                }
            }

            public TimerGroup StableTimerGroup { get; private set; }
            public TimerGroup VolatileTimerGroup
            {
                get
                {
                    return _volatileTimerGroup;
                }
            }

            internal void Kill()
            {
                StableTimerGroup.WaitableTimer.Kill();
                _volatileTimerGroup.WaitableTimer.Kill();
            }

            public void Set(IOThreadTimer timer, long dueTime)
            {
                long timeDiff = dueTime - timer._dueTime;
                if (timeDiff < 0)
                {
                    timeDiff = -timeDiff;
                }

                if (timeDiff > timer._maxSkew)
                {
                    lock (ThisLock)
                    {
                        TimerGroup timerGroup = timer._timerGroup;
                        TimerQueue timerQueue = timerGroup.TimerQueue;

                        if (timer._index > 0)
                        {
                            if (timerQueue.UpdateTimer(timer, dueTime))
                            {
                                UpdateWaitableTimer(timerGroup);
                            }
                        }
                        else
                        {
                            if (timerQueue.InsertTimer(timer, dueTime))
                            {
                                UpdateWaitableTimer(timerGroup);

                                if (timerQueue.Count == 1)
                                {
                                    EnsureWaitScheduled();
                                }
                            }
                        }
                    }
                }
            }

            public bool Cancel(IOThreadTimer timer)
            {
                lock (ThisLock)
                {
                    if (timer._index > 0)
                    {
                        TimerGroup timerGroup = timer._timerGroup;
                        TimerQueue timerQueue = timerGroup.TimerQueue;

                        timerQueue.DeleteTimer(timer);

                        if (timerQueue.Count > 0)
                        {
                            UpdateWaitableTimer(timerGroup);
                        }
                        else
                        {
                            TimerGroup otherTimerGroup = GetOtherTimerGroup(timerGroup);
                            if (otherTimerGroup.TimerQueue.Count == 0)
                            {
                                long now = Ticks.Now;
                                long thisGroupRemainingTime = timerGroup.WaitableTimer.DueTime - now;
                                long otherGroupRemainingTime = otherTimerGroup.WaitableTimer.DueTime - now;
                                if (thisGroupRemainingTime > maxTimeToWaitForMoreTimers &&
                                    otherGroupRemainingTime > maxTimeToWaitForMoreTimers)
                                {
                                    timerGroup.WaitableTimer.Set(Ticks.Add(now, maxTimeToWaitForMoreTimers));
                                }
                            }
                        }

                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            private void EnsureWaitScheduled()
            {
                if (!_waitScheduled)
                {
                    ScheduleWait();
                }
            }

            private TimerGroup GetOtherTimerGroup(TimerGroup timerGroup)
            {
                if (object.ReferenceEquals(timerGroup, _volatileTimerGroup))
                {
                    return StableTimerGroup;
                }
                else
                {
                    return _volatileTimerGroup;
                }
            }

            private void OnWaitCallback(object state)
            {
                WaitableTimer.WaitAny(_waitableTimers);
                long now = Ticks.Now;
                lock (ThisLock)
                {
                    _waitScheduled = false;
                    ScheduleElapsedTimers(now);
                    ReactivateWaitableTimers();
                    ScheduleWaitIfAnyTimersLeft();
                }
            }

            private void ReactivateWaitableTimers()
            {
                ReactivateWaitableTimer(StableTimerGroup);
                ReactivateWaitableTimer(_volatileTimerGroup);
            }

            private void ReactivateWaitableTimer(TimerGroup timerGroup)
            {
                TimerQueue timerQueue = timerGroup.TimerQueue;

                if (timerGroup.WaitableTimer.dead)
                {
                    return;
                }

                if (timerQueue.Count > 0)
                {
                    timerGroup.WaitableTimer.Set(timerQueue.MinTimer._dueTime);
                }
                else
                {
                    timerGroup.WaitableTimer.Set(long.MaxValue);
                }
            }

            private void ScheduleElapsedTimers(long now)
            {
                ScheduleElapsedTimers(StableTimerGroup, now);
                ScheduleElapsedTimers(_volatileTimerGroup, now);
            }

            private void ScheduleElapsedTimers(TimerGroup timerGroup, long now)
            {
                TimerQueue timerQueue = timerGroup.TimerQueue;
                while (timerQueue.Count > 0)
                {
                    IOThreadTimer timer = timerQueue.MinTimer;
                    long timeDiff = timer._dueTime - now;
                    if (timeDiff <= timer._maxSkew)
                    {
                        timerQueue.DeleteMinTimer();
                        ActionItem.Schedule(timer._callback, timer._callbackState);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            private void ScheduleWait()
            {
                ActionItem.Schedule(_onWaitCallback, null);
                _waitScheduled = true;
            }

            private void ScheduleWaitIfAnyTimersLeft()
            {
                if (StableTimerGroup.WaitableTimer.dead &&
                    _volatileTimerGroup.WaitableTimer.dead)
                {
                    return;
                }

                if (StableTimerGroup.TimerQueue.Count > 0 ||
                    _volatileTimerGroup.TimerQueue.Count > 0)
                {
                    ScheduleWait();
                }
            }

            private void UpdateWaitableTimer(TimerGroup timerGroup)
            {
                WaitableTimer waitableTimer = timerGroup.WaitableTimer;
                IOThreadTimer minTimer = timerGroup.TimerQueue.MinTimer;
                long timeDiff = waitableTimer.DueTime - minTimer._dueTime;
                if (timeDiff < 0)
                {
                    timeDiff = -timeDiff;
                }
                if (timeDiff > minTimer._maxSkew)
                {
                    waitableTimer.Set(minTimer._dueTime);
                }
            }
        }

        private class TimerGroup
        {
            private readonly WaitableTimer _waitableTimer;

            public TimerGroup()
            {
                _waitableTimer = new WaitableTimer();
                TimerQueue = new TimerQueue();
            }

            public TimerQueue TimerQueue { get; private set; }
            public WaitableTimer WaitableTimer
            {
                get
                {
                    return _waitableTimer;
                }
            }
        }

        private class TimerQueue
        {
            private IOThreadTimer[] _timers;

            public TimerQueue()
            {
                _timers = new IOThreadTimer[4];
            }

            public int Count { get; private set; }

            public IOThreadTimer MinTimer
            {
                get
                {
                    Fx.Assert(Count > 0, "Should have at least one timer in our queue.");
                    return _timers[1];
                }
            }
            public void DeleteMinTimer()
            {
                IOThreadTimer minTimer = MinTimer;
                DeleteMinTimerCore();
                minTimer._index = 0;
                minTimer._dueTime = 0;
            }
            public void DeleteTimer(IOThreadTimer timer)
            {
                int index = timer._index;

                Fx.Assert(index > 0, "");
                Fx.Assert(index <= Count, "");

                IOThreadTimer[] timers = _timers;

                for (; ; )
                {
                    int parentIndex = index / 2;

                    if (parentIndex >= 1)
                    {
                        IOThreadTimer parentTimer = timers[parentIndex];
                        timers[index] = parentTimer;
                        parentTimer._index = index;
                    }
                    else
                    {
                        break;
                    }

                    index = parentIndex;
                }

                timer._index = 0;
                timer._dueTime = 0;
                timers[1] = null;
                DeleteMinTimerCore();
            }

            public bool InsertTimer(IOThreadTimer timer, long dueTime)
            {
                Fx.Assert(timer._index == 0, "Timer should not have an index.");

                IOThreadTimer[] timers = _timers;

                int index = Count + 1;

                if (index == timers.Length)
                {
                    timers = new IOThreadTimer[timers.Length * 2];
                    Array.Copy(_timers, timers, _timers.Length);
                    _timers = timers;
                }

                Count = index;

                if (index > 1)
                {
                    for (; ; )
                    {
                        int parentIndex = index / 2;

                        if (parentIndex == 0)
                        {
                            break;
                        }

                        IOThreadTimer parent = timers[parentIndex];

                        if (parent._dueTime > dueTime)
                        {
                            timers[index] = parent;
                            parent._index = index;
                            index = parentIndex;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                timers[index] = timer;
                timer._index = index;
                timer._dueTime = dueTime;
                return index == 1;
            }
            public bool UpdateTimer(IOThreadTimer timer, long dueTime)
            {
                int index = timer._index;

                IOThreadTimer[] timers = _timers;
                int count = Count;

                Fx.Assert(index > 0, "");
                Fx.Assert(index <= count, "");

                int parentIndex = index / 2;
                if (parentIndex == 0 ||
                    timers[parentIndex]._dueTime <= dueTime)
                {
                    int leftChildIndex = index * 2;
                    if (leftChildIndex > count ||
                        timers[leftChildIndex]._dueTime >= dueTime)
                    {
                        int rightChildIndex = leftChildIndex + 1;
                        if (rightChildIndex > count ||
                            timers[rightChildIndex]._dueTime >= dueTime)
                        {
                            timer._dueTime = dueTime;
                            return index == 1;
                        }
                    }
                }

                DeleteTimer(timer);
                InsertTimer(timer, dueTime);
                return true;
            }

            private void DeleteMinTimerCore()
            {
                int count = Count;

                if (count == 1)
                {
                    Count = 0;
                    _timers[1] = null;
                }
                else
                {
                    IOThreadTimer[] timers = _timers;
                    IOThreadTimer lastTimer = timers[count];
                    Count = --count;

                    int index = 1;
                    for (; ; )
                    {
                        int leftChildIndex = index * 2;

                        if (leftChildIndex > count)
                        {
                            break;
                        }

                        int childIndex;
                        IOThreadTimer child;

                        if (leftChildIndex < count)
                        {
                            IOThreadTimer leftChild = timers[leftChildIndex];
                            int rightChildIndex = leftChildIndex + 1;
                            IOThreadTimer rightChild = timers[rightChildIndex];

                            if (rightChild._dueTime < leftChild._dueTime)
                            {
                                child = rightChild;
                                childIndex = rightChildIndex;
                            }
                            else
                            {
                                child = leftChild;
                                childIndex = leftChildIndex;
                            }
                        }
                        else
                        {
                            childIndex = leftChildIndex;
                            child = timers[childIndex];
                        }

                        if (lastTimer._dueTime > child._dueTime)
                        {
                            timers[index] = child;
                            child._index = index;
                        }
                        else
                        {
                            break;
                        }

                        index = childIndex;

                        if (leftChildIndex >= count)
                        {
                            break;
                        }
                    }

                    timers[index] = lastTimer;
                    lastTimer._index = index;
                    timers[count + 1] = null;
                }
            }
        }

        public class WaitableTimer : EventWaitHandle
        {
            public bool dead;

            public WaitableTimer() : base(false, EventResetMode.AutoReset)
            {
            }

            public long DueTime { get; private set; }

            public void Set(long dueTime)
            {
                if (dueTime < DueTime)
                {
                    DueTime = dueTime;
                    Set(); // We might be waiting on a later time so nudge it to reworkout the time
                }
                else
                {
                    DueTime = dueTime;
                }
            }

            public void Kill()
            {
                dead = true;
                Set();
            }

            public static int WaitAny(WaitableTimer[] waitableTimers)
            {
                do
                {
                    var earliestDueTime = waitableTimers[0].DueTime;
                    for (int i = 1; i < waitableTimers.Length; i++)
                    {
                        if (waitableTimers[i].dead)
                        {
                            return 0;
                        }

                        if (waitableTimers[i].DueTime < earliestDueTime)
                        {
                            earliestDueTime = waitableTimers[i].DueTime;
                        }

                        waitableTimers[i].Reset();
                    }

                    var waitDurationInMillis = (earliestDueTime - DateTime.UtcNow.Ticks) / TimeSpan.TicksPerMillisecond;
                    if (waitDurationInMillis < 0) // Already passed the due time
                    {
                        return 0;
                    }

                    Contract.Assert(waitDurationInMillis < int.MaxValue, "Waiting for longer than is possible");
                    WaitHandle.WaitAny(waitableTimers, (int)waitDurationInMillis);
                    // Always loop around and check wait time again as values might have changed.
                } while (true);
            }
        }
    }
}
