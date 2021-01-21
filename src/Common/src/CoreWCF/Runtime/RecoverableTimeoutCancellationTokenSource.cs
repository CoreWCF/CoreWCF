// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using CoreWCF;

namespace CoreWCF.Runtime
{
    internal class RecoverableTimeoutCancellationTokenSource : CancellationTokenSource
    {
        private TimeSpan _originalTimeout;

        public RecoverableTimeoutCancellationTokenSource(TimeSpan timeout) : base()
        {
            if (timeout.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), $"Only TimeSpan's representing up to {int.MaxValue}ms are supported");
            }

            _originalTimeout = timeout;
        }

        public RecoverableTimeoutCancellationTokenSource(int millisecondsDelay)
        {
            if (millisecondsDelay == Timeout.Infinite)
            {
                _originalTimeout = Timeout.InfiniteTimeSpan;
            }
            else
            {
                _originalTimeout = TimeSpan.FromMilliseconds(millisecondsDelay);
            }
        }

        public override int GetHashCode()
        {
            return (int)_originalTimeout.TotalMilliseconds;
        }

        internal static TimeSpan GetOriginalTimeout(CancellationToken token)
        {
            // Covers CancellationToken.None as well as any other non-cancellable token
            if (!token.CanBeCanceled)
            {
                return Timeout.InfiniteTimeSpan;
            }

            return TimeSpan.FromMilliseconds(token.GetHashCode());
        }
    }

    internal class CancellationTokenSourceIOThreadTimer : IOThreadTimer
    {
        private readonly List<CancellationTokenSource> _cancellationTokenSources = new List<CancellationTokenSource>();
        private bool _timerFired = false;
        private Action<object> _timerFiredCallback;
        private object _timerFiredState;

        public CancellationTokenSourceIOThreadTimer() : base(TimerCallback, null, false)
        {
            Reinitialize(TimerCallback, this);
        }

        public void SetCompletionCallback(Action<object> callback, object state)
        {
            _timerFiredCallback = Fx.ThunkCallback(callback);
            _timerFiredState = state;
        }

        public void RegisterTokenSourceForCancellation(CancellationTokenSource cts)
        {
            // TODO: Consider if unregistering would be helpful. It would require
            // knowing that the CancellationToken is no longer needed.
            lock (_cancellationTokenSources)
            {
                if (!_timerFired)
                {
                    _cancellationTokenSources.Add(cts);
                    return;
                }
            }

            // Timer has already fired so cancelling now.
            CancelTokenSource(cts);
        }

        internal static void CancelTokenSource(object state)
        {
            var cts = (CancellationTokenSource)state;
            try
            {
                // Ensure all callbacks are fired
                cts.Cancel(throwOnFirstException: false);
                cts.Dispose();
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                // Callbacks shouldn't be throwing
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Error);
            }
        }

        internal void OnTimer()
        {
            _timerFiredCallback(_timerFiredState);

            lock (_cancellationTokenSources)
            {
                _timerFired = true;
            }
            // Once _timerFired is set, there's no need to hold the lock as
            // no more will be added to the list.
            foreach (var cts in _cancellationTokenSources)
            {
                // TODO: ActionItem.Schedule might be overkill here as I don't expect there
                // to be many cancellations. There's just no
                if (!cts.IsCancellationRequested)
                {
                    ActionItem.Schedule(CancelTokenSource, cts);
                }
            }
        }

        internal static void TimerCallback(object state)
        {
            var thisPtr = (CancellationTokenSourceIOThreadTimer)state;
            thisPtr.OnTimer();
        }
    }
}
