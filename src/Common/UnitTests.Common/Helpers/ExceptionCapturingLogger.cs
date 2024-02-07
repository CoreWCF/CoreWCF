// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Helpers
{
    public class ExceptionCapturingLogger : ILogger
    {
        private readonly ILogger _wrappedLogger;
        private readonly ConcurrentBag<Exception> _exceptionsLogged = new();
        public ExceptionCapturingLogger(ILogger wrappedLogger)
        {
            _wrappedLogger = wrappedLogger;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            _wrappedLogger?.Log(logLevel, eventId, state, exception, formatter);
            if (exception != null)
            {
                _exceptionsLogged.Add(exception);
            }
        }

        public bool IsEnabled(LogLevel logLevel) => _wrappedLogger?.IsEnabled(logLevel) ?? (int)logLevel>= (int)LogLevel.Error;

        public IDisposable BeginScope<TState>(TState state) => NoopDisposable.Instance;
        public IEnumerable<Exception> ExceptionsLogged => _exceptionsLogged;
        private class NoopDisposable : IDisposable
        {
            public static NoopDisposable Instance = new NoopDisposable();
            public void Dispose()
            { }
        }
    }
}
