// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;

namespace CoreWCF.Diagnostics
{
    // TODO: Work out how TraceUtility fits in with all the other exception and tracing classes
    internal static partial class TraceUtility
    {
        internal static void TraceUserCodeException(Exception e, MethodInfo method)
        {
            //if (DiagnosticUtility.ShouldTraceWarning)
            //{
            //    StringTraceRecord record = new StringTraceRecord("Comment",
            //        SR.Format(SR.SFxUserCodeThrewException, method.DeclaringType.FullName, method.Name));
            //    DiagnosticUtility.DiagnosticTrace.TraceEvent(TraceEventType.Warning,
            //        TraceCode.UnhandledExceptionInUserOperation, GenerateMsdnTraceCode(TraceCode.UnhandledExceptionInUserOperation),
            //        SR.Format(SR.TraceCodeUnhandledExceptionInUserOperation, method.DeclaringType.FullName, method.Name),
            //        record,
            //        e, null);
            //}
        }

        internal static void TraceDroppedMessage(Message requestMessage, EndpointDispatcher endpoint)
        {
            //if (DiagnosticUtility.ShouldTraceInformation)
            //{
            //    EndpointAddress endpointAddress = null;
            //    if (dispatcher != null)
            //    {
            //        endpointAddress = dispatcher.EndpointAddress;
            //    }
            //    TraceUtility.TraceEvent(TraceEventType.Information, TraceCode.DroppedAMessage,
            //        SR.Format(SR.TraceCodeDroppedAMessage), new MessageDroppedTraceRecord(message, endpointAddress));
            //}
        }
    }
}
