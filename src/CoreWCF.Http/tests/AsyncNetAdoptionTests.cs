using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BasicHttp
{
    public class AsyncNetAdoptionTests
    {
        private ITestOutputHelper _output;

        public AsyncNetAdoptionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task OneWayPatternTest()
        {
            var inputs = Enumerable.Range(0, 10).Select(x => x.ToString());
            var host = ServiceHelper.CreateWebHostBuilder<AsyncNetAdoptionOneWayServiceStartup>(_output).Build();
            using (host)
            {
                host.Start();
                Services.OneWayService.TestOutputHelper = _output;
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IOneWayContract>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/OneWayPatternTest/basichttp.svc")));
                var channel = factory.CreateChannel();
                Parallel.ForEach(inputs, s => channel.OneWay(s));
                await Task.WhenAll(Services.OneWayService.Tasks);
                foreach (string input in inputs)
                {
                    Assert.Contains(input, Services.OneWayService.Inputs);
                }
            }
        }

        internal class AsyncNetAdoptionOneWayServiceStartup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.OneWayService>();
                    builder.AddServiceEndpoint<Services.OneWayService, ServiceContract.IOneWayContract>(new CoreWCF.BasicHttpBinding(), "/OneWayPatternTest/basichttp.svc");
                });
            }
        }
    }
}
