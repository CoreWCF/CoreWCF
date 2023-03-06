using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
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
                await host.StartAsync();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IOneWayContract>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/OneWayPatternTest/basichttp.svc")));
                var channel = factory.CreateChannel();
                await channel.OneWay("Hello");
                AsyncCountdownEvent countdownEvent = host.Services.GetService<AsyncCountdownEvent>();
                await countdownEvent.WaitAsync();
                Assert.Contains("Hello", host.Services.GetService<ConcurrentBag<string>>());
            }
        }

        [Theory]
        [InlineData(10)]
#if NET6_0_OR_GREATER
        [InlineData(100)]
        [InlineData(1000)]
#endif
        public async Task OneWayPatternTest_Parallel(int callCount)
        {
            var inputs = Enumerable.Range(0, callCount).Select(x => x.ToString()).ToArray();
            IWebHost host = ServiceHelper.CreateWebHostBuilder<AsyncNetAdoptionOneWayServiceStartup>(_output).Build();
            using (host)
            {
                await host.StartAsync();
                AsyncCountdownEvent countdownEvent = host.Services.GetService<AsyncCountdownEvent>();
                countdownEvent.AddCount(inputs.Length - 1);
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IOneWayContract>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/OneWayPatternTest/basichttp.svc")));
                var channel = factory.CreateChannel();
                var tasks = inputs.AsParallel().Select(x => channel.OneWay(x)).ToList();
                await Task.WhenAll(tasks);
                await countdownEvent.WaitAsync();
                var bag = host.Services.GetService<ConcurrentBag<string>>();
                foreach (string input in inputs)
                {
                    Assert.Contains(input, bag);
                }
            }
        }

        internal class AsyncNetAdoptionOneWayServiceStartup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddSingleton<ConcurrentBag<string>>();
                services.AddSingleton(_ => new AsyncCountdownEvent(1));
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
