﻿using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using CoreWCF.Runtime.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace CoreWCF.Runtime
{
    internal static class Fx
    {
        const string defaultEventSource = "Microsoft.Runtime";
        static ExceptionTrace s_exceptionTrace;
        static EtwDiagnosticTrace s_diagnosticTrace;
        static ExceptionHandler s_asynchronousThreadExceptionHandler;

        public static ExceptionTrace Exception
        {
            get
            {
                if (s_exceptionTrace == null)
                {
                    // don't need a lock here since a true singleton is not required
                    s_exceptionTrace = new ExceptionTrace(defaultEventSource, Trace);
                }

                return s_exceptionTrace;
            }
        }

        public static EtwDiagnosticTrace Trace
        {
            get
            {
                if (s_diagnosticTrace == null)
                {
                    s_diagnosticTrace = InitializeTracing();
                }

                return s_diagnosticTrace;
            }
        }

        static EtwDiagnosticTrace InitializeTracing()
        {
            EtwDiagnosticTrace trace = new EtwDiagnosticTrace(defaultEventSource, EtwDiagnosticTrace.DefaultEtwProviderId);

            //if (null != trace.EtwProvider)
            //{
            //    trace.RefreshState += delegate ()
            //    {
            //        Fx.UpdateLevel();
            //    };
            //}
            //Fx.UpdateLevel(trace);
            return trace;
        }

        public static ExceptionHandler AsynchronousThreadExceptionHandler
        {
            get
            {
                return s_asynchronousThreadExceptionHandler;
            }

            set
            {
                s_asynchronousThreadExceptionHandler = value;
            }
        }

        public static void AssertAndThrow(bool condition, string description)
        {
            if (!condition)
            {
                AssertAndThrow(description);
            }
        }

        public static Exception AssertAndThrow(string description)
        {
            Assert(description);
            //TraceCore.ShipAssertExceptionMessage(Trace, description);
            throw new InternalException(description);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Exception AssertAndThrowFatal(string description)
        {
            Fx.Assert(description);
            //TraceCore.ShipAssertExceptionMessage(Trace, description);
            throw new FatalInternalException(description);
        }

        [Conditional("DEBUG")]
        public static void Assert(string description)
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
            Contract.Assert(false, description);
        }

        [Conditional("DEBUG")]
        public static void Assert(bool condition, string description)
        {
            if (!condition)
            {
                Assert(description);
            }
        }

        public static bool IsFatal(Exception exception)
        {
            while (exception != null)
            {
                if (exception is FatalException ||
                    (exception is OutOfMemoryException /*&& !(exception is InsufficientMemoryException)*/) ||
                    //exception is ThreadAbortException ||
                    exception is FatalInternalException)
                {
                    return true;
                }

                // These exceptions aren't themselves fatal, but since the CLR uses them to wrap other exceptions,
                // we want to check to see whether they've been used to wrap a fatal exception.  If so, then they
                // count as fatal.
                if (exception is TypeInitializationException ||
                    exception is TargetInvocationException)
                {
                    exception = exception.InnerException;
                }
                else if (exception is AggregateException)
                {
                    // AggregateExceptions have a collection of inner exceptions, which may themselves be other
                    // wrapping exceptions (including nested AggregateExceptions).  Recursively walk this
                    // hierarchy.  The (singular) InnerException is included in the collection.
                    ReadOnlyCollection<Exception> innerExceptions = ((AggregateException)exception).InnerExceptions;
                    foreach (Exception innerException in innerExceptions)
                    {
                        if (IsFatal(innerException))
                        {
                            return true;
                        }
                    }

                    break;
                }
                else
                {
                    break;
                }
            }

            return false;
        }

        [Serializable]
        internal class InternalException : SystemException
        {
            public InternalException(string description)
                : base($"ShipAssertExceptionMessage,{description}" /*InternalSR.ShipAssertExceptionMessage(description)*/)
            {
            }

            protected InternalException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }
        }

        [Serializable]
        internal class FatalInternalException : InternalException
        {
            public FatalInternalException(string description)
                : base(description)
            {
            }

            protected FatalInternalException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }
        }

        internal static byte[] AllocateByteArray(int size)
        {
            try
            {
                // Safe to catch OOM from this as long as the ONLY thing it does is a simple allocation of a primitive type (no method calls).
                return new byte[size];
            }
            catch (OutOfMemoryException exception)
            {
                // Convert OOM into an exception that can be safely handled by higher layers.
                throw Fx.Exception.AsError(exception);
                //new InsufficientMemoryException(InternalSR.BufferAllocationFailed(size), exception));
            }
        }

        public static AsyncCallback ThunkCallback(AsyncCallback callback)
        {
            return new AsyncThunk(callback).ThunkFrame;
        }

        public static Action<T1> ThunkCallback<T1>(Action<T1> callback)
        {
            return new ActionThunk<T1>(callback).ThunkFrame;
        }

        public static Action<T1, T2> ThunkCallback<T1, T2>(Action<T1, T2> callback)
        {
            return new ActionThunk<T1, T2>(callback).ThunkFrame;
        }

        public static Action<T1, T2, T3> ThunkCallback<T1, T2, T3>(Action<T1, T2, T3> callback)
        {
            return new ActionThunk<T1, T2, T3>(callback).ThunkFrame;
        }

        public static IOCompletionCallback ThunkCallback(IOCompletionCallback callback)
        {
            Fx.Assert(callback != null, "Trying to create a ThunkCallback with a null callback method");
            return (new IOCompletionThunk(callback)).ThunkFrame;
        }

        abstract class Thunk<T> where T : class
        {
            T callback;

            protected Thunk(T callback)
            {
                this.callback = callback;
            }

            internal T Callback
            {
                get
                {
                    return callback;
                }
            }
        }

        sealed class ActionThunk<T1> : Thunk<Action<T1>>
        {
            public ActionThunk(Action<T1> callback) : base(callback)
            {
            }

            public Action<T1> ThunkFrame
            {
                get
                {
                    return UnhandledExceptionFrame;
                }
            }

            void UnhandledExceptionFrame(T1 param1)
            {
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                    Callback(param1);
                }
                catch (Exception exception)
                {
                    if (!Fx.HandleAtThreadBase(exception))
                    {
                        throw;
                    }
                }
            }
        }

        sealed class ActionThunk<T1, T2> : Thunk<Action<T1, T2>>
        {
            public ActionThunk(Action<T1, T2> callback) : base(callback)
            {
            }

            public Action<T1, T2> ThunkFrame
            {
                get
                {
                    return UnhandledExceptionFrame;
                }
            }

            void UnhandledExceptionFrame(T1 param1, T2 param2)
            {
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                    Callback(param1, param2);
                }
                catch (Exception exception)
                {
                    if (!Fx.HandleAtThreadBase(exception))
                    {
                        throw;
                    }
                }
            }
        }

        sealed class ActionThunk<T1, T2, T3> : Thunk<Action<T1, T2, T3>>
        {
            public ActionThunk(Action<T1, T2, T3> callback) : base(callback)
            {
            }

            public Action<T1, T2, T3> ThunkFrame
            {
                get
                {
                    return UnhandledExceptionFrame;
                }
            }

            void UnhandledExceptionFrame(T1 param1, T2 param2, T3 param3)
            {
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                    Callback(param1, param2, param3);
                }
                catch (Exception exception)
                {
                    if (!Fx.HandleAtThreadBase(exception))
                    {
                        throw;
                    }
                }
            }
        }

        sealed class AsyncThunk : Thunk<AsyncCallback>
        {
            public AsyncThunk(AsyncCallback callback) : base(callback)
            {
            }

            public AsyncCallback ThunkFrame
            {
                get
                {
                    return new AsyncCallback(UnhandledExceptionFrame);
                }
            }

            void UnhandledExceptionFrame(IAsyncResult result)
            {
                // PrepareConstrainedRegions are in .net standard 1.7+
                //RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                    Callback(result);
                }
                catch (Exception exception)
                {
                    if (!Fx.HandleAtThreadBase(exception))
                    {
                        throw;
                    }
                }
            }
        }

        static void TraceExceptionNoThrow(Exception exception)
        {
            try
            {
                // This call exits the CER.  However, when still inside a catch, normal ThreadAbort is prevented.
                // Rude ThreadAbort will still be allowed to terminate processing.
                Fx.Exception.TraceUnhandledException(exception);
            }
            catch
            {
                // This empty catch is only acceptable because we are a) in a CER and b) processing an exception
                // which is about to crash the process anyway.
            }
        }

        static bool HandleAtThreadBase(Exception exception)
        {
            // This area is too sensitive to do anything but return.
            if (exception == null)
            {
                Fx.Assert("Null exception in HandleAtThreadBase.");
                return false;
            }

            TraceExceptionNoThrow(exception);

            try
            {
                ExceptionHandler handler = Fx.AsynchronousThreadExceptionHandler;
                return handler == null ? false : handler.HandleException(exception);
            }
            catch (Exception secondException)
            {
                // Don't let a new exception hide the original exception.
                TraceExceptionNoThrow(secondException);
            }

            return false;
        }

        public abstract class ExceptionHandler
        {
            public abstract bool HandleException(Exception exception);
        }

        // This can't derive from Thunk since T would be unsafe.
        unsafe sealed class IOCompletionThunk
        {
            IOCompletionCallback callback;

            public IOCompletionThunk(IOCompletionCallback callback)
            {
                this.callback = callback;
            }

            public IOCompletionCallback ThunkFrame
            {
                get
                {
                    return new IOCompletionCallback(UnhandledExceptionFrame);
                }
            }

            void UnhandledExceptionFrame(uint error, uint bytesRead, NativeOverlapped* nativeOverlapped)
            {
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                    callback(error, bytesRead, nativeOverlapped);
                }
                catch (Exception exception)
                {
                    if (!Fx.HandleAtThreadBase(exception))
                    {
                        throw;
                    }
                }
            }
        }
    }
}