// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Helpers
{
    public class ExceptionCapturingLoggerProvider : ILoggerProvider
    {
        private readonly XunitLoggerProvider _wrappedProvider;
        private readonly ConcurrentDictionary<string, ExceptionCapturingLogger> _loggers = new ConcurrentDictionary<string, ExceptionCapturingLogger>();
        public ExceptionCapturingLoggerProvider(XunitLoggerProvider wrappedProvider)
        {
            _wrappedProvider = wrappedProvider;
        }

        public void Dispose()
        { }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, new ExceptionCapturingLogger(_wrappedProvider?.CreateLogger(categoryName)));
        }

        public IEnumerable<TException> GetExceptionsLogged<TException>() where TException : Exception
        {
            return _loggers.SelectMany(l=>l.Value.ExceptionsLogged).OfType<TException>();
        }
    }
}
