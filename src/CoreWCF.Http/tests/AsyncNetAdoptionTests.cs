using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
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
            var host = ServiceHelper.CreateWebHostBuilder<AsyncNetAdoptionOneWayServiceStartup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IOneWayContract>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/OneWayPatternTest/basichttp.svc")));
                var channel = factory.CreateChannel();
                await channel.OneWay("Hello");
                Assert.Contains("Hello", host.Services.GetService<ConcurrentBag<string>>());
            }
        }

        [Fact]
        public async Task OneWayPatternTest_Parallel()
        {
            var inputs = Enumerable.Range(0, 100).Select(x => x.ToString()).ToArray();
            var host = ServiceHelper.CreateWebHostBuilder<AsyncNetAdoptionOneWayServiceStartup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IOneWayContract>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/OneWayPatternTest/basichttp.svc")));
                var channel = factory.CreateChannel();
                var tasks = inputs.AsParallel().Select(x => channel.OneWay(x)).ToList();
                await Task.WhenAll(tasks);
                foreach (string input in inputs)
                {
                    Assert.Contains(input, host.Services.GetService<ConcurrentBag<string>>());
                }
            }
        }

        internal class AsyncNetAdoptionOneWayServiceStartup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddSingleton<ConcurrentBag<string>>();
                services.AddTransient<Services.OneWayService>();
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
