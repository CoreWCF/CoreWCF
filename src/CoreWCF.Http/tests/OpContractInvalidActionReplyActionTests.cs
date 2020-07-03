using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Services;
using System;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class OpContractInvalidActionReplyActionTests
    {
        private ITestOutputHelper _output;
        public OpContractInvalidActionReplyActionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void NullAction()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                InvalidOperationException exception = null;
                try
                {
                    host.Start();
                }
                catch (InvalidOperationException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.NotNull(exception.InnerException);
                Assert.IsType<ArgumentNullException>(exception.InnerException);
            }
        }

        [Fact]
        public void NullReplyAction()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup2>(_output).Build();
            using (host)
            {
                InvalidOperationException exception = null;
                try
                {
                    host.Start();
                }
                catch (InvalidOperationException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.NotNull(exception.InnerException);
                Assert.IsType<ArgumentNullException>(exception.InnerException);
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
                    builder.AddService<OpContractInvalidActionSerivce>();
                    builder.AddServiceEndpoint<OpContractInvalidActionSerivce, ServiceContract.IOpContractInvalidAction>(new BasicHttpBinding(), "/BasicWcfService/OpContractInvalidActionSerivce.svc");
                });
            }
        }

        internal class Startup2
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<OpContractInvalidReplyActionSerivce>();
                    builder.AddServiceEndpoint<OpContractInvalidReplyActionSerivce, ServiceContract.IOpContractInvalidReplyAction>(new BasicHttpBinding(), "/BasicWcfService/OpContractInvalidReplyActionSerivce.svc");
                });
            }
        }
    }
}
