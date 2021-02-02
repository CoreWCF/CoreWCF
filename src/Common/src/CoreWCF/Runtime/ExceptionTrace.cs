// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Reflection;
using CoreWCF.Runtime.Diagnostics;

namespace CoreWCF.Runtime
{
    internal class ExceptionTrace
    {
        private readonly string _eventSourceName;
        private readonly EtwDiagnosticTrace _diagnosticTrace;

        internal ExceptionTrace(string eventSourceName, EtwDiagnosticTrace diagnosticTrace)
        {
            Fx.Assert(diagnosticTrace != null, "'diagnosticTrace' MUST NOT be NULL.");

            _eventSourceName = eventSourceName;
            _diagnosticTrace = diagnosticTrace;
        }

        public void AsWarning(Exception exception)
        {
            //Traces a warning trace message
            //TraceCore.HandledExceptionWarning(this.diagnosticTrace, exception != null ? exception.ToString() : string.Empty, exception);
        }

        public Exception AsError(Exception exception)
        {
            // AggregateExceptions are automatically unwrapped.
            if (exception is AggregateException aggregateException)
            {
                return AsError<Exception>(aggregateException);
            }

            // TargetInvocationExceptions are automatically unwrapped.
            if (exception is TargetInvocationException targetInvocationException && targetInvocationException.InnerException != null)
            {
                return AsError(targetInvocationException.InnerException);
            }

            return TraceException<Exception>(exception);
        }

        public Exception AsError<TPreferredException>(AggregateException aggregateException)
        {
            return AsError<TPreferredException>(aggregateException, _eventSourceName);
        }

        public Exception AsError<TPreferredException>(AggregateException aggregateException, string eventSource)
        {
            Fx.Assert(aggregateException != null, "aggregateException cannot be null.");

            // If aggregateException contains any fatal exceptions, return it directly
            // without tracing it or any inner exceptions.
            if (Fx.IsFatal(aggregateException))
            {
                return aggregateException;
            }

            // Collapse possibly nested graph into a flat list.
            // Empty inner exception list is unlikely but possible via public api.
            System.Collections.ObjectModel.ReadOnlyCollection<Exception> innerExceptions = aggregateException.Flatten().InnerExceptions;
            if (innerExceptions.Count == 0)
            {
                return TraceException(aggregateException, eventSource);
            }

            // Find the first inner exception, giving precedence to TPreferredException
            Exception favoredException = null;
            foreach (Exception nextInnerException in innerExceptions)
            {
                // AggregateException may wrap TargetInvocationException, so unwrap those as well

                Exception innerException = (nextInnerException is TargetInvocationException targetInvocationException && targetInvocationException.InnerException != null)
                                                ? targetInvocationException.InnerException
                                                : nextInnerException;

                if (innerException is TPreferredException && favoredException == null)
                {
                    favoredException = innerException;
                }

                // All inner exceptions are traced
                TraceException<Exception>(innerException, eventSource);
            }

            if (favoredException == null)
            {
                Fx.Assert(innerExceptions.Count > 0, "InnerException.Count is known to be > 0 here.");
                favoredException = innerExceptions[0];
            }

            return favoredException;
        }

        public ArgumentException Argument(string paramName, string message)
        {
            return TraceException(new ArgumentException(message, paramName));
        }

        public ArgumentNullException ArgumentNull(string paramName)
        {
            return TraceException(new ArgumentNullException(paramName));
        }

        private TException TraceException<TException>(TException exception)
            where TException : Exception
        {
            return TraceException<TException>(exception, _eventSourceName);
        }

        private TException TraceException<TException>(TException exception, string eventSource)
    where TException : Exception
        {
            //if (TraceCore.ThrowingExceptionIsEnabled(this.diagnosticTrace))
            //{
            //    TraceCore.ThrowingException(this.diagnosticTrace, eventSource, exception != null ? exception.ToString() : string.Empty, exception);
            //}

            //BreakOnException(exception);

            return exception;
        }

        public ArgumentOutOfRangeException ArgumentOutOfRange(string paramName, object actualValue, string message)
        {
            return TraceException(new ArgumentOutOfRangeException(paramName, actualValue, message));
        }

        public void TraceUnhandledException(Exception exception)
        {
            //TraceCore.UnhandledException(this.diagnosticTrace, exception != null ? exception.ToString() : string.Empty, exception);
        }

        public void TraceHandledException(Exception exception, TraceEventType traceEventType)
        {
            //switch (traceEventType)
            //{
            //    case TraceEventType.Error:
            //        if (TraceCore.HandledExceptionErrorIsEnabled(this.diagnosticTrace))
            //        {
            //            TraceCore.HandledExceptionError(this.diagnosticTrace, exception != null ? exception.ToString() : string.Empty, exception);
            //        }
            //        break;
            //    case TraceEventType.Warning:
            //        if (TraceCore.HandledExceptionWarningIsEnabled(this.diagnosticTrace))
            //        {
            //            TraceCore.HandledExceptionWarning(this.diagnosticTrace, exception != null ? exception.ToString() : string.Empty, exception);
            //        }
            //        break;
            //    case TraceEventType.Verbose:
            //        if (TraceCore.HandledExceptionVerboseIsEnabled(this.diagnosticTrace))
            //        {
            //            TraceCore.HandledExceptionVerbose(this.diagnosticTrace, exception != null ? exception.ToString() : string.Empty, exception);
            //        }
            //        break;
            //    default:
            //        if (TraceCore.HandledExceptionIsEnabled(this.diagnosticTrace))
            //        {
            //            TraceCore.HandledException(this.diagnosticTrace, exception != null ? exception.ToString() : string.Empty, exception);
            //        }
            //        break;
            //}
        }
    }
}
