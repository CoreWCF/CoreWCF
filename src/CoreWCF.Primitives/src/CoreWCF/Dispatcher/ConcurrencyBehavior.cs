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
        private ConcurrencyMode concurrencyMode;
        private bool enforceOrderedReceive;

        internal ConcurrencyBehavior(DispatchRuntime runtime)
        {
            concurrencyMode = runtime.ConcurrencyMode;
            enforceOrderedReceive = runtime.EnsureOrderedDispatch;
            //this.supportsTransactedBatch = ConcurrencyBehavior.SupportsTransactedBatch(runtime.ChannelDispatcher);
        }

        internal bool IsConcurrent(MessageRpc rpc)
        {
            return IsConcurrent(concurrencyMode, enforceOrderedReceive, rpc.Channel.HasSession/*, this.supportsTransactedBatch*/);
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
            if (concurrencyMode != ConcurrencyMode.Multiple)
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
                if (concurrencyMode == ConcurrencyMode.Reentrant)
                {
                    throw new NotSupportedException(nameof(ConcurrencyMode.Reentrant));
                }
            }
        }

        internal void UnlockInstance(ref MessageRpc rpc)
        {
            if (concurrencyMode != ConcurrencyMode.Multiple)
            {
                ConcurrencyBehavior.UnlockInstance(rpc.InstanceContext);
            }
        }

        internal static void UnlockInstanceBeforeCallout(OperationContext operationContext)
        {
            if (operationContext != null && operationContext.IsServiceReentrant)
            {
                ConcurrencyBehavior.UnlockInstance(operationContext.InstanceContext);
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
            private IResumeMessageRpc resume;

            internal MessageRpcWaiter(IResumeMessageRpc resume)
            {
                this.resume = resume;
            }

            void IWaiter.Signal()
            {
                try
                {
                    bool alreadyResumedNoLock;
                    resume.Resume(out alreadyResumedNoLock);

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
            private ManualResetEvent wait = new ManualResetEvent(false);

            void IWaiter.Signal()
            {
                wait.Set();
            }

            internal void Wait()
            {
                wait.WaitOne();
                wait.Dispose();
            }
        }
    }

    internal class ConcurrencyInstanceContextFacet
    {
        internal bool Locked;
        private Queue<TaskCompletionSource<object>> calloutMessageQueue;
        private Queue<TaskCompletionSource<object>> newMessageQueue;

        internal bool HasWaiters
        {
            get
            {
                return (((calloutMessageQueue != null) && (calloutMessageQueue.Count > 0)) ||
                        ((newMessageQueue != null) && (newMessageQueue.Count > 0)));
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
            if ((calloutMessageQueue != null) && (calloutMessageQueue.Count > 0))
            {
                waiter = DequeueFrom(calloutMessageQueue);
            }
            else
            {
                waiter = DequeueFrom(newMessageQueue);
            }

            waiter.TrySetResult(null);
        }

        internal Task EnqueueNewMessage()
        {
            if (newMessageQueue == null)
                newMessageQueue = new Queue<TaskCompletionSource<object>>();
            // Prevent release of waiter from running the waiter on the releasing thread by using RunContinuationsAsynchronously
            var waiter = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            newMessageQueue.Enqueue(waiter);
            return waiter.Task;
        }

        internal Task EnqueueCalloutMessage()
        {
            if (calloutMessageQueue == null)
                calloutMessageQueue = new Queue<TaskCompletionSource<object>>();
            // Prevent release of waiter from running the waiter on the releasing thread by using RunContinuationsAsynchronously
            var waiter = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            calloutMessageQueue.Enqueue(waiter);
            return waiter.Task;
        }
    }

}