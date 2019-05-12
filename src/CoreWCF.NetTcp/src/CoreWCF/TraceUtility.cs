using CoreWCF.Channels;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF
{
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
            return exception;
        }
    }
}
