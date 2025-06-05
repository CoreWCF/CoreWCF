// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF.Metadata.Tests.Helpers;
using CoreWCF.Channels;
using ServiceContract;
using Services;
using Xunit;
using Xunit.Abstractions;
using System.Collections.Generic;
using System;

namespace CoreWCF.Metadata.Tests
{
    public class HelpPageTests
    {
        private readonly ITestOutputHelper _output;

        public HelpPageTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task HttpOnlyEndpointGetHttpHelpPage()
        {
            await TestHelper.RunHelpPageTestAsync<SimpleEchoService, IEchoService>(new BasicHttpBinding(BasicHttpSecurityMode.None), _output);
        }

        [Fact]
        public async Task HttpsOnlyEndpointGetHttpsHelpPage()
        {
            await TestHelper.RunHelpPageTestAsync<SimpleEchoService, IEchoService>(new BasicHttpBinding(BasicHttpSecurityMode.Transport), _output);
        }

        [Fact]
        public async Task HttpAndHttpsEndpointsGetHelpPages()
        {
            var bindingEndpointMap = new Dictionary<string, Binding>
            {
                ["insecure"] = new BasicHttpBinding(BasicHttpSecurityMode.None),
                ["secure"] = new BasicHttpBinding(BasicHttpSecurityMode.Transport)
            };
            await TestHelper.RunMultipleEndpointsHelpPageTestAsync<SimpleEchoService, IEchoService>(bindingEndpointMap, new Uri[] { new Uri("http://localhost/wsHttp"), new Uri("https://localhost/wsHttp") }, _output);
        }
    }
}
