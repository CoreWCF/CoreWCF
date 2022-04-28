// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Metadata.Tests.Helpers;
using CoreWCF.Security;
using ServiceContract;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Metadata.Tests
{
    public class WSHttpBindingTest
    {
        private readonly ITestOutputHelper _output;

        public WSHttpBindingTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "NetCoreOnly")] // Windows auth not supported on NetFx
        public async Task TransportWithMessageCredentials_MultiAuth()
        {
            var bindingEndpointMap = new Dictionary<string, Binding>
            {
                ["winAuth"] = CreateBindingUsingCredentialType(MessageCredentialType.Windows),
                ["certAuth"] = CreateBindingUsingCredentialType(MessageCredentialType.Certificate),
                ["userNameAuth"] = CreateBindingUsingCredentialType(MessageCredentialType.UserName)
            };
            await TestHelper.RunSingleWsdlTestAsync<SimpleEchoService, IEchoService>(bindingEndpointMap, new Uri[] { new Uri("http://localhost:8080/wsHttp"), new Uri("https://localhost:8443/wsHttp") }, _output);
        }

        private WSHttpBinding CreateBindingUsingCredentialType(MessageCredentialType credentialType)
        {
            WSHttpBinding binding = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
            binding.Security.Message.ClientCredentialType = credentialType;
            return binding;
        }

        [Fact]
        public async Task TransportWithMessageCredentials_CertificateAuth_NoSecurityContext()
        {
            WSHttpBinding binding = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
            binding.Security.Message.ClientCredentialType = MessageCredentialType.Certificate;
            binding.Security.Message.EstablishSecurityContext = false;
            await TestHelper.RunSingleWsdlTestAsync<SimpleEchoService, IEchoService>(binding, _output);
        }

        [Fact]
        public async Task TransportWithMessageCredentials_CertAuth_MultiSecurityAlgo()
        {
            var bindingEndpointMap = new Dictionary<string, Binding>
            {
                ["Basic128"] = CreateBindingUsingAglgorithmSuite(SecurityAlgorithmSuite.Basic128),
                ["Basic128Sha256"] = CreateBindingUsingAglgorithmSuite(SecurityAlgorithmSuite.Basic128Sha256),
                ["Basic192"] = CreateBindingUsingAglgorithmSuite(SecurityAlgorithmSuite.Basic192),
                ["Basic192Sha256"] = CreateBindingUsingAglgorithmSuite(SecurityAlgorithmSuite.Basic192Sha256),
                ["Basic256"] = CreateBindingUsingAglgorithmSuite(SecurityAlgorithmSuite.Basic256),
                ["Basic256Sha256"] = CreateBindingUsingAglgorithmSuite(SecurityAlgorithmSuite.Basic256Sha256)
            };
            await TestHelper.RunSingleWsdlTestAsync<SimpleEchoService, IEchoService>(bindingEndpointMap, new Uri[] { new Uri("http://localhost:8080/wsHttp"), new Uri("https://localhost:8443/wsHttp") }, _output);
        }

        private Binding CreateBindingUsingAglgorithmSuite(SecurityAlgorithmSuite suite)
        {
            WSHttpBinding binding = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
            binding.Security.Message.ClientCredentialType = MessageCredentialType.Certificate;
            binding.Security.Message.AlgorithmSuite = suite;
            return binding;
        }
    }
}
