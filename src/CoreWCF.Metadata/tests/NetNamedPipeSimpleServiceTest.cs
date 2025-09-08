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
    public class NetNamedPipeSimpleServiceTest
    {
        private readonly ITestOutputHelper _output;

        public NetNamedPipeSimpleServiceTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [WindowsOnlyFact]
        public async Task NetNamedPipeBindingSecurityNoneRequestReplyEchoString()
            {
            // The NetNamedPipeBinding is only available on Windows, so we use a WindowsOnlyFact attribute.
#pragma warning disable CA1416 // Validate platform compatibility
            await TestHelper.RunSingleWsdlTestAsync<SimpleEchoService, IEchoService>(new NetNamedPipeBinding(NetNamedPipeSecurityMode.None), _output);
#pragma warning restore CA1416 // Validate platform compatibility
        }

        [WindowsOnlyFact]
        public async Task NetNamedPipeBindingSecurityTransportRequestReplyEchoString()
        {
            // The NetNamedPipeBinding is only available on Windows, so we use a WindowsOnlyFact attribute.
#pragma warning disable CA1416 // Validate platform compatibility
            await TestHelper.RunSingleWsdlTestAsync<SimpleEchoService, IEchoService>(new NetNamedPipeBinding(NetNamedPipeSecurityMode.Transport), _output);
#pragma warning restore CA1416 // Validate platform compatibility
        }

        [WindowsOnlyFact]
        public async Task NetNamedPipeBindingSecurityTransportExplicitUpnRequestReplyEchoString()
        {
            // The NetNamedPipeBinding is only available on Windows, so we use a WindowsOnlyFact attribute.
#pragma warning disable CA1416 // Validate platform compatibility
            await TestHelper.RunSingleWsdlTestAsync<SimpleEchoService, IEchoService>(new NetNamedPipeBinding(NetNamedPipeSecurityMode.Transport), _output, null, null, null,
                (serviceHostBase) =>
                {
                    foreach (var endpoint in serviceHostBase.Description.Endpoints)
                    {
                        endpoint.Address = new EndpointAddress(endpoint.Address.Uri, new UpnEndpointIdentity("user@corewcf.net"));
                    }
                });
#pragma warning restore CA1416 // Validate platform compatibility
        }
    }
}
