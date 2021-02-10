// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Runtime
{
    internal sealed class TraceSR
    {
        internal const string GenericCallbackException = "A user callback threw an exception.  Check the exception stack and inner exception to determine the callback that failed.";

        /* Here are the TraceSR resource strings which come from SMDiagnostics
         * TODO: Convert this to use the resource mechanism so localization can happen
        ActivityBoundary=Activity boundary.
        ThrowingException=Throwing an exception.
        TraceHandledException=Handling an exception.
        TraceCodeAppDomainUnload=AppDomain unloading.
        TraceCodeEventLog=Wrote to the EventLog.
        TraceCodeTraceTruncatedQuotaExceeded=A trace size quota was exceeded. The trace was truncated.
        UnhandledException=Unhandled exception
        WriteCharsInvalidContent=The contents of the buffer passed to PlainXmlWriter.WriteChars are invalid.
        GenericCallbackException=A user callback threw an exception.  Check the exception stack and inner exception to determine the callback that failed.
        StringNullOrEmpty=The input string parameter is either null or empty.
        */
    }
}