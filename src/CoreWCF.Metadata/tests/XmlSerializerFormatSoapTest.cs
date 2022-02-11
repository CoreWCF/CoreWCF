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
    public class XmlSerializerFormatSoapTest
    {
        private readonly ITestOutputHelper _output;

        public XmlSerializerFormatSoapTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task SoapServiceTest()
        {
            await TestHelper.RunSingleWsdlTestAsync<SoapService, IWcfSoapService>(new BasicHttpBinding(), _output);
        }

        [Fact]
        public async Task XmlGeneratedTest()
        {
            await TestHelper.RunSingleWsdlTestAsync<WcfServiceXmlGenerated, IWcfServiceXmlGenerated>(new BasicHttpBinding(), _output);
        }
    }
}
