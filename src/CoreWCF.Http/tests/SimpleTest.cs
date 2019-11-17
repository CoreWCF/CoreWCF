using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Helpers;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace BasicHttp
{
    public class SimpleTest
    {
        private ITestOutputHelper _output;

        public SimpleTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BasicHttpRequestReplyEchoString()
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
                var channel = factory.CreateChannel();
                var result = channel.EchoString(testString);
                Assert.Equal(testString, result);
            }
        }
    }
}