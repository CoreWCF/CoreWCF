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

        // The coalesced timer this source is registered with, if any. It's used to remove
        // this source from the timer's tracking list when the operation completes so the
        // source can be disposed and collected instead of lingering until the timer fires.
        private CancellationTokenSourceIOThreadTimer _owningTimer;
        private int _unregistered;

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

        internal void SetOwningTimer(CancellationTokenSourceIOThreadTimer timer)
        {
            _owningTimer = timer;
        }

        // Called on the normal completion path once the CancellationToken is no longer
        // needed. It removes this source from the coalesced timer's tracking list (so it
        // no longer keeps this instance alive until the send timeout expires) and disposes
        // it. Safe to call multiple times and from any thread.
        internal void Unregister()
        {
            if (Interlocked.Exchange(ref _unregistered, 1) != 0)
            {
                return;
            }

            _owningTimer?.UnregisterTokenSourceForCancellation(this);
            Dispose();
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

    // Lightweight (allocation-free) handle handed back when a cancellation token is
    // acquired for a scoped operation. Disposing it on the completion path unregisters
    // and disposes the underlying source. Disposing a default instance is a no-op, which
    // covers the CancellationToken.None / pre-cancelled cases where there's no source.
    internal readonly struct RecoverableTokenRegistration
    {
        private readonly RecoverableTimeoutCancellationTokenSource _tokenSource;

        internal RecoverableTokenRegistration(RecoverableTimeoutCancellationTokenSource tokenSource)
        {
            _tokenSource = tokenSource;
        }

        public void Dispose()
        {
            _tokenSource?.Unregister();
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

        public void RegisterTokenSourceForCancellation(RecoverableTimeoutCancellationTokenSource cts)
        {
            lock (_cancellationTokenSources)
            {
                if (!_timerFired)
                {
                    cts.SetOwningTimer(this);
                    _cancellationTokenSources.Add(cts);
                    return;
                }
            }

            // Timer has already fired so cancelling now.
            CancelTokenSource(cts);
        }

        // Removes a token source that was registered via RegisterTokenSourceForCancellation.
        // Called from the normal completion path (RecoverableTimeoutCancellationTokenSource.Unregister)
        // so completed sources don't accumulate in the list until the timer fires. If the timer
        // has already fired the list is no longer mutated (OnTimer iterates it without the lock),
        // so there's nothing to remove.
        public void UnregisterTokenSourceForCancellation(RecoverableTimeoutCancellationTokenSource cts)
        {
            lock (_cancellationTokenSources)
            {
                if (!_timerFired)
                {
                    _cancellationTokenSources.Remove(cts);
                }
            }
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
            foreach (CancellationTokenSource cts in _cancellationTokenSources)
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
