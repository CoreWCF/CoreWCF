// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels.Framing
{
    internal class ConnectionIdWrappingLogger : ILogger
    {
        private ILogger _innerLogger;
        private string _connectionId;

        public ConnectionIdWrappingLogger(ILogger innerLogger, string connectionId)
        {
            _innerLogger = innerLogger;
            _connectionId = connectionId;
        }
        public IDisposable BeginScope<TState>(TState state) => _innerLogger.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => _innerLogger.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            (TState state, string connectionId, Func<TState, Exception, string> origFormatter) newState = (state, _connectionId, formatter);
            _innerLogger.Log(logLevel, eventId, newState, exception, ConnectionIdFormatter<TState>);
        }

        private static string ConnectionIdFormatter<TState>((TState state, string connectionId, Func<TState, Exception, string> formatter) modifiedState, Exception exception)
        {
            var state = modifiedState.state;
            var connectionId = modifiedState.connectionId;
            var formatter = modifiedState.formatter;
            var formattedString = formatter(state, exception);
            return $"[{connectionId}] {formattedString}";
        }
    }

}
