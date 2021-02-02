﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Runtime
{
    // TODO: Make internal again. I had to expose this for cross assembly usage
    public abstract class ActionItem
    {
        //SecurityContext context;
        private bool _isScheduled;

        protected ActionItem()
        {
        }

        public bool LowPriority { get; protected set; }

        public static void Schedule(Action<object> callback, object state)
        {
            Schedule(callback, state, false);
        }

        public static void Schedule(Action<object> callback, object state, bool lowPriority)
        {
            Fx.Assert(callback != null, "A null callback was passed for Schedule!");

            //if (Action<object>ActionItem.ShouldUseActivity ||
            //    Fx.Trace.IsEnd2EndActivityTracingEnabled)
            //{
            //    new DefaultActionItem(callback, state, lowPriority).Schedule();
            //}
            //else
            //{
            ScheduleCallback(callback, state, lowPriority);
            //}
        }


        protected abstract void Invoke();

        protected void Schedule()
        {
            if (_isScheduled)
            {
                throw Fx.Exception.AsError(new InvalidOperationException(SR.ActionItemIsAlreadyScheduled));
            }

            _isScheduled = true;
            //if (this.context != null)
            //{
            //    ScheduleCallback(CallbackHelper.InvokeWithContextCallback);
            //}
            //else
            //{
            ScheduleCallback(CallbackHelper.InvokeWithoutContextCallback);
            //}
        }

        //protected void ScheduleWithContext(SecurityContext context)
        //{
        //    if (context == null)
        //    {
        //        throw Fx.Exception.ArgumentNull("context");
        //    }
        //    if (isScheduled)
        //    {
        //        throw Fx.Exception.AsError(new InvalidOperationException(InternalSR.ActionItemIsAlreadyScheduled));
        //    }

        //    this.isScheduled = true;
        //    this.context = context.CreateCopy();
        //    ScheduleCallback(CallbackHelper.InvokeWithContextCallback);
        //}

        protected void ScheduleWithoutContext()
        {
            if (_isScheduled)
            {
                throw Fx.Exception.AsError(new InvalidOperationException(SR.ActionItemIsAlreadyScheduled));
            }

            _isScheduled = true;
            ScheduleCallback(CallbackHelper.InvokeWithoutContextCallback);
        }

        private static void ScheduleCallback(Action<object> callback, object state, bool lowPriority)
        {
            Fx.Assert(callback != null, "Cannot schedule a null callback");
            if (lowPriority)
            {
                IOThreadScheduler.ScheduleCallbackLowPriNoFlow(callback, state);
            }
            else
            {
                IOThreadScheduler.ScheduleCallbackNoFlow(callback, state);
            }
        }

        //SecurityContext ExtractContext()
        //{
        //    Fx.Assert(this.context != null, "Cannot bind to a null context; context should have been set by now");
        //    Fx.Assert(this.isScheduled, "Context is extracted only while the object is scheduled");
        //    SecurityContext result = this.context;
        //    this.context = null;
        //    return result;
        //}

        private void ScheduleCallback(Action<object> callback)
        {
            ScheduleCallback(callback, this, LowPriority);
        }

        private static class CallbackHelper
        {
            private static Action<object> s_invokeWithContextCallback;
            private static Action<object> s_invokeWithoutContextCallback;
            private static ContextCallback s_onContextAppliedCallback;

            public static Action<object> InvokeWithContextCallback
            {
                get
                {
                    if (s_invokeWithContextCallback == null)
                    {
                        s_invokeWithContextCallback = InvokeWithContext;
                    }
                    return s_invokeWithContextCallback;
                }
            }

            public static Action<object> InvokeWithoutContextCallback
            {
                get
                {
                    if (s_invokeWithoutContextCallback == null)
                    {
                        s_invokeWithoutContextCallback = InvokeWithoutContext;
                    }
                    return s_invokeWithoutContextCallback;
                }
            }

            public static ContextCallback OnContextAppliedCallback
            {
                get
                {
                    if (s_onContextAppliedCallback == null)
                    {
                        s_onContextAppliedCallback = new ContextCallback(OnContextApplied);
                    }
                    return s_onContextAppliedCallback;
                }
            }

            private static void InvokeWithContext(object state)
            {
                throw new PlatformNotSupportedException();
                //SecurityContext context = ((ActionItem)state).ExtractContext();
                //SecurityContext.Run(context, OnContextAppliedCallback, state);
            }

            private static void InvokeWithoutContext(object state)
            {
                ((ActionItem)state).Invoke();
                ((ActionItem)state)._isScheduled = false;
            }

            private static void OnContextApplied(object o)
            {
                ((ActionItem)o).Invoke();
                ((ActionItem)o)._isScheduled = false;
            }
        }

        private class DefaultActionItem : ActionItem
        {
            private readonly Action<object> _callback;
            private readonly object _state;

            //bool flowLegacyActivityId;
            //Guid activityId;
            //EventTraceActivity eventTraceActivity;

            public DefaultActionItem(Action<object> callback, object state, bool isLowPriority)
            {
                Fx.Assert(callback != null, "Shouldn't instantiate an object to wrap a null callback");
                LowPriority = isLowPriority;
                _callback = callback;
                _state = state;
                //if (Action<object>ActionItem.ShouldUseActivity)
                //{
                //    this.flowLegacyActivityId = true;
                //    this.activityId = EtwDiagnosticTrace.ActivityId;
                //}
                //if (Fx.Trace.IsEnd2EndActivityTracingEnabled)
                //{
                //    this.eventTraceActivity = EventTraceActivity.GetFromThreadOrCreate();
                //    if (TraceCore.ActionItemScheduledIsEnabled(Fx.Trace))
                //    {
                //        TraceCore.ActionItemScheduled(Fx.Trace, this.eventTraceActivity);
                //    }
                //}

            }

            protected override void Invoke()
            {
                //if (this.flowLegacyActivityId || Fx.Trace.IsEnd2EndActivityTracingEnabled)
                //{
                //    TraceAndInvoke();
                //}
                //else
                //{
                _callback(_state);
                //}
            }

            private void TraceAndInvoke()
            {
                //TODO:  Consider merging these since they go through the same codepath.
                //if (this.flowLegacyActivityId)
                //{
                //    Guid currentActivityId = EtwDiagnosticTrace.ActivityId;
                //    try
                //    {
                //        EtwDiagnosticTrace.ActivityId = this.activityId;
                //        this.callback(this.state);
                //    }
                //    finally
                //    {
                //        EtwDiagnosticTrace.ActivityId = currentActivityId;
                //    }
                //}
                //else
                //{
                Guid previous = Guid.Empty;
                bool restoreActivityId = false;
                try
                {
                    //if (this.eventTraceActivity != null)
                    //{
                    //    previous = Trace.CorrelationManager.ActivityId;
                    //    restoreActivityId = true;
                    //    Trace.CorrelationManager.ActivityId = this.eventTraceActivity.ActivityId;
                    //    if (TraceCore.ActionItemCallbackInvokedIsEnabled(Fx.Trace))
                    //    {
                    //        TraceCore.ActionItemCallbackInvoked(Fx.Trace, this.eventTraceActivity);
                    //    }
                    //}
                    _callback(_state);
                }
                finally
                {
                    if (restoreActivityId)
                    {
                        //Trace.CorrelationManager.ActivityId = previous;
                    }
                }
                //}
            }
        }

        public static TaskScheduler IOTaskScheduler = new IOThreadTaskScheduler();

        internal class IOThreadTaskScheduler : TaskScheduler
        {
            [ThreadStatic]
            private static bool s_onSchedulerThread;

            internal IOThreadTaskScheduler()
            {
                _tasks = new ConcurrentQueue<Task>();
            }

            // The queue of tasks to execute, maintained for debugging purposes
            // An alternative implementation would be to pass the Task directly
            // to the OnTaskQueued method. Using an intermediate queue might cause
            // a performance bottleneck. Profiling will be needed to determine.
            // Unless this is discovered to be a problem, using an intermediate
            // queue to aid in debugging.
            private readonly ConcurrentQueue<Task> _tasks;

            private static void OnTaskQueued(object obj)
            {
                var thisPtr = obj as IOThreadTaskScheduler;
                if (thisPtr._tasks.TryDequeue(out Task nextTask))
                {
                    s_onSchedulerThread = true;
                    thisPtr.TryExecuteTask(nextTask);
                    s_onSchedulerThread = false;
                }
            }

            protected override IEnumerable<Task> GetScheduledTasks()
            {
                return _tasks;
            }

            protected override void QueueTask(Task task)
            {
                _tasks.Enqueue(task);
                Schedule(OnTaskQueued, this);
            }

            protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
            {
                return s_onSchedulerThread && TryExecuteTask(task);
            }
        }
    }
}