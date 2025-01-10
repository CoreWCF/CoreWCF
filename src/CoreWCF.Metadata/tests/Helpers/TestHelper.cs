// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CoreWCF.Channels;
using Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
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

            using (host)
            {
                await host.StartAsync();
                var portHelper = host.Services.GetRequiredService<ListeningPortHelper>();
                string wsdlScheme = binding.Scheme.StartsWith("http") ? binding.Scheme : Uri.UriSchemeHttp;
                int port = portHelper.GetPortForScheme(wsdlScheme);
                if (port == 0)
                {
                    Assert.Fail($"No port found for {wsdlScheme} scheme, available port mappings are: {portHelper}");
                }

                string wsdlAddress = $"{wsdlScheme}://localhost:{port}" + EndpointRelativePath;

                endpointAddress ??= ServiceHelper.GetEndpointBaseAddress(host, binding.Scheme);
                if (endpointAddress.EndsWith("/"))
                {
                    endpointAddress = endpointAddress.Substring(0, endpointAddress.Length - 1);
                }
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

            using (host)
            {
                await host.StartAsync();
                var portHelper = host.Services.GetRequiredService<ListeningPortHelper>();
                string wsdlScheme1 = binding1.Scheme.StartsWith("http") ? binding1.Scheme : Uri.UriSchemeHttp;
                int port1 = portHelper.GetPortForScheme(wsdlScheme1);
                if (port1 == 0)
                {
                    Assert.Fail($"No port found for {wsdlScheme1} scheme, available port mappings are: {portHelper}");
                }
                string wsdl1Address = $"{wsdlScheme1}://localhost:{port1}/1" + EndpointRelativePath;

                string wsdlScheme2 = binding2.Scheme.StartsWith("http") ? binding2.Scheme : Uri.UriSchemeHttp;
                int port2 = portHelper.GetPortForScheme(wsdlScheme2);
                if (port2 == 0)
                {
                    Assert.Fail($"No port found for {wsdlScheme2} scheme, available port mappings are: {portHelper}");
                }
                string wsdl2Address = $"{wsdlScheme2}://localhost:{port2}/2" + EndpointRelativePath;

                var endpoint1Address = $"{ServiceHelper.GetEndpointBaseAddress(host, binding1.Scheme)}1{EndpointRelativePath}/{binding1.Scheme}";
                var endpoint2Address = $"{ServiceHelper.GetEndpointBaseAddress(host, binding2.Scheme)}2{EndpointRelativePath}/{binding2.Scheme}";
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
                // Fix the base addresses to have the correct port numbers based on the actual port numbers used by the host
                var portHelper = host.Services.GetRequiredService<ListeningPortHelper>();
                for (int i = 0; i < baseAddresses.Length; i++)
                {
                    var baseAddressScheme = baseAddresses[i].Scheme;
                    int port = portHelper.GetPortForScheme(baseAddressScheme);
                    if (port == 0)
                    {
                        Assert.Fail($"No port found for {baseAddressScheme} scheme, available port mappings are: {portHelper}");
                    }

                    var baseAddressBuilder = new UriBuilder(baseAddresses[i]);
                    baseAddressBuilder.Port = port;
                    baseAddresses[i] = baseAddressBuilder.Uri;
                }

                await WsdlHelper.ValidateSingleWsdl(baseAddresses, bindingEndpointMap, callerMethodName, sourceFilePath);
            }
        }

        internal static async Task RunHelpPageTestAsync<TService, TContract>(Binding binding, ITestOutputHelper output,
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

            using (host)
            {
                await host.StartAsync();
                var portHelper = host.Services.GetRequiredService<ListeningPortHelper>();
                string helpPageAddress;
                if (Uri.UriSchemeHttps.Equals(binding.Scheme))
                {
                    int port = portHelper.GetPortForScheme(Uri.UriSchemeHttps);
                    if (port == 0)
                    {
                        Assert.Fail($"No port found for https scheme, available port mappings are: {portHelper}");
                    }
                    helpPageAddress = $"https://localhost:{port}" + EndpointRelativePath;
                }
                else
                {
                    int port = portHelper.GetPortForScheme(Uri.UriSchemeHttp);
                    if (port == 0)
                    {
                        Assert.Fail($"No port found for http scheme, available port mappings are: {portHelper}");
                    }
                    helpPageAddress = $"http://localhost:{port}" + EndpointRelativePath;
                }

                await HelpPageHelper.ValidateHelpPage(helpPageAddress, callerMethodName, sourceFilePath, configureHttpClient);
            }
        }

        internal static async Task RunMultipleEndpointsHelpPageTestAsync<TService, TContract>(IDictionary<string, Binding> bindingEndpointMap, Uri[] baseAddresses, ITestOutputHelper output,
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
                var baseAddressSchemes = baseAddresses.Select(uri => uri.Scheme).Distinct().ToArray();
                var portHelper = host.Services.GetRequiredService<ListeningPortHelper>();
                string helpPageAddress;
                foreach (var scheme in new[] { Uri.UriSchemeHttp, Uri.UriSchemeHttps } )
                {
                    if (!baseAddressSchemes.Contains(scheme))
                    {
                        continue;
                    }

                    int port = portHelper.GetPortForScheme(scheme);
                    if (port == 0)
                    {
                        Assert.Fail($"No port found for {scheme} scheme, available port mappings are: {portHelper}");
                    }

                    var helpPageAddressBuilder = new UriBuilder(baseAddresses.First(uri => uri.Scheme.Equals(scheme)));
                    helpPageAddressBuilder.Port = port;
                    var endpointRelativePath = bindingEndpointMap.Where(entry => entry.Value.Scheme.Equals(scheme)).Select(entry => entry.Key).First();
                    helpPageAddressBuilder.Path = Path.Combine(helpPageAddressBuilder.Path, endpointRelativePath);

                    helpPageAddress = helpPageAddressBuilder.Uri.ToString();
                    await HelpPageHelper.ValidateHelpPage(helpPageAddress, callerMethodName, sourceFilePath);
                }
            }
        }

    }
}
