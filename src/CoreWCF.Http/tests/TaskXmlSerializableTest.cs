using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Http.Tests
{
    public class TaskXmlSerializableTest
    {
        private ITestOutputHelper _output;

        public TaskXmlSerializableTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void HostServiceAndValidate()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();

                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var channelFactory = new System.ServiceModel.ChannelFactory<ClientContract.IXmlSerializerContract>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));

                ClientContract.IXmlSerializerContract xmlSerializerContract = (ClientContract.IXmlSerializerContract)channelFactory.CreateChannel();
                Task<ClientContract.XmlSerializerPerson> person = xmlSerializerContract.GetPerson();

                Assert.NotNull(person);
                Assert.NotNull(person.Result);
            }
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.XmlSerializerContract>();
                    builder.AddServiceEndpoint<Services.XmlSerializerContract, ServiceContract.IXmlSerializerContract>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                });
            }
        }
    }
}
