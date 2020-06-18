using System;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class FaultContractNameTests
    {
        private ITestOutputHelper _output;

        public FaultContractNameTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void FaultOnDiffString()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestFaultContractName>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/TestFaultContractNameService.svc")));
                var channel = factory.CreateChannel();

                //test variations count
                int count = 21;
                string faultToThrow = "Test fault thrown from a service";

                //variation method1
                try
                {
                    string s = channel.Method1("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method2
                try
                {
                    string s = channel.Method2("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method3
                try
                {
                    string s = channel.Method3("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method4
                try
                {
                    string s = channel.Method4("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method5
                try
                {
                    string s = channel.Method5("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method6
                try
                {
                    string s = channel.Method6("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method7
                try
                {
                    string s = channel.Method7("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method8
                try
                {
                    string s = channel.Method8("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method9
                try
                {
                    string s = channel.Method9("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method10
                try
                {
                    string s = channel.Method10("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method11
                try
                {
                    string s = channel.Method11("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method12
                try
                {
                    string s = channel.Method12("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method13
                try
                {
                    string s = channel.Method13("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method14
                try
                {
                    string s = channel.Method14("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method15
                try
                {
                    string s = channel.Method15("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method16
                try
                {
                    string s = channel.Method16("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method17
                try
                {
                    string s = channel.Method17("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method18
                try
                {
                    string s = channel.Method18("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method19
                try
                {
                    string s = channel.Method19("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method20
                try
                {
                    string s = channel.Method20("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //variation method21
                try
                {
                    string s = channel.Method21("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);

                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }
                Assert.Equal(0, count);
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
                    builder.AddService<Services.TestFaultContractNameService>();
                    builder.AddServiceEndpoint<Services.TestFaultContractNameService, ServiceContract.ITestFaultContractName>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/TestFaultContractNameService.svc");
                });
            }
        }

    }
}
