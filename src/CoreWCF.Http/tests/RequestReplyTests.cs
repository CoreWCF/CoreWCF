// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using ClientContract;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class RequestReplyTests
    {
        private readonly ITestOutputHelper _output;

        public RequestReplyTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("Http1Binding")]
        //[InlineData("Http2Binding")] //Fail
        //[InlineData("Http3Binding")]
        public void RequestReplyStreaming(string binding)
        {
            Startup.binding = binding;
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.ChannelFactory<ClientContract.IStream> channelFactory = null;
                switch (binding)
                {
                    case "Http1Binding":
                        channelFactory = new System.ServiceModel.ChannelFactory<ClientContract.IStream>(ClientHelper.GetBufferedModHttp1Binding(),
                      new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService1/RequestReplyTests.svc")));
                        break;
                    //case "Http2Binding":
                    //    channelFactory = new System.ServiceModel.ChannelFactory<ClientContract.IStream>(ClientHelper.GetBufferedModHttp2Binding(),
                    //  new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService2/RequestReplyTests.svc")));
                    //    break;
                    //case "Http3Binding":
                    //    channelFactory = new System.ServiceModel.ChannelFactory<ClientContract.IStream>(ClientHelper.GetBufferedModHttp3Binding(),
                    //  new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService3/RequestReplyTests.svc")));
                    //    break;
                    default:
                        throw new Exception("Unknown binding");
                }
                IStream stream2 = channelFactory.CreateChannel();
                long messageSize = 0;
                long num2 = 20000;
                Stream stream = null;
                MyStream input = new MyStream(messageSize);
                stream = stream2.Echo(input);
                int num3 = 0;
                byte[] buffer = new byte[5000];
                int num4;
                while ((num4 = stream.Read(buffer, 0, 370)) != 0)
                {
                    num3 = num4 + num3;
                }
                Assert.Equal(num2, (long)num3);
            }
        }

        internal class Startup
        {
            public static string binding;
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }
            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<ReqRepService>();
                    switch (binding)
                    {
                        case "Http1Binding":
                            builder.AddServiceEndpoint<ReqRepService, ServiceContract.IStream>(ServiceHelper.GetBufferedModHttp1Binding(), "/BasicWcfService1/RequestReplyTests.svc");
                            break;
                        //case "Http2Binding":
                        //    builder.AddServiceEndpoint<ReqRepService, ServiceContract.IStream>(ServiceHelper.GetBufferedModHttp2Binding(), "/BasicWcfService2/RequestReplyTests.svc");
                        //    break;
                        //case "Http3Binding":
                        //    builder.AddServiceEndpoint<ReqRepService, ServiceContract.IStream>(ServiceHelper.GetBufferedModHttp3Binding(), "/BasicWcfService3/RequestReplyTests.svc");
                        //    break;
                        default:
                            throw new Exception("Unknown binding");

                    }
                });
            }
        }
    }
}

