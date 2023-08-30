// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CoreWCF.Channels;
using Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace CoreWCF.Metadata.Tests.Helpers
{
    internal static class TestHelper
    {
        private const string EndpointRelativePath = "/endpointAddress.svc";

        internal static async Task RunSingleWsdlTestAsync<TService, TContract>(Binding binding, ITestOutputHelper output,
            Action<IServiceCollection> configureServices = null,
            Action<HttpClient> configureHttpClient = null,
            string endpointAddress = null,
            [System.Runtime.CompilerServices.CallerMemberName] string callerMethodName = "",
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
                 where TService : class
        {
            IWebHost host = ServiceHelper.CreateHttpWebHostBuilderWithMetadata<TService, TContract>(
                binding,
                EndpointRelativePath,
                configureServices,
                output,
                callerMethodName)
                .Build();
            string wsdlAddress = "http://localhost:8080" + EndpointRelativePath;
            if ("https".Equals(binding.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                wsdlAddress = "https://localhost:8443" + EndpointRelativePath;
            }
            using (host)
            {
                await host.StartAsync();
                endpointAddress ??= ServiceHelper.GetEndpointBaseAddress(host, binding);
                endpointAddress += EndpointRelativePath;
                await WsdlHelper.ValidateSingleWsdl(wsdlAddress, endpointAddress, callerMethodName, sourceFilePath, configureHttpClient);
            }
        }

        internal static async Task RunMultipleWsdlTestAsync<TService1, TService2, TContract1, TContract2>(Binding binding1, Binding binding2, ITestOutputHelper output,
        [System.Runtime.CompilerServices.CallerMemberName] string callerMethodName = "",
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "") where TService1 : class where TService2 : class
        {
            IWebHost host = ServiceHelper.CreateHttpWebHostBuilderWithMetadata<TService1, TService2, TContract1, TContract2>(
                binding1,
                binding2,
                EndpointRelativePath,
                output,
                callerMethodName)
                .Build();
            string wsdl1Address = "http://localhost:8080/1" + EndpointRelativePath;
            if ("https".Equals(binding1.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                wsdl1Address = "https://localhost:8443/1" + EndpointRelativePath;
            }
            string wsdl2Address = "http://localhost:8080/2" + EndpointRelativePath;
            if ("https".Equals(binding1.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                wsdl2Address = "https://localhost:8443/2" + EndpointRelativePath;
            }
            using (host)
            {
                await host.StartAsync();
                var endpoint1Address = $"{ServiceHelper.GetEndpointBaseAddress(host, binding1)}/1{EndpointRelativePath}/{binding1.Scheme}";
                var endpoint2Address = $"{ServiceHelper.GetEndpointBaseAddress(host, binding2)}/2{EndpointRelativePath}/{binding2.Scheme}";
                await WsdlHelper.ValidateSingleWsdl(wsdl1Address, endpoint1Address, callerMethodName + "_1", sourceFilePath);
                await WsdlHelper.ValidateSingleWsdl(wsdl2Address, endpoint2Address, callerMethodName + "_2", sourceFilePath);
            }
        }


        internal static async Task RunSingleWsdlTestAsync<TService, TContract>(IDictionary<string, Binding> bindingEndpointMap, Uri[] baseAddresses, ITestOutputHelper output,
                [System.Runtime.CompilerServices.CallerMemberName] string callerMethodName = "",
                [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "") where TService : class
        {
            IWebHost host = ServiceHelper.CreateHttpWebHostBuilderWithMetadata<TService, TContract>(
                bindingEndpointMap,
                baseAddresses,
                output,
                callerMethodName)
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
