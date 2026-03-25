using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class LifeTimeTest
    {
        private ITestOutputHelper _output;
        private ManualResetEvent syncEvent = null;
        private bool flag = true;

        public LifeTimeTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("basic")]
        [InlineData("threaded")]
        [InlineData("threadedsync")]
        public void PerCallTest(string variationInfo)
        {
            syncEvent = new ManualResetEvent(false);
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.ILifeTimeTestService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/LifeTimeTestService.svc")));
                var channel = factory.CreateChannel();
                channel.Start();

                switch (variationInfo.ToLower())
                {
                    case "basic":
                        CallBasic();
                        channel.Final(3, variationInfo);
                        break;

                    case "threaded":
                        Thread[] threads = new Thread[3];
                        for (int i = 0; i < threads.Length; i++)
                        {
                            threads[i] = new Thread(new ThreadStart(CallBasic));
                            threads[i].Start();
                        }

                        for (int i = 0; i < threads.Length; i++)
                        {
                            threads[i].Join();
                        }

                        channel.Final(9, variationInfo);
                        break;

                    case "threadedsync":
                        flag = false;
                        threads = new Thread[3];
                        for (int i = 0; i < threads.Length; i++)
                        {
                            threads[i] = new Thread(new ThreadStart(CallBasic));
                            threads[i].Start();
                        }

                        syncEvent.Set();
                        for (int i = 0; i < threads.Length; i++)
                        {
                            threads[i].Join();
                        }

                        channel.Final(9, variationInfo);
                        break;

                    default:
                        break;
                }

                ((System.ServiceModel.IClientChannel)channel).Close();
            }
        }

        private void CallBasic()
        {
            var httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<ClientContract.ILifeTimeTestService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/LifeTimeTestService.svc")));
            var channel = factory.CreateChannel();

            // This happens only for ThreadedSync
            if (flag == false)
                syncEvent.WaitOne();

            for (int i = 0; i < 3; i++)
            {
                //channel.OneWay(); blocking issue: https://github.com/CoreWCF/CoreWCF/issues/197
                channel.TwoWay();
            }

            ((System.ServiceModel.IClientChannel)channel).Close();
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
                    builder.AddService<Services.LifeTimeTestService>();
                    builder.AddServiceEndpoint<Services.LifeTimeTestService, ServiceContract.ILifeTimeTestService>(new BasicHttpBinding(), "/BasicWcfService/LifeTimeTestService.svc");
                });
            }
        }
    }
}
