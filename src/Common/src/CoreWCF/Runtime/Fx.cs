// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime.Diagnostics;

namespace CoreWCF.Runtime
{
    internal static class Fx
    {
        private const string defaultEventSource = "Microsoft.Runtime";
        private static ExceptionTrace s_exceptionTrace;
        private static EtwDiagnosticTrace s_diagnosticTrace;

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

        private static EtwDiagnosticTrace InitializeTracing()
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

        public static ExceptionHandler AsynchronousThreadExceptionHandler { get; set; }

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
            Assert(description);
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
                : base($"ShipAssertExceptionMessage,{description}" /*InternalSRCommon.ShipAssertExceptionMessage(description)*/)
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
                throw Exception.AsError(exception);
                //new InsufficientMemoryException(InternalSRCommon.BufferAllocationFailed(size), exception));
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
            Assert(callback != null, "Trying to create a ThunkCallback with a null callback method");
            return (new IOCompletionThunk(callback)).ThunkFrame;
        }

        private abstract class Thunk<T> where T : class
        {
            protected Thunk(T callback)
            {
                Callback = callback;
            }

            internal T Callback { get; private set; }
        }

        private sealed class ActionThunk<T1> : Thunk<Action<T1>>
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

            private void UnhandledExceptionFrame(T1 param1)
            {
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                    Callback(param1);
                }
                catch (Exception exception)
                {
                    if (!HandleAtThreadBase(exception))
                    {
                        throw;
                    }
                }
            }
        }

        private sealed class ActionThunk<T1, T2> : Thunk<Action<T1, T2>>
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

            private void UnhandledExceptionFrame(T1 param1, T2 param2)
            {
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                    Callback(param1, param2);
                }
                catch (Exception exception)
                {
                    if (!HandleAtThreadBase(exception))
                    {
                        throw;
                    }
                }
            }
        }

        private sealed class ActionThunk<T1, T2, T3> : Thunk<Action<T1, T2, T3>>
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

            private void UnhandledExceptionFrame(T1 param1, T2 param2, T3 param3)
            {
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                    Callback(param1, param2, param3);
                }
                catch (Exception exception)
                {
                    if (!HandleAtThreadBase(exception))
                    {
                        throw;
                    }
                }
            }
        }

        private sealed class AsyncThunk : Thunk<AsyncCallback>
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

            private void UnhandledExceptionFrame(IAsyncResult result)
            {
                // PrepareConstrainedRegions are in .net standard 1.7+
                //RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                    Callback(result);
                }
                catch (Exception exception)
                {
                    if (!HandleAtThreadBase(exception))
                    {
                        throw;
                    }
                }
            }
        }

        private static void TraceExceptionNoThrow(Exception exception)
        {
            try
            {
                // This call exits the CER.  However, when still inside a catch, normal ThreadAbort is prevented.
                // Rude ThreadAbort will still be allowed to terminate processing.
                Exception.TraceUnhandledException(exception);
            }
            catch
            {
                // This empty catch is only acceptable because we are a) in a CER and b) processing an exception
                // which is about to crash the process anyway.
            }
        }

        private static bool HandleAtThreadBase(Exception exception)
        {
            // This area is too sensitive to do anything but return.
            if (exception == null)
            {
                Assert("Null exception in HandleAtThreadBase.");
                return false;
            }

            TraceExceptionNoThrow(exception);

            try
            {
                ExceptionHandler handler = AsynchronousThreadExceptionHandler;
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
        private sealed unsafe class IOCompletionThunk
        {
            private readonly IOCompletionCallback _callback;

            public IOCompletionThunk(IOCompletionCallback callback)
            {
                _callback = callback;
            }

            public IOCompletionCallback ThunkFrame
            {
                get
                {
                    return new IOCompletionCallback(UnhandledExceptionFrame);
                }
            }

            private void UnhandledExceptionFrame(uint error, uint bytesRead, NativeOverlapped* nativeOverlapped)
            {
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                    _callback(error, bytesRead, nativeOverlapped);
                }
                catch (Exception exception)
                {
                    if (!HandleAtThreadBase(exception))
                    {
                        throw;
                    }
                }
            }
        }
    }
}
