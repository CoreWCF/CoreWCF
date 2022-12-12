// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO.Pipelines;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels.Framing
{
    internal static class FramingLoggingExtensions
    {
        private static Action<ILogger, string, int, string, Exception> s_connectionLogging = LoggerMessage.Define<string, int, string>(
            LogLevel.Trace,
            new EventId(1059, "ConnectionLogging"),
            "{method}[{byteCount}]{data}");

        public static void LogBytes(this ILogger logger, string method, int count, string data)
        {
            s_connectionLogging(logger, method, count, data, null);
        }
    }
}
