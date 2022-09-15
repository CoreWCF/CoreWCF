﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF.Metadata.Tests.Helpers;
using ServiceContract;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Metadata.Tests
{
    public class BasicHttpSimpleServiceTest
    {
        private readonly ITestOutputHelper _output;

        public BasicHttpSimpleServiceTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task BasicHttpRequestReplyEchoString()
        {
            await TestHelper.RunSingleWsdlTestAsync<SimpleEchoService, IEchoService>(new BasicHttpBinding(), _output);
        }
    }
}
