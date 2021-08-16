// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
#if DEBUG
using System.Diagnostics;
#endif

namespace CoreWCF.Runtime
{
    // See https://github.com/CoreWCF/CoreWCF/pull/399#issuecomment-898045090 for remark on broken functionality and memory leak
    // which justifies the obsolete attribute here.
    [Obsolete("This class leaks memory and should not be used anymore to provide async locks. Use SimpleAsyncLock instead.")]
    internal class AsyncLock
    {
#if DEBUG
        private StackTrace _lockTakenCallStack;
        private string _lockTakenCallStackString;
#endif
        private readonly SemaphoreSlim _semaphore;
        private readonly SafeSemaphoreRelease _semaphoreRelease;
        private static readonly AsyncLocal<object> s_heldLocks = new AsyncLocal<object>(LockTakenValueChanged);

        public AsyncLock()
        {
            _semaphore = new SemaphoreSlim(1);
            _semaphoreRelease = new SafeSemaphoreRelease(this);
        }

        private static void LockTakenValueChanged(AsyncLocalValueChangedArgs<object> obj)
        {
            // Without this fixup, when completing the call to await TakeLockAsync there is
            // a switch of Context and _heldLocks will be reset to null. This is because
            // of leaving the task.

            if (obj.ThreadContextChanged)
            {
                s_heldLocks.Value = obj.PreviousValue;
            }
        }

        public Task<IDisposable> TakeLockAsync()
        {
            return TakeLockAsync(default(CancellationToken));
        }

        public async Task<IDisposable> TakeLockAsync(CancellationToken token)
        {
#if DEBUG
            object existingValue = s_heldLocks.Value;
#endif // DEBUG
            AsyncLock existingLock = s_heldLocks.Value as AsyncLock;
            if (existingLock == this)
            {
                return null;
            }

            List<AsyncLock> existingLocks = null;
            if (existingLock == null)
            {
                existingLocks = s_heldLocks.Value as List<AsyncLock>;
                if (existingLocks?.Contains(this) ?? false)
                {
                    return null;
                }
            }

            await _semaphore.WaitAsync(token);

#if DEBUG
            Debug.Assert(existingValue == s_heldLocks.Value, "AsyncLocal modified while awaiting");
#endif // DEBUG

            if (s_heldLocks.Value == null) // No locks previously entered
            {
                s_heldLocks.Value = this;
            }
            else if (existingLock != null) // A single AsyncLock already entered but not this instance
            {
                // Create new list of held locks and add the single existing lock and this lock to it
                s_heldLocks.Value = new List<AsyncLock>(new AsyncLock[] { existingLock, this });
            }
            else
            {
#if DEBUG
                Debug.Assert(existingLocks != null, "_heldLocks.Value has invalid value, type of value is " + s_heldLocks.Value?.GetType() ?? "(null)");
#endif
                existingLocks.Add(this);
            }
#if DEBUG
            _lockTakenCallStack = new StackTrace();
            _lockTakenCallStackString = _lockTakenCallStack.ToString();
#endif
            return _semaphoreRelease;
        }

        public async Task<IDisposable> TakeLockAsync(TimeSpan timeout)
        {
#if DEBUG
            object existingValue = s_heldLocks.Value;
#endif // DEBUG
            AsyncLock existingLock = s_heldLocks.Value as AsyncLock;
            if (existingLock == this)
            {
                return null;
            }

            List<AsyncLock> existingLocks = null;
            if (existingLock == null)
            {
                existingLocks = s_heldLocks.Value as List<AsyncLock>;
                if (existingLocks?.Contains(this) ?? false)
                {
                    return null;
                }
            }

            await _semaphore.WaitAsync(timeout);

#if DEBUG
            Debug.Assert(existingValue == s_heldLocks.Value, "AsyncLocal modified while awaiting");
#endif // DEBUG

            if (s_heldLocks.Value == null) // No locks previously entered
            {
                s_heldLocks.Value = this;
            }
            else if (existingLock != null) // A single AsyncLock already entered but not this instance
            {
                // Create new list of held locks and add the single existing lock and this lock to it
                s_heldLocks.Value = new List<AsyncLock>(new AsyncLock[] { existingLock, this });
            }
            else
            {
#if DEBUG
                Debug.Assert(existingLocks != null, "_heldLocks.Value has invalid value, type of value is " + s_heldLocks.Value?.GetType() ?? "(null)");
#endif
                existingLocks.Add(this);
            }
#if DEBUG
            _lockTakenCallStack = new StackTrace();
            _lockTakenCallStackString = _lockTakenCallStack.ToString();
#endif
            return _semaphoreRelease;
        }

        public IDisposable TakeLock()
        {
            return TakeLock(Timeout.Infinite);
        }

        public IDisposable TakeLock(TimeSpan timeout)
        {
            return TakeLock((int)timeout.TotalMilliseconds);
        }

        public IDisposable TakeLock(int timeout)
        {
#if DEBUG
            object existingValue = s_heldLocks.Value;
#endif // DEBUG
            AsyncLock existingLock = s_heldLocks.Value as AsyncLock;
            if (existingLock == this)
            {
                return null;
            }

            List<AsyncLock> existingLocks = null;
            if (existingLock == null)
            {
                existingLocks = s_heldLocks.Value as List<AsyncLock>;
                if (existingLocks?.Contains(this) ?? false)
                {
                    return null;
                }
            }

            _semaphore.Wait(timeout);

#if DEBUG
            Debug.Assert(existingValue == s_heldLocks.Value, "AsyncLocal modified while awaiting");
#endif // DEBUG

            if (s_heldLocks.Value == null) // No locks previously entered
            {
                s_heldLocks.Value = this;
            }
            else if (existingLock != null) // A single AsyncLock already entered but not this instance
            {
                // Create new list of held locks and add the single existing lock and this lock to it
                s_heldLocks.Value = new List<AsyncLock>(new AsyncLock[] { existingLock, this });
            }
            else
            {
#if DEBUG
                Debug.Assert(existingLocks != null, "_heldLocks.Value has invalid value, type of value is " + s_heldLocks.Value?.GetType() ?? "(null)");
#endif
                existingLocks.Add(this);
            }
#if DEBUG
            _lockTakenCallStack = new StackTrace();
            _lockTakenCallStackString = _lockTakenCallStack.ToString();
#endif
            return _semaphoreRelease;
        }

        public struct SafeSemaphoreRelease : IDisposable
        {
            private readonly AsyncLock _asyncLock;

            public SafeSemaphoreRelease(AsyncLock asyncLock)
            {
                _asyncLock = asyncLock;
            }

            public void Dispose()
            {
#if DEBUG
                _asyncLock._lockTakenCallStack = null;
                _asyncLock._lockTakenCallStackString = null;
#endif
                if (s_heldLocks.Value == _asyncLock) // This is the only lock entered
                {
                    s_heldLocks.Value = null;
                }
                else if (s_heldLocks.Value is List<AsyncLock> listOfLocks)
                {
#if DEBUG
                    Debug.Assert(listOfLocks.Contains(_asyncLock), "The list of AsyncLock's didn't contain the expected lock");
#endif
                    // As locks are expected to be released in the order they are taken and they are always appended to the end,
                    // removal should be O(n) simply to look for the lock and removal should be constant time. If this becomes
                    // a significant overhead, then manual search in reverse will fix it. Keeping simple for now.
                    listOfLocks.Remove(_asyncLock);
                    if (listOfLocks.Count == 1) // If only one lock left, replace list with single lock.
                    {
                        s_heldLocks.Value = listOfLocks[0];
                    }
                }

                _asyncLock._semaphore.Release();
            }
        }
    }
}
