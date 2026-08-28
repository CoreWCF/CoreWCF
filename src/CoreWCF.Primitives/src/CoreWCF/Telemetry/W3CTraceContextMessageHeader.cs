// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF.Telemetry;

internal static class W3CTraceContextMessageHeader
{
    internal const string Namespace = "https://www.w3.org/TR/trace-context/";

    private const string TraceParentHeaderName = "traceparent";
    private const string TraceStateHeaderName = "tracestate";

    public static bool TryExtract(Message message, out ActivityContext parentContext)
    {
        parentContext = default;

        if (message.Version == MessageVersion.None)
        {
            return false;
        }

        try
        {
            if (!TryReadHeader(message.Headers, TraceParentHeaderName, out string traceParent))
            {
                return false;
            }

            TryReadHeader(message.Headers, TraceStateHeaderName, out string traceState);
            return ActivityContext.TryParse(traceParent, traceState, isRemote: true, out parentContext);
        }
        catch (Exception exception) when (!Fx.IsFatal(exception))
        {
            return false;
        }
    }

    private static bool TryReadHeader(MessageHeaders headers, string name, out string value)
    {
        value = null;
        int headerIndex = headers.FindHeader(name, Namespace);
        if (headerIndex < 0)
        {
            return false;
        }

        using var reader = headers.GetReaderAtHeader(headerIndex);
        reader.Read();
        value = reader.ReadContentAsString();
        return true;
    }
}
