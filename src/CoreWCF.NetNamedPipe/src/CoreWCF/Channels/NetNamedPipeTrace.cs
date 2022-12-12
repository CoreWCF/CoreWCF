// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels
{
    internal class NetNamedPipeTrace : ILogger
    {
        private readonly ILogger _generalLogger;
        private readonly ILogger _connectionLogger;

        public NetNamedPipeTrace(ILoggerFactory loggerFactory, ILogger connectionLogger)
        {
            _generalLogger = loggerFactory.CreateLogger("CoreWCF.NetNamedPipe");
            _connectionLogger = connectionLogger;
        }

        public void ConnectionStart(string connectionId)
        {
            ConnectionsLog.ConnectionStart(_connectionLogger, connectionId);
        }

        public void ConnectionStop(string connectionId)
        {
            GeneralLog.ConnectionStop(_generalLogger, connectionId);
        }

        public void LogBytes(string method, int count, string data)
        {
            ConnectionsLog.ConnectionLogging(_connectionLogger, method, count, data);
        }

        public void ConnectionAccepted(string connectionId)
        {
            ConnectionsLog.ConnectionAccepted(_connectionLogger, connectionId);
        }

        public void ConnectionError(string connectionId, Exception ex)
        {
            ConnectionsLog.ConnectionError(_connectionLogger, connectionId, ex);
        }

        public void ConnectionReadEnd(string connectionId)
        {
            ConnectionsLog.ConnectionReadEnd(_connectionLogger, connectionId);
        }

        public void ConnectionPause(string connectionId)
        {
            ConnectionsLog.ConnectionPause(_connectionLogger, connectionId);
        }

        public void ConnectionResume(string connectionId)
        {
            ConnectionsLog.ConnectionResume(_connectionLogger, connectionId);
        }

        internal void ConnectionDisconnect(string connectionId, string reason)
        {
            ConnectionsLog.ConnectionDisconnect(_connectionLogger, connectionId, reason);
        }

        internal void LogConnectionError(int eventId, Exception exception, string message)
        {
            _connectionLogger.LogError(eventId, exception, message);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            => _generalLogger.Log(logLevel, eventId, state, exception, formatter);

        public bool IsEnabled(LogLevel logLevel) => _generalLogger.IsEnabled(logLevel);

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => _generalLogger.BeginScope(state);

        private static partial class ConnectionsLog
        {
            // TODO: Replace with LoggerMessageAttribute once moved to .NET 6

            //[LoggerMessage(1, LogLevel.Debug, @"Connection id ""{ConnectionId}"" started.", EventName = "ConnectionStart")]
            //public static partial void ConnectionStart(ILogger logger, string connectionId);
            private static Action<ILogger, string, Exception> s_connectionStart = LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(1, "ConnectionStart"),
                @"Connection id ""{ConnectionId}"" started.");
            public static void ConnectionStart(ILogger logger, string connectionId) => s_connectionStart(logger, connectionId, null);

            //[LoggerMessage(2, LogLevel.Debug, @"Connection id ""{ConnectionId}"" unexpected error.", EventName = "ConnectionError", SkipEnabledCheck = true)]
            //private static partial void ConnectionError(ILogger logger, string connectionId, Exception ex);
            private static Action<ILogger, string, Exception> s_connectionError = LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(2, "ConnectionError"),
                @"Connection id ""{ConnectionId}"" unexpected error.");
            public static void ConnectionError(ILogger logger, string connectionId, Exception ex) => s_connectionError(logger, connectionId, ex);

            //[LoggerMessage(4, LogLevel.Debug, @"Connection id ""{ConnectionId}"" paused.", EventName = "ConnectionPause", SkipEnabledCheck = true)]
            //private static partial void ConnectionPause(ILogger logger, string connectionId);
            private static Action<ILogger, string, Exception> s_connectionPause = LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(4, "ConnectionPause"),
                @"Connection id ""{ConnectionId}"" paused.");
            public static void ConnectionPause(ILogger logger, string connectionId) => s_connectionPause(logger, connectionId, null);

            //[LoggerMessage(5, LogLevel.Debug, @"Connection id ""{ConnectionId}"" resumed.", EventName = "ConnectionResume", SkipEnabledCheck = true)]
            //private static partial void ConnectionResume(ILogger logger, string connectionId);
            private static Action<ILogger, string, Exception> s_connectionResume = LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(5, "ConnectionResume"),
                @"Connection id ""{ConnectionId}"" resumed.");
            public static void ConnectionResume(ILogger logger, string connectionId) => s_connectionResume(logger, connectionId, null);

            //[LoggerMessage(6, LogLevel.Debug, @"Connection id ""{ConnectionId}"" received end of stream.", EventName = "ConnectionReadEnd", SkipEnabledCheck = true)]
            //public static partial void ConnectionReadEnd(ILogger logger, string connectionId);
            private static Action<ILogger, string, Exception> s_connectionReadEnd = LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(6, "ConnectionReadEnd"),
                @"Connection id ""{ConnectionId}"" received end of stream.");
            public static void ConnectionReadEnd(ILogger logger, string connectionId) => s_connectionReadEnd(logger, connectionId, null);

            //[LoggerMessage(7, LogLevel.Debug, @"Connection id ""{ConnectionId}"" disconnecting stream because: ""{Reason}""", EventName = "ConnectionDisconnect", SkipEnabledCheck = true)]
            //private static partial void ConnectionDisconnect(ILogger logger, string connectionId, string reason);
            private static Action<ILogger, string, string, Exception> s_connectionDisconnect = LoggerMessage.Define<string, string>(
                LogLevel.Debug,
                new EventId(7, "ConnectionDisconnect"),
                @"Connection id ""{ConnectionId}"" disconnecting stream because: ""{Reason}""");
            public static void ConnectionDisconnect(ILogger logger, string connectionId, string reason) => s_connectionDisconnect(logger, connectionId, reason, null);

            //[LoggerMessage(1059, LogLevel.Trace, "{method}[{byteCount}]{data}", EventName = "ConnectionLogging")]
            //public static partial void LogBytes(ILogger logger, string method, int count, string data);
            private static Action<ILogger, string, int, string, Exception> s_connectionLogging = LoggerMessage.Define<string, int, string>(
                LogLevel.Trace,
                new EventId(1059, "ConnectionLogging"),
                "{method}[{byteCount}]{data}");
            public static void ConnectionLogging(ILogger logger, string method, int count, string data) => s_connectionLogging(logger, method, count, data, null);

            //[LoggerMessage(4, LogLevel.Debug, @"Connection id ""{ConnectionId}"" paused.", EventName = "ConnectionPause")]
            //public static partial void ConnectionPause(ILogger logger, string connectionId);

            //[LoggerMessage(5, LogLevel.Debug, @"Connection id ""{ConnectionId}"" resumed.", EventName = "ConnectionResume")]
            //public static partial void ConnectionResume(ILogger logger, string connectionId);

            //[LoggerMessage(9, LogLevel.Debug, @"Connection id ""{ConnectionId}"" completed keep alive response.", EventName = "ConnectionKeepAlive")]
            //public static partial void ConnectionKeepAlive(ILogger logger, string connectionId);

            //[LoggerMessage(10, LogLevel.Debug, @"Connection id ""{ConnectionId}"" disconnecting.", EventName = "ConnectionDisconnect")]
            //public static partial void ConnectionDisconnect(ILogger logger, string connectionId);

            //[LoggerMessage(16, LogLevel.Debug, "Some connections failed to close gracefully during server shutdown.", EventName = "NotAllConnectionsClosedGracefully")]
            //public static partial void NotAllConnectionsClosedGracefully(ILogger logger);

            //[LoggerMessage(21, LogLevel.Debug, "Some connections failed to abort during server shutdown.", EventName = "NotAllConnectionsAborted")]
            //public static partial void NotAllConnectionsAborted(ILogger logger);

            //[LoggerMessage(24, LogLevel.Warning, @"Connection id ""{ConnectionId}"" rejected because the maximum number of concurrent connections has been reached.", EventName = "ConnectionRejected")]
            //public static partial void ConnectionRejected(ILogger logger, string connectionId);

            //[LoggerMessage(34, LogLevel.Information, @"Connection id ""{ConnectionId}"", Request id ""{TraceIdentifier}"": the application aborted the connection.", EventName = "ApplicationAbortedConnection")]
            //public static partial void ApplicationAbortedConnection(ILogger logger, string connectionId, string traceIdentifier);

            //[LoggerMessage(39, LogLevel.Debug, @"Connection id ""{ConnectionId}"" accepted.", EventName = "ConnectionAccepted")]
            //public static partial void ConnectionAccepted(ILogger logger, string connectionId);
            private static Action<ILogger, string, Exception> s_connectionAccepted = LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(39, "ConnectionAccepted"),
                @"Connection id ""{ConnectionId}"" accepted.");
            public static void ConnectionAccepted(ILogger logger, string connectionId) => s_connectionAccepted(logger, connectionId, null);

            //// Highest shared ID is 63. New consecutive IDs start at 64


        }

        private static partial class GeneralLog
        {
            //[LoggerMessage(2, LogLevel.Debug, @"Connection id ""{ConnectionId}"" stopped.", EventName = "ConnectionStop")]
            //public static partial void ConnectionStop(ILogger logger, string connectionId);
            private static Action<ILogger, string, Exception> s_connectionStop = LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(2, "ConnectionStop"),
                @"Connection id ""{ConnectionId}"" stopped.");
            public static void ConnectionStop(ILogger logger, string connectionId) => s_connectionStop(logger, connectionId, null);
        }
    }
}
