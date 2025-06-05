// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Metadata.Tests.Helpers;
using ServiceContract;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Metadata.Tests
{
    public class DiscoTests
    {
        private readonly ITestOutputHelper _output;

        public DiscoTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task HttpGetDiscoDocument()
        {
            await TestHelper.RunDiscoTestAsync<SimpleEchoService, IEchoService>(new BasicHttpBinding(BasicHttpSecurityMode.None), _output);
        }

        [Fact]
        public async Task HttpsGetDiscoDocument()
        {
            await TestHelper.RunDiscoTestAsync<SimpleEchoService, IEchoService>(new BasicHttpBinding(BasicHttpSecurityMode.Transport), _output);
        }
    }
}
