using System;
using System.Reflection;
using Microsoft.Runtime;
using Microsoft.ServiceModel.Channels;
using Microsoft.ServiceModel.Dispatcher;

namespace Microsoft.ServiceModel.Diagnostics
{
    // TODO: Work out how TraceUtility fits in with all the other exception and tracing classes
    internal static class TraceUtility
    {
        internal static Exception ThrowHelperError(Exception exception, Message message)
        {
            // If the message is closed, we won't get an activity
            //Guid activityId = TraceUtility.ExtractActivityId(message);
            //if (DiagnosticUtility.ShouldTraceError)
            //{
            //    DiagnosticUtility.DiagnosticTrace.TraceEvent(TraceEventType.Error, TraceCode.ThrowingException, GenerateMsdnTraceCode(TraceCode.ThrowingException),
            //        TraceSR.Format(TraceSR.ThrowingException), null, exception, activityId, null);
            //}
            return exception;
        }

        internal static Exception ThrowHelperError(Exception exception, Guid activityId, object source)
        {
            //if (DiagnosticUtility.ShouldTraceError)
            //{
            //    DiagnosticUtility.DiagnosticTrace.TraceEvent(TraceEventType.Error, TraceCode.ThrowingException, GenerateMsdnTraceCode(TraceCode.ThrowingException),
            //        TraceSR.Format(TraceSR.ThrowingException), null, exception, activityId, source);
            //}
            return exception;
        }

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