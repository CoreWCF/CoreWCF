// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF.Metadata.Tests.Helpers;
using ServiceContract;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Metadata.Tests
{
    public class NetTcpSimpleServiceTest
    {
        private readonly ITestOutputHelper _output;

        public NetTcpSimpleServiceTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task NetTcpBindingSecurityNoneRequestReplyEchoString()
        {
            await TestHelper.RunSingleWsdlTestAsync<SimpleEchoService, IEchoService>(new NetTcpBinding(SecurityMode.None), _output);
        }
    }
}
