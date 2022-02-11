// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CoreWCF.Runtime;

namespace CoreWCF
{
    internal class DiagnosticUtility
    {
        private static ExceptionUtility s_exceptionUtility = (ExceptionUtility)null;
        private static readonly object s_lockObject = new object();

        internal static ExceptionUtility ExceptionUtility
        {
            get
            {
                return s_exceptionUtility ?? GetExceptionUtility();
            }
        }

        private static ExceptionUtility GetExceptionUtility()
        {
            lock (s_lockObject)
            {
                if (s_exceptionUtility == null)
                {
                    // TODO: Make this generic shared code used by multiple assemblies
                    //exceptionUtility = new ExceptionUtility("System.ServiceModel", "System.ServiceModel 4.0.0.0", (object)DiagnosticUtility.diagnosticTrace, (object)FxTrace.Exception);
                    s_exceptionUtility = new ExceptionUtility();
                }
            }

            return s_exceptionUtility;
        }

        internal static void TraceHandledException(Exception exception, TraceEventType traceEventType)
        {
            Fx.Exception.TraceHandledException(exception, traceEventType);
        }

        [Conditional("DEBUG")]
        internal static void DebugAssert(bool condition, string message)
        {
            if (!condition)
            {
                DebugAssert(message);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Conditional("DEBUG")]
        internal static void DebugAssert(string message)
        {
            Fx.Assert(message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Exception FailFast(string message)
        {
            try
            {
                try
                {
                    ExceptionUtility.TraceFailFast(message);
                }
                finally
                {
                    Environment.FailFast(message);
                }
            }
            catch
            {
            }
            Environment.FailFast(message);
            return (Exception)null;
        }
    }

    internal class ExceptionUtility
    {
        internal ArgumentException ThrowHelperArgument(string message)
        {
            return (ArgumentException)ThrowHelperError(new ArgumentException(message));
        }

        internal ArgumentException ThrowHelperArgument(string paramName, string message)
        {
            return (ArgumentException)ThrowHelperError(new ArgumentException(message, paramName));
        }

        internal ArgumentNullException ThrowHelperArgumentNull(string paramName)
        {
            return (ArgumentNullException)ThrowHelperError(new ArgumentNullException(paramName));
        }

        internal Exception ThrowHelperFatal(string message, Exception innerException)
        {
            return ThrowHelperError(new FatalException(message, innerException));
        }

        internal Exception ThrowHelperError(Exception exception)
        {
            return ThrowHelper(exception, TraceEventType.Error);
        }

        internal Exception ThrowHelperWarning(Exception exception)
        {
            return ThrowHelper(exception, TraceEventType.Warning);
        }

        internal Exception ThrowHelper(Exception exception, TraceEventType eventType)
        {
            return ThrowHelper(exception, eventType, null);
        }

        internal Exception ThrowHelper(Exception exception, TraceEventType eventType, TraceRecord extendedData)
        {
            //if ((_diagnosticTrace == null ? 0 : (_diagnosticTrace.ShouldTrace(eventType) ? 1 : 0)) != 0)
            //{
            //    using (
            //        ExceptionUtility.useStaticActivityId
            //            ? Activity.CreateActivity(ExceptionUtility.activityId)
            //            : (Activity)null)
            //        _diagnosticTrace.TraceEvent(eventType, 131075,
            //            LegacyDiagnosticTrace.GenerateMsdnTraceCode("System.ServiceModel.Diagnostics",
            //                "ThrowingException"), TraceSRCommon.Format("ThrowingException"), extendedData, exception,
            //            (object)null);
            //    IDictionary data = exception.Data;
            //    if (data != null && !data.IsReadOnly && !data.IsFixedSize)
            //    {
            //        object obj =
            //            data[(object)"System.ServiceModel.Diagnostics.ExceptionUtility.ExceptionStackAsString"];
            //        string str1 = obj == null ? "" : obj as string;
            //        if (str1 != null)
            //        {
            //            string stackTrace = exception.StackTrace;
            //            if (!string.IsNullOrEmpty(stackTrace))
            //            {
            //                string str2 = str1 + (str1.Length == 0 ? "" : Environment.NewLine) + "throw" +
            //                              Environment.NewLine + stackTrace + Environment.NewLine + "catch" +
            //                              Environment.NewLine;
            //                data[(object)"System.ServiceModel.Diagnostics.ExceptionUtility.ExceptionStackAsString"]
            //                    = (object)str2;
            //            }
            //        }
            //    }
            //}
            //this.exceptionTrace.TraceEtwException(exception, eventType);
            return exception;
        }

        internal Exception ThrowHelperCallback(Exception innerException)
        {
            return ThrowHelperCallback(TraceSR.GenericCallbackException, innerException);
        }

        internal Exception ThrowHelperCallback(string message, Exception innerException)
        {
            return ThrowHelperCritical(new CallbackException(message, innerException));
        }

        internal Exception ThrowHelperCritical(Exception exception)
        {
            return ThrowHelper(exception, TraceEventType.Critical);
        }

        internal class TraceRecord
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void TraceFailFast(string message)
        {
            //Microsoft.Runtime.Diagnostics.EventLogger logger = null;
            //try
            //{
            //    logger = new Microsoft.Runtime.Diagnostics.EventLogger(this.eventSourceName, this.diagnosticTrace);
            //}
            //finally
            //{
            //    TraceFailFast(message, logger);
            //}
        }

        internal Exception ThrowHelperArgumentNull(string paramName, string message)
        {
            return (ArgumentNullException)ThrowHelperError(new ArgumentNullException(paramName, message));
        }

        public Exception ThrowHelperInvalidOperation(string message)
        {
            return ThrowHelperError(new InvalidOperationException(message));
        }
    }
}
