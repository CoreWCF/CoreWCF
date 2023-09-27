// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    internal class ConcurrencyBehavior
    {
        private readonly ConcurrencyMode _concurrencyMode;
        private readonly bool _enforceOrderedReceive;

        internal ConcurrencyBehavior(DispatchRuntime runtime)
        {
            _concurrencyMode = runtime.ConcurrencyMode;
            _enforceOrderedReceive = runtime.EnsureOrderedDispatch;
            //this.supportsTransactedBatch = ConcurrencyBehavior.SupportsTransactedBatch(runtime.ChannelDispatcher);
        }

        internal bool IsConcurrent(MessageRpc rpc)
        {
            return IsConcurrent(_concurrencyMode, _enforceOrderedReceive, rpc.Channel.HasSession/*, this.supportsTransactedBatch*/);
        }

        internal static bool IsConcurrent(ConcurrencyMode concurrencyMode, bool ensureOrderedDispatch, bool hasSession /*, bool supportsTransactedBatch*/)
        {
            //if (supportsTransactedBatch)
            //{
            //    return false;
            //}

            if (concurrencyMode != ConcurrencyMode.Single)
            {
                return true;
            }

            if (hasSession)
            {
                return false;
            }

            if (ensureOrderedDispatch)
            {
                return false;
            }

            return true;
        }

        internal static bool IsConcurrent(ChannelDispatcher runtime, bool hasSession)
        {
            bool isConcurrencyModeSingle = true;

            //if (ConcurrencyBehavior.SupportsTransactedBatch(runtime))
            //{
            //    return false;
            //}

            foreach (EndpointDispatcher endpointDispatcher in runtime.Endpoints)
            {
                if (endpointDispatcher.DispatchRuntime.EnsureOrderedDispatch)
                {
                    return false;
                }

                if (endpointDispatcher.DispatchRuntime.ConcurrencyMode != ConcurrencyMode.Single)
                {
                    isConcurrencyModeSingle = false;
                }
            }

            if (!isConcurrencyModeSingle)
            {
                return true;
            }

            if (!hasSession)
            {
                return true;
            }

            return false;
        }

        internal async Task LockInstanceAsync(MessageRpc rpc)
        {
            if (_concurrencyMode != ConcurrencyMode.Multiple)
            {
                ConcurrencyInstanceContextFacet resource = rpc.InstanceContext.Concurrency;
                Task waiter = null;
                lock (rpc.InstanceContext.ThisLock)
                {
                    if (!resource.Locked)
                    {
                        resource.Locked = true;
                    }
                    else
                    {
                        waiter = resource.EnqueueNewMessage();
                    }
                }

                if (waiter != null)
                {
                    await waiter;
                }

                // TODO: Throw this on setup
                if (_concurrencyMode == ConcurrencyMode.Reentrant)
                {
                    //throw new NotSupportedException(nameof(ConcurrencyMode.Reentrant));
                    throw new Exception($"ConcurrencyMode {nameof(ConcurrencyMode.Reentrant)} is not yet supported by current version" );
                }
            }
        }

        internal void UnlockInstance(ref MessageRpc rpc)
        {
            if (_concurrencyMode != ConcurrencyMode.Multiple)
            {
                UnlockInstance(rpc.InstanceContext);
            }
        }

        internal static void UnlockInstanceBeforeCallout(OperationContext operationContext)
        {
            if (operationContext != null && operationContext.IsServiceReentrant)
            {
                UnlockInstance(operationContext.InstanceContext);
            }
        }

        private static void UnlockInstance(InstanceContext instanceContext)
        {
            ConcurrencyInstanceContextFacet resource = instanceContext.Concurrency;

            lock (instanceContext.ThisLock)
            {
                if (resource.HasWaiters)
                {
                    resource.DequeueWaiter();
                }
                else
                {
                    //We have no pending Callouts and no new Messages to process
                    resource.Locked = false;
                }
            }
        }

        // TODO: Make async to remove blocking Wait call
        internal static Task LockInstanceAfterCalloutAsync(OperationContext operationContext)
        {
            if (operationContext != null)
            {
                InstanceContext instanceContext = operationContext.InstanceContext;

                if (operationContext.IsServiceReentrant)
                {
                    ConcurrencyInstanceContextFacet resource = instanceContext.Concurrency;
                    bool needToWait = false;
                    lock (instanceContext.ThisLock)
                    {
                        if (!resource.Locked)
                        {
                            resource.Locked = true;
                        }
                    }

                    if (needToWait)
                    {
                        return resource.EnqueueCalloutMessage();
                    }
                }
            }

            return Task.CompletedTask;
        }

        internal interface IWaiter
        {
            void Signal();
        }

        private class MessageRpcWaiter : IWaiter
        {
            private readonly IResumeMessageRpc _resume;

            internal MessageRpcWaiter(IResumeMessageRpc resume)
            {
                _resume = resume;
            }

            void IWaiter.Signal()
            {
                try
                {
                    _resume.Resume(out bool alreadyResumedNoLock);

                    if (alreadyResumedNoLock)
                    {
                        Fx.Assert("ConcurrencyBehavior resumed more than once for same call.");
                    }
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
                }
            }
        }

        private class ThreadWaiter : IWaiter
        {
            private readonly ManualResetEvent _wait = new ManualResetEvent(false);

            void IWaiter.Signal()
            {
                _wait.Set();
            }

            internal void Wait()
            {
                _wait.WaitOne();
                _wait.Dispose();
            }
        }
    }

    internal class ConcurrencyInstanceContextFacet
    {
        internal bool Locked;
        private Queue<TaskCompletionSource<object>> _calloutMessageQueue;
        private Queue<TaskCompletionSource<object>> _newMessageQueue;

        internal bool HasWaiters
        {
            get
            {
                return (((_calloutMessageQueue != null) && (_calloutMessageQueue.Count > 0)) ||
                        ((_newMessageQueue != null) && (_newMessageQueue.Count > 0)));
            }
        }

        private TaskCompletionSource<object> DequeueFrom(Queue<TaskCompletionSource<object>> queue)
        {
            TaskCompletionSource<object> waiter = queue.Dequeue();

            if (queue.Count == 0)
            {
                queue.TrimExcess();
            }

            return waiter;
        }

        internal void DequeueWaiter()
        {
            TaskCompletionSource<object> waiter;
            if ((_calloutMessageQueue != null) && (_calloutMessageQueue.Count > 0))
            {
                waiter = DequeueFrom(_calloutMessageQueue);
            }
            else
            {
                waiter = DequeueFrom(_newMessageQueue);
            }

            waiter.TrySetResult(null);
        }

        internal Task EnqueueNewMessage()
        {
            if (_newMessageQueue == null)
            {
                _newMessageQueue = new Queue<TaskCompletionSource<object>>();
            }
            // Prevent release of waiter from running the waiter on the releasing thread by using RunContinuationsAsynchronously
            var waiter = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _newMessageQueue.Enqueue(waiter);
            return waiter.Task;
        }

        internal Task EnqueueCalloutMessage()
        {
            if (_calloutMessageQueue == null)
            {
                _calloutMessageQueue = new Queue<TaskCompletionSource<object>>();
            }
            // Prevent release of waiter from running the waiter on the releasing thread by using RunContinuationsAsynchronously
            var waiter = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _calloutMessageQueue.Enqueue(waiter);
            return waiter.Task;
        }
    }
}