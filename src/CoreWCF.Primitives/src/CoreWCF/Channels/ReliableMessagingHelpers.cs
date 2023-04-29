// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal class InterruptibleWaitObject
    {
        private bool _aborted = false;
        private CommunicationObject _communicationObject;
        private bool _set;
        private int _syncWaiters;
        private object _thisLock = new object();
        private bool _throwTimeoutByDefault = true;
        private TaskCompletionSource<object> _tcs;

        public InterruptibleWaitObject(bool signaled)
            : this(signaled, true)
        {
        }

        public InterruptibleWaitObject(bool signaled, bool throwTimeoutByDefault)
        {
            _set = signaled;
            _throwTimeoutByDefault = throwTimeoutByDefault;
        }

        public void Abort(CommunicationObject communicationObject)
        {
            if (communicationObject == null)
            {
                throw Fx.AssertAndThrow("Argument communicationObject cannot be null.");
            }

            lock (_thisLock)
            {
                if (_aborted)
                {
                    return;
                }

                _communicationObject = communicationObject;

                _aborted = true;
                InternalSet();
            }
        }

        public void Fault(CommunicationObject communicationObject)
        {
            if (communicationObject == null)
            {
                throw Fx.AssertAndThrow("Argument communicationObject cannot be null.");
            }

            lock (_thisLock)
            {
                if (_aborted)
                {
                    return;
                }

                _communicationObject = communicationObject;

                _aborted = false;
                InternalSet();
            }
        }

        private Exception GetException()
        {
            if (_communicationObject == null)
            {
                Fx.Assert("Caller is attempting to retrieve an exception from a null communicationObject.");
            }

            return _aborted
                ? _communicationObject.CreateAbortedException()
                : _communicationObject.GetPendingException();
        }

        private void InternalSet()
        {
            lock (_thisLock)
            {
                _set = true;

                if (_tcs != null)
                {
                    _tcs.TrySetResult(null);
                }
            }
        }

        public void Reset()
        {
            lock (_thisLock)
            {
                _communicationObject = null;
                _aborted = false;
                _set = false;

                if (_tcs != null && _tcs.Task.IsCompleted)
                {
                    _tcs = new TaskCompletionSource<object>();
                }
            }
        }

        public void Set()
        {
            InternalSet();
        }

        public Task<bool> WaitAsync(CancellationToken token)
        {
            return WaitAsync(token, _throwTimeoutByDefault);
        }

        public async Task<bool> WaitAsync(CancellationToken token, bool throwTimeoutException)
        {
            lock (_thisLock)
            {
                if (_set)
                {
                    if (_communicationObject != null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(GetException());
                    }

                    return true;
                }

                if (_tcs == null)
                {
                    _tcs = new TaskCompletionSource<object>();
                }

                _syncWaiters++;
            }

            try
            {
                if (!await _tcs.Task.WaitWithCancellationAsync(token))
                {
                    if (throwTimeoutException)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TimeoutException(SR.Format(SR.TimeoutOnOperation, TimeoutHelper.GetOriginalTimeout(token))));
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            finally
            {
                lock (_thisLock)
                {
                    // Last one out turns off the light.
                    _syncWaiters--;
                    if (_syncWaiters == 0 && _tcs.Task.IsCompleted)
                    {
                        _tcs = null;
                    }
                }
            }

            if (_communicationObject != null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(GetException());
            }

            return true;
        }
    }
}
