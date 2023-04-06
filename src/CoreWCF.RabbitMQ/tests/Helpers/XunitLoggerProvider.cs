// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace CoreWCF.RabbitMQ.Tests.Helpers
{
    public class XunitLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly string _callerMethodName;

        public XunitLoggerProvider(ITestOutputHelper testOutputHelper, string callerMethodName)
        {
            _testOutputHelper = testOutputHelper;
            _callerMethodName = callerMethodName;
        }

        public ILogger CreateLogger(string categoryName)
            => new XunitLogger(_testOutputHelper, categoryName, _callerMethodName);

        public void Dispose()
        { }
    }
}
