using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ServiceModel;
using Xunit;
using Xunit.Abstractions;

namespace BasicHttp
{
    public class ClientBaseTest
    {
        private ITestOutputHelper _output;

        public ClientBaseTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("AlwaysOff")]
        [InlineData("Default")]
        [InlineData("AlwaysOn")]
        public void VariousClientBase(string cacheSetting)
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {               
                host.Start();
#if NET472
                ClientBase<IEcho>.CacheSetting = (CacheSetting)Enum.Parse(typeof(CacheSetting), cacheSetting);
#endif
                _output.WriteLine("Creating Instance #1 of ClientBase()...");
                EchoClient proxy1 = new EchoClient(ClientHelper.GetBufferedModeBinding(), new EndpointAddress("http://localhost:8080/BasicWcfService/basichttp.svc"));
                ChannelFactoryCacheCommon.VerifyMruListCount<ClientBase<IEcho>>(0,_output);
                ChannelFactoryCacheCommon.VerifyUseCacheFactory<ClientBase<IEcho>>(proxy1, false, _output);
                string result = proxy1.Echo(ChannelFactoryCacheCommon.HelloWorld);
                ChannelFactoryCacheCommon.VerifyResult(result, ChannelFactoryCacheCommon.HelloWorld, _output);
                proxy1.Close();

                _output.WriteLine("Creating Instance #2 of ClientBase()...");
                EchoClient proxy2 = new EchoClient(ClientHelper.GetBufferedModeBinding(), new EndpointAddress("http://localhost:8080/BasicWcfService/basichttp.svc")); // getting endpoint from config
#if NET472
                ChannelFactoryCacheCommon.VerifyMruListCount<ClientBase<IEcho>>(EchoClient.CacheSetting == CacheSetting.AlwaysOff ? 0 : 1, _output);
                ChannelFactoryCacheCommon.VerifyUseCacheFactory<ClientBase<IEcho>>(proxy2, EchoClient.CacheSetting != CacheSetting.AlwaysOff, _output); // its ChannelFactory can not be shared iff AlwaysOff
#endif
                result = proxy2.Echo(ChannelFactoryCacheCommon.HelloWorld);
                ChannelFactoryCacheCommon.VerifyResult(result, ChannelFactoryCacheCommon.HelloWorld, _output);
#if NET472
                ChannelFactoryCacheCommon.CompareChannelFactoryRef(
                    EchoClient.CacheSetting != CacheSetting.AlwaysOff, _output,// the same ChannelFactory obj ref for Deafult and AlwaysOn
                    proxy1.ChannelFactory, proxy2.ChannelFactory);
                proxy2.Close();
#endif
                _output.WriteLine("Creating Instance #3 of ClientBase()...");
                EchoClient proxy3 = new EchoClient(ClientHelper.GetBufferedModeBinding(), new EndpointAddress("http://localhost:8080/BasicWcfService/basichttp.svc")); // getting endpoint from config
                result = proxy3.Echo(ChannelFactoryCacheCommon.HelloWorld);
                ChannelFactoryCacheCommon.VerifyResult(result, ChannelFactoryCacheCommon.HelloWorld, _output);
#if NET472
                ChannelFactoryCacheCommon.VerifyMruListCount<ClientBase<IEcho>>(EchoClient.CacheSetting == CacheSetting.AlwaysOff ? 0 : 1, _output);
                ChannelFactoryCacheCommon.VerifyUseCacheFactory<ClientBase<IEcho>>(proxy3, EchoClient.CacheSetting != CacheSetting.AlwaysOff, _output); // its ChannelFactory can not be shared iff AlwaysOff
                ChannelFactoryCacheCommon.CompareChannelFactoryRef(
                    EchoClient.CacheSetting != CacheSetting.AlwaysOff, _output, // the same ChannelFactory obj ref for Deafult and AlwaysOn
                    proxy1.ChannelFactory, proxy2.ChannelFactory, proxy3.ChannelFactory);
#endif
                proxy3.Close();
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
                    builder.AddService<Services.Echo>();
                    
                    builder.AddServiceEndpoint<Services.Echo, ServiceContract.IEcho>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                });
            }
        }
    }
}