// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    internal sealed class QuotaThrottle
    {
        private readonly object _mutex;
        private readonly Queue<TaskCompletionSource<object>> _waiters;
        //private bool _didTraceThrottleLimit;
        // private string _propertyName = "ManualFlowControlLimit"; // Used for eventing
#pragma warning disable IDE0052 // Remove unread private members
        private string _owner; // Used for eventing
#pragma warning restore IDE0052 // Remove unread private members

        internal QuotaThrottle(object mutex)
        {
            Limit = int.MaxValue;
            _mutex = mutex;
            _waiters = new Queue<TaskCompletionSource<object>>();
        }

        private bool IsEnabled
        {
            get { return Limit != int.MaxValue; }
        }

        internal string Owner
        {
            set { _owner = value; }
        }

        internal int Limit { get; private set; }

        internal Task AcquireAsync()
        {
            lock (_mutex)
            {
                if (IsEnabled)
                {
                    if (Limit > 0)
                    {
                        Limit--;

                        if (Limit == 0)
                        {
                            // TODO: Events
                            //if (DiagnosticUtility.ShouldTraceWarning && !_didTraceThrottleLimit)
                            //{
                            //    _didTraceThrottleLimit = true;

                            //    TraceUtility.TraceEvent(
                            //        TraceEventType.Warning,
                            //        TraceCode.ManualFlowThrottleLimitReached,
                            //        SR.GetString(SR.TraceCodeManualFlowThrottleLimitReached,
                            //                     _propertyName, _owner));
                            //}
                        }

                        return Task.CompletedTask;
                    }
                    else
                    {
                        var tcs = new TaskCompletionSource<object>();
                        _waiters.Enqueue(tcs);
                        return tcs.Task;
                    }
                }
                else
                {
                    return Task.CompletedTask;
                }
            }
        }

        internal int IncrementLimit(int incrementBy)
        {
            if (incrementBy < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(incrementBy), incrementBy,
                                                     SRCommon.ValueMustBeNonNegative));
            }

            int newLimit;
            TaskCompletionSource<object>[] released = null;

            lock (_mutex)
            {
                if (IsEnabled)
                {
                    checked { Limit += incrementBy; }
                    released = LimitChanged();
                }

                newLimit = Limit;
            }

            if (released != null)
            {
                Release(released);
            }

            return newLimit;
        }

        private TaskCompletionSource<object>[] LimitChanged()
        {
            TaskCompletionSource<object>[] released = null;

            if (IsEnabled)
            {
                if ((_waiters.Count > 0) && (Limit > 0))
                {
                    if (Limit < _waiters.Count)
                    {
                        released = new TaskCompletionSource<object>[Limit];
                        for (int i = 0; i < Limit; i++)
                        {
                            released[i] = _waiters.Dequeue();
                        }

                        Limit = 0;
                    }
                    else
                    {
                        released = _waiters.ToArray();
                        _waiters.Clear();
                        _waiters.TrimExcess();

                        Limit -= released.Length;
                    }
                }
                //didTraceThrottleLimit = false;
            }
            else
            {
                released = _waiters.ToArray();
                _waiters.Clear();
                _waiters.TrimExcess();
            }

            return released;
        }

        internal void SetLimit(int messageLimit)
        {
            if (messageLimit < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(messageLimit), messageLimit,
                                                    SRCommon.ValueMustBeNonNegative));
            }

            TaskCompletionSource<object>[] released = null;

            lock (_mutex)
            {
                Limit = messageLimit;
                released = LimitChanged();
            }

            if (released != null)
            {
                Release(released);
            }
        }

        private void ReleaseAsync(object state)
        {
            ((TaskCompletionSource<object>)state).TrySetResult(null);
        }

        internal void Release(TaskCompletionSource<object>[] released)
        {
            for (int i = 0; i < released.Length; i++)
            {
                ActionItem.Schedule(ReleaseAsync, released[i]);
            }
        }
    }
}
