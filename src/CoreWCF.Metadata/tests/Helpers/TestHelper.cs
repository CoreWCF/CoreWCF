// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoreWCF.Channels;
using Helpers;
using Microsoft.AspNetCore.Hosting;
using Xunit.Abstractions;

namespace CoreWCF.Metadata.Tests.Helpers
{
    internal static class TestHelper
    {
        private const string EndpointRelativePath = "/endpointAddress.svc";
        internal static async Task RunSingleWsdlTestAsync<TService, TContract>(Binding binding, ITestOutputHelper output,
                [System.Runtime.CompilerServices.CallerMemberName] string callerMethodName = "",
                [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "") where TService : class
        {
            IWebHost host = ServiceHelper.CreateHttpWebHostBuilderWithMetadata<TService, TContract>(
                binding,
                EndpointRelativePath,
                output)
                .Build();
            string wsdlAddress = "http://localhost:8080/endpointAddress.svc";
            if ("https".Equals(binding.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                wsdlAddress = "https://localhost:8443/endpointAddress.svc";
            }
            using (host)
            {
                await host.StartAsync();
                var endpointAddress = ServiceHelper.GetEndpointBaseAddress(host, binding) + EndpointRelativePath;
                await WsdlHelper.ValidateSingleWsdl(wsdlAddress, endpointAddress, callerMethodName, sourceFilePath);
            }
        }

        internal static async Task RunSingleWsdlTestAsync<TService, TContract>(IDictionary<string, Binding> bindingEndpointMap, Uri[] baseAddresses, ITestOutputHelper output,
                [System.Runtime.CompilerServices.CallerMemberName] string callerMethodName = "",
                [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "") where TService : class
        {
            IWebHost host = ServiceHelper.CreateHttpWebHostBuilderWithMetadata<TService, TContract>(
                bindingEndpointMap,
                baseAddresses,
                output)
                .Build();

            using (host)
            {
                await host.StartAsync();
                var serverListeningAddress = ServiceHelper.GetEndpointBaseAddress(host, bindingEndpointMap.Values.First());
                await WsdlHelper.ValidateSingleWsdl(baseAddresses, bindingEndpointMap, callerMethodName, sourceFilePath);
            }
        }
    }
}
