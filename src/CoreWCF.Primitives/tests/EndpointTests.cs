// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Configuration;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public class EndpointTests
    {
        [ServiceContract]
        interface IContract1
        {
            [OperationContract]
            void Operation1();
        }

        [ServiceContract]
        interface IContract2
        {
            [OperationContract]
            void Operation2();
        }

        class TwoContractService : IContract1, IContract2
        {
            public void Operation1()
            { }

            public void Operation2()
            { }
        }

        class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<TwoContractService>();
                    var binding = new NetTcpBinding();
                    var address = "net-tcp://localhost/Test";
                    builder.AddServiceEndpoint<TwoContractService, IContract1>(binding, address);
                    builder.AddServiceEndpoint<TwoContractService, IContract2>(binding, address);
                });
            }
        }

        [Fact]
        public void MultipleEndpointsWithSameListenAddressShouldWork()
        {
            var builder = WebHost.CreateDefaultBuilder<Startup>(null);
            builder.UseNetTcp(0);
            var host = builder.Build();
            using (host)
                host.Start();
        }
    }
}
