using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
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
                CountdownEvent countdownEvent = host.Services.GetService<CountdownEvent>();
                countdownEvent.Wait(TimeSpan.FromSeconds(30));
                Assert.Contains("Hello", host.Services.GetService<ConcurrentBag<string>>());
            }
        }

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task OneWayPatternTest_Parallel(int callCount)
        {
            // Does nothing on .NET [Core], but enables .NET Framework to have sufficient concurrency
            // to make all the requests
            int previousConnectionLimit = System.Net.ServicePointManager.DefaultConnectionLimit;
            System.Net.ServicePointManager.DefaultConnectionLimit = callCount;
            try
            {
                var inputs = Enumerable.Range(0, callCount).Select(x => x.ToString()).ToArray();
                var host = ServiceHelper.CreateWebHostBuilder<AsyncNetAdoptionOneWayServiceStartup>(_output).Build();
                using (host)
                {
                    host.Start();
                    CountdownEvent countdownEvent = host.Services.GetService<CountdownEvent>();
                    countdownEvent.AddCount(inputs.Length - 1);
                    var httpBinding = ClientHelper.GetBufferedModeBinding();
                    var factory = new System.ServiceModel.ChannelFactory<ClientContract.IOneWayContract>(httpBinding,
                        new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/OneWayPatternTest/basichttp.svc")));
                    var channel = factory.CreateChannel();
                    var tasks = inputs.AsParallel().Select(x => channel.OneWay(x)).ToList();
                    await Task.WhenAll(tasks);
                    countdownEvent.Wait(TimeSpan.FromSeconds(30));
                    var bag = host.Services.GetService<ConcurrentBag<string>>();
                    foreach (string input in inputs)
                    {
                        Assert.Contains(input, bag);
                    }
                }
            }
            finally
            {
                System.Net.ServicePointManager.DefaultConnectionLimit = previousConnectionLimit;
            }
        }

        internal class AsyncNetAdoptionOneWayServiceStartup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddSingleton<ConcurrentBag<string>>();
                services.AddSingleton(_ => new CountdownEvent(1));
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
