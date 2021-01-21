// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace BasicHttp
{
    public class HttpRequestMessagePropertyTests
    {
        private ITestOutputHelper _output;
        internal const string TestHeaderName = "HttpRequestMessagePropertyTests-TestHeader";
        internal const string TestHeaderValue = "HttpRequestMessagePropertyTestsHeaderValue";

        public HttpRequestMessagePropertyTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void CanFetchHttpHeadersSync()
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.ChannelFactory<IHttpHeadersService> factory = null;
                IHttpHeadersService channel = null;
                try
                {
                    var httpBinding = ClientHelper.GetBufferedModeBinding();
                    factory = new System.ServiceModel.ChannelFactory<IHttpHeadersService>(httpBinding,
                        new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/httpHeaders.svc")));
                    channel = factory.CreateChannel();
                    using (new System.ServiceModel.OperationContextScope((System.ServiceModel.IContextChannel)channel))
                    {
                        var httpRequestMessageProperty = new System.ServiceModel.Channels.HttpRequestMessageProperty();
                        httpRequestMessageProperty.Headers[TestHeaderName] = TestHeaderValue;
                        System.ServiceModel.OperationContext.Current.OutgoingMessageProperties.Add(System.ServiceModel.Channels.HttpRequestMessageProperty.Name, httpRequestMessageProperty);
                        var headerValue = channel.GetHeaderSync(TestHeaderName);
                        Assert.Equal(TestHeaderValue, headerValue);
                    }
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CanFetchHttpHeadersAsync(bool initialYield)
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.ChannelFactory<IHttpHeadersService> factory = null;
                IHttpHeadersService channel = null;
                try
                {
                    var httpBinding = ClientHelper.GetBufferedModeBinding();
                    factory = new System.ServiceModel.ChannelFactory<IHttpHeadersService>(httpBinding,
                        new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/httpHeaders.svc")));
                    channel = factory.CreateChannel();
                    Task<string> resultTask = null;
                    using (new System.ServiceModel.OperationContextScope((System.ServiceModel.IContextChannel)channel))
                    {
                        var httpRequestMessageProperty = new System.ServiceModel.Channels.HttpRequestMessageProperty();
                        httpRequestMessageProperty.Headers[TestHeaderName] = TestHeaderValue;
                        System.ServiceModel.OperationContext.Current.OutgoingMessageProperties.Add(System.ServiceModel.Channels.HttpRequestMessageProperty.Name, httpRequestMessageProperty);
                        resultTask = channel.GetHeaderAsync(TestHeaderName, initialYield);
                    }

                    var headerValue = await resultTask;
                    Assert.Equal(TestHeaderValue, headerValue);
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
                }
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
                    builder.AddService<HttpHeadersService>();
                    builder.AddServiceEndpoint<HttpHeadersService, IHttpHeadersService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/httpHeaders.svc");
                });
            }
        }

        public class HttpHeadersService : IHttpHeadersService
        {
            public async Task<string> GetHeaderAsync(string headerName, bool initialYield)
            {
                if (initialYield)
                {
                    await Task.Yield();
                }

                return GetHeaderSync(headerName);
            }

            public string GetHeaderSync(string headerName)
            {
                var operationContext = OperationContext.Current;
                if (operationContext == null)
                {
                    return "Operation Context was null";
                }

                HttpRequestMessageProperty httpReqProp = GetMessageProperty(operationContext);
                if (httpReqProp == null)
                {
                    return "Missing HttpRequestMessageProperty";
                }

                if (httpReqProp.Headers == null)
                {
                    return "HttpRequestMessageProperty.Headers is null";
                }

                return httpReqProp.Headers[headerName];
            }

            private HttpRequestMessageProperty GetMessageProperty(OperationContext operationContext)
            {
                HttpRequestMessageProperty httpReqProp = null;
                if (operationContext.IncomingMessageProperties.TryGetValue(HttpRequestMessageProperty.Name, out object httpReqPropObj))
                {
                    httpReqProp = httpReqPropObj as HttpRequestMessageProperty;
                }

                return httpReqProp;
            }
        }

        [System.ServiceModel.ServiceContract]
        public interface IHttpHeadersService
        {
            [System.ServiceModel.OperationContract]
            string GetHeaderSync(string headerName);
            [System.ServiceModel.OperationContract]
            Task<string> GetHeaderAsync(string headerName, bool initialYield);
        }
    }
}
