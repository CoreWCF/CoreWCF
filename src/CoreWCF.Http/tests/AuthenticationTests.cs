using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace CoreWCF.Http.Tests
{
    public class AuthenticationTests
    {
        private ITestOutputHelper _output;
        static System.Net.AuthenticationSchemes scheme;

        public AuthenticationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("NTLM")]
        [InlineData("Basic")]
        public void WindowsProxyAuthenticationSelfhostSecure(string authenticationSchemes)
        {
            switch (authenticationSchemes)
            {
                case "NTLM":
                    scheme = System.Net.AuthenticationSchemes.Ntlm;
                    break;
                case "Basic":
                    scheme = System.Net.AuthenticationSchemes.Basic;
                    break;
                default:
                    break;
            }
            
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedNetHttpBinding(scheme);
                ClientReceiver callback = new ClientReceiver();

                var factory = new System.ServiceModel.DuplexChannelFactory<ClientContract.IDuplexService>(new System.ServiceModel.InstanceContext(callback), httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/IDuplexService.svc")));
                factory.Credentials.UserName.UserName = "Fake";
                factory.Credentials.UserName.Password = "Fake";
                ClientContract.IDuplexService proxy = factory.CreateChannel();
                _output.WriteLine("Proxy created, sending data");
                proxy.UploadData("First request");
                proxy.UploadData("Second Request");
                proxy.UploadData("Third Request");
                proxy.GetLog();
                _output.WriteLine("Data sent, waiting for log data in local callback instance");

                if (!callback.LogReceived.WaitOne(TimeSpan.FromSeconds(60)))
                {
                    throw new Exception("Log not received from service");
                }

                if (!ValidateLog(callback.ServerLog, 3,_output))
                {
                    throw new Exception("Not enough entries in server log");
                }
            }
        }

        private static bool ValidateLog(List<string> log, int entries, ITestOutputHelper output)
        {
            output.WriteLine("Validating server log, expecting {0} entries", entries);
            foreach (string entry in log)
            {
                output.WriteLine("Received server log entry: {0}", entry);
            }

            return log.Count >= entries;
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
                    builder.AddService<Services.DuplexService>();
                    builder.AddServiceEndpoint<Services.DuplexService, ServiceContract.IDuplexService>(ServiceHelper.GetNetHttpBinding(scheme), "/BasicWcfService/IDuplexService.svc");
                });
            }
        }
    }
}
