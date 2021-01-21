// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CoreWCF.Dispatcher
{
    internal sealed class FlowThrottle
    {
        private int _capacity;
        private int _count;
        private bool _warningIssued;
        private int _warningRestoreLimit;
        private object _mutex;
        // TODO: See if there's a way to pool resettable awaitables to remove allocation. Same in QuotaThrottle
        private Queue<TaskCompletionSource<object>> _waiters;
        private string _propertyName;
        private string _configName;
        private Action _acquired;
        private Action _released;
        private Action<int> _ratio;

        internal FlowThrottle(int capacity, string propertyName, string configName)
        {
            if (capacity <= 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxThrottleLimitMustBeGreaterThanZero0));

            _count = 0;
            _capacity = capacity;
            _mutex = new object();
            _waiters = new Queue<TaskCompletionSource<object>>();
            _propertyName = propertyName;
            _configName = configName;
            _warningRestoreLimit = (int)Math.Floor(0.7 * (double)capacity);
        }

        internal int Capacity
        {
            get { return _capacity; }
            set
            {
                if (value <= 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxThrottleLimitMustBeGreaterThanZero0));
                _capacity = value;
            }
        }

        internal async ValueTask AcquireAsync()
        {
            TaskCompletionSource<object> tcs = null;
            bool acquiredThrottle = true;

            lock (_mutex)
            {
                if (_count < _capacity)
                {
                    _count++;
                }
                else
                {
                    if (_waiters.Count == 0)
                    {
                        //if (TD.MessageThrottleExceededIsEnabled())
                        //{
                        //    if (!this.warningIssued)
                        //    {
                        //        TD.MessageThrottleExceeded(this.propertyName, this.capacity);
                        //        this.warningIssued = true;
                        //    }
                        //}
                        //if (DiagnosticUtility.ShouldTraceWarning)
                        //{
                        //    string traceMessage;
                        //    if (this.propertyName != null)
                        //    {
                        //        traceMessage = SR.GetString(SR.TraceCodeServiceThrottleLimitReached,
                        //                         this.propertyName, this.capacity, this.configName);
                        //    }
                        //    else
                        //    {
                        //        traceMessage = SR.GetString(SR.TraceCodeServiceThrottleLimitReachedInternal,
                        //                         this.capacity);
                        //    }

                        //    TraceUtility.TraceEvent(
                        //        TraceEventType.Warning, TraceCode.ServiceThrottleLimitReached, traceMessage);

                        //}
                    }

                    // To prevent the thread that's releasing a throttle being hijacked to run the continuations,
                    // set the TaskCreationOptions to make the waiting method run on a new thread.
                    tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _waiters.Enqueue(tcs);
                    acquiredThrottle = false;
                }

                _acquired?.Invoke();
                _ratio?.Invoke(_count);
            }

            if (!acquiredThrottle && tcs != null)
            {
                _ = await tcs.Task;
            }
        }

        internal void Release()
        {
            TaskCompletionSource<object> next = null;

            lock (_mutex)
            {
                if (_waiters.Count > 0)
                {
                    next = _waiters.Dequeue();
                    if (_waiters.Count == 0)
                        _waiters.TrimExcess();
                }
                else
                {
                    _count--;
                    if (_count < _warningRestoreLimit)
                    {
                        //if (TD.MessageThrottleAtSeventyPercentIsEnabled() && this.warningIssued)
                        //{
                        //    TD.MessageThrottleAtSeventyPercent(this.propertyName, this.capacity);
                        //}
                        _warningIssued = false;
                    }
                }
            }

            if (next != null)
                next.TrySetResult(null);

            _released?.Invoke();
            _ratio?.Invoke(_count);
        }

        internal void SetReleased(Action action)
        {
            _released = action;
        }

        internal void SetAcquired(Action action)
        {
            _acquired = action;
        }

        internal void SetRatio(Action<int> action)
        {
            _ratio = action;
        }
    }
}