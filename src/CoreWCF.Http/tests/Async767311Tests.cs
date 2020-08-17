﻿using System;
using System.Threading;
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
    public class Async767311Tests
    {
        public ITestOutputHelper _output;

        public Async767311Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        public string clientString = "String From Client";
        public string clientResult = "Async call was valid";

        [Fact]
        public void Variation_EndMethod()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<IClientAsync_767311>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/SyncService.svc")));
                IClientAsync_767311 clientAsync_ = factory.CreateChannel();
                _output.WriteLine("Testing [Variation_EndMethod]");
                IAsyncResult result = clientAsync_.BeginEchoString(clientString, null, null);
                _output.WriteLine("Message sent via Async");
                string strB = clientAsync_.EndEchoString(result);
                Assert.Equal(clientResult, strB);
            }
        }

        [Fact]
        public void Variation_WaitMethod()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<IClientAsync_767311>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/SyncService.svc")));
                IClientAsync_767311 clientAsync_ = factory.CreateChannel();
                _output.WriteLine("Testing [Variation_WaitMethod]");
                IAsyncResult asyncResult = clientAsync_.BeginEchoString(clientString, null, null);
                _output.WriteLine("Message sent via Async, waiting for handle to be signaled");
                asyncResult.AsyncWaitHandle.WaitOne();
                _output.WriteLine("Wait handle has been signaled");
                string strB = clientAsync_.EndEchoString(asyncResult);
                Assert.Equal(clientResult, strB);
            }
        }

        [Fact]
        public void Variation_PollingMethod()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<IClientAsync_767311>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/SyncService.svc")));
                IClientAsync_767311 clientAsync_ = factory.CreateChannel();
                _output.WriteLine("Testing [Variation_PollingMethod]");
                IAsyncResult asyncResult = clientAsync_.BeginEchoString(clientString, null, null);
                _output.WriteLine("Message sent via Async");
                _output.WriteLine("Start polling for IsCompleted != true");
                while (!asyncResult.IsCompleted)
                {
                }
                _output.WriteLine("IsCompleted == true");
                string text = clientAsync_.EndEchoString(asyncResult);
                _output.WriteLine(text);
                Assert.Equal(clientResult, text);
            }
        }

        public void CallbackResults(IAsyncResult asyncResult)
        {
            _output.WriteLine("Callback received, signalling");
            autoEvent.Set();
        }

        [Fact]
        public void Variation_CallbackMethod()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<IClientAsync_767311>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/SyncService.svc")));
                IClientAsync_767311 clientAsync_ = factory.CreateChannel();
                _output.WriteLine("Testing [Variation_CallbackMethod]");
                AsyncCallback callback = new AsyncCallback(CallbackResults);
                IAsyncResult result = clientAsync_.BeginEchoString(clientString, callback, null);
                _output.WriteLine("Message sent via Async, waiting for callback");
                this.autoEvent.WaitOne();
                _output.WriteLine("Event has been signalled");
                string text = clientAsync_.EndEchoString(result);
                _output.WriteLine(text);
                Assert.Equal(clientResult, text);
            }
        }

        public AutoResetEvent autoEvent = new AutoResetEvent(false);

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
                    builder.AddService<SM_767311Service>();
                    builder.AddServiceEndpoint<SM_767311Service, ServiceContract.ISyncService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/SyncService.svc");
                });
            }
        }
    }
}

