using System;
using System.Net.Http;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.Metadata.Tests.Helpers;
using Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
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
