// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal enum LifetimeState
    {
        Opened,
        Closing,
        Closed
    }

    internal class LifetimeManager
    {
        private bool _aborted;
        private int _busyCount;
        private ICommunicationWaiter _busyWaiter;
        private int _busyWaiterCount;
        private readonly object _mutex;
        private LifetimeState _state;

        public LifetimeManager(object mutex)
        {
            _mutex = mutex;
            _state = LifetimeState.Opened;
        }

        public int BusyCount
        {
            get { return _busyCount; }
        }

        protected LifetimeState State
        {
            get { return _state; }
        }

        protected object ThisLock
        {
            get { return _mutex; }
        }

        public void Abort()
        {
            lock (ThisLock)
            {
                if (State == LifetimeState.Closed || _aborted)
                {
                    return;
                }

                _aborted = true;
                _state = LifetimeState.Closing;
            }

            OnAbort();
            _state = LifetimeState.Closed;
        }

        private void ThrowIfNotOpened()
        {
            if (!_aborted && _state != LifetimeState.Opened)
            {
            }
        }

        public async Task CloseAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            lock (ThisLock)
            {
                ThrowIfNotOpened();
                _state = LifetimeState.Closing;
            }

            await OnCloseAsync(token);
            _state = LifetimeState.Closed;
        }

        protected virtual async Task OnCloseAsync(CancellationToken token)
        {
            switch (await CloseCoreAsync(false, token))
            {
                case CommunicationWaitResult.Expired:
                    // TODO: Derive CancellationToken so that the original timeout can be stored inside
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TimeoutException(SR.Format(SR.SFxCloseTimedOut1, null)));
                case CommunicationWaitResult.Aborted:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().ToString()));
            }
        }

        public async Task<CommunicationWaitResult> CloseCoreAsync(bool aborting, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ICommunicationWaiter busyWaiter = null;
            CommunicationWaitResult result = CommunicationWaitResult.Succeeded;

            lock (ThisLock)
            {
                if (_busyCount > 0)
                {
                    if (_busyWaiter != null)
                    {
                        if (!aborting && _aborted)
                        {
                            return CommunicationWaitResult.Aborted;
                        }

                        busyWaiter = _busyWaiter;
                    }
                    else
                    {
                        busyWaiter = new AsyncCommunicationWaiter(ThisLock);
                        _busyWaiter = busyWaiter;
                    }
                    Interlocked.Increment(ref _busyWaiterCount);
                }
            }

            if (busyWaiter != null)
            {
                result = await busyWaiter.WaitAsync(aborting, token);
                if (Interlocked.Decrement(ref _busyWaiterCount) == 0)
                {
                    busyWaiter.Dispose();
                    _busyWaiter = null;
                }
            }

            return result;
        }

        private CommunicationWaitResult AbortCore(CancellationToken token)
        {
            ICommunicationWaiter busyWaiter = null;
            CommunicationWaitResult result = CommunicationWaitResult.Succeeded;

            lock (ThisLock)
            {
                if (_busyCount > 0)
                {
                    if (_busyWaiter != null)
                    {
                        busyWaiter = _busyWaiter;
                    }
                    else
                    {
                        busyWaiter = new AsyncCommunicationWaiter(ThisLock);
                        _busyWaiter = busyWaiter;
                    }
                    Interlocked.Increment(ref _busyWaiterCount);
                }
            }

            if (busyWaiter != null)
            {
                result = busyWaiter.Wait(true, token);
                if (Interlocked.Decrement(ref _busyWaiterCount) == 0)
                {
                    busyWaiter.Dispose();
                    _busyWaiter = null;
                }
            }

            return result;
        }

        protected void DecrementBusyCount()
        {
            ICommunicationWaiter busyWaiter = null;
            bool empty = false;

            lock (ThisLock)
            {
                if (_busyCount <= 0)
                {
                    throw Fx.AssertAndThrow("LifetimeManager.DecrementBusyCount: (this.busyCount > 0)");
                }
                if (--_busyCount == 0)
                {
                    if (_busyWaiter != null)
                    {
                        busyWaiter = _busyWaiter;
                        Interlocked.Increment(ref _busyWaiterCount);
                    }
                    empty = true;
                }
            }

            if (busyWaiter != null)
            {
                busyWaiter.Signal();
                if (Interlocked.Decrement(ref _busyWaiterCount) == 0)
                {
                    busyWaiter.Dispose();
                    _busyWaiter = null;
                }
            }

            if (empty && State == LifetimeState.Opened)
            {
                OnEmpty();
            }
        }

        protected virtual void IncrementBusyCount()
        {
            lock (ThisLock)
            {
                Fx.Assert(State == LifetimeState.Opened, "LifetimeManager.IncrementBusyCount: (this.State == LifetimeState.Opened)");
                _busyCount++;
            }
        }

        protected virtual void IncrementBusyCountWithoutLock()
        {
            Fx.Assert(State == LifetimeState.Opened, "LifetimeManager.IncrementBusyCountWithoutLock: (this.State == LifetimeState.Opened)");
            _busyCount++;
        }

        protected virtual void OnAbort()
        {
            // We have decided not to make this configurable
            AbortCore(new CancellationTokenSource(TimeSpan.FromSeconds(1)).Token);
        }

        protected virtual void OnEmpty()
        {
        }
    }

    internal enum CommunicationWaitResult
    {
        Waiting,
        Succeeded,
        Expired,
        Aborted
    }

    internal interface ICommunicationWaiter : IDisposable
    {
        void Signal();
        Task<CommunicationWaitResult> WaitAsync(bool aborting, CancellationToken token);
        CommunicationWaitResult Wait(bool aborting, CancellationToken token);
    }

    internal class AsyncCommunicationWaiter : ICommunicationWaiter
    {
        private bool _closed;
        private readonly object _mutex;
        private CommunicationWaitResult _result;

        private TaskCompletionSource<bool> _tcs;

        internal AsyncCommunicationWaiter(object mutex)
        {
            _mutex = mutex;
            _tcs = new TaskCompletionSource<bool>();
        }

        private object ThisLock
        {
            get { return _mutex; }
        }

        public void Dispose()
        {
            lock (ThisLock)
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;
                _tcs?.TrySetResult(false);
            }
        }

        public void Signal()
        {
            lock (ThisLock)
            {
                if (_closed)
                {
                    return;
                }

                _tcs.TrySetResult(true);
            }
        }

        public async Task<CommunicationWaitResult> WaitAsync(bool aborting, CancellationToken token)
        {
            Fx.Assert(token.CanBeCanceled, "CancellationToken must be cancellable");

            if (_closed)
            {
                return CommunicationWaitResult.Aborted;
            }

            if (token.IsCancellationRequested)
            {
                return CommunicationWaitResult.Expired;
            }

            if (aborting)
            {
                _result = CommunicationWaitResult.Aborted;
            }

            _tcs = new TaskCompletionSource<bool>();
            using (token.Register(WaiterTimeout, _tcs))
            {
                await _tcs.Task;
                bool expired = token.IsCancellationRequested;

                lock (ThisLock)
                {
                    if (_result == CommunicationWaitResult.Waiting)
                    {
                        _result = (expired ? CommunicationWaitResult.Expired : CommunicationWaitResult.Succeeded);
                    }
                }

                return _result;
            }
        }

        public CommunicationWaitResult Wait(bool aborting, CancellationToken token)
        {
            return WaitAsync(aborting, token).GetAwaiter().GetResult();
        }

        internal static void WaiterTimeout(object state)
        {
            var tcs = state as TaskCompletionSource<bool>;
            tcs?.TrySetResult(false);
        }
    }
}