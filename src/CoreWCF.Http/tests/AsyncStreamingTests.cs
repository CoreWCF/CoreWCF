using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel.Channels;
using System.Threading;
using System.Xml;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class AsyncStreamingTests
    {
        private ITestOutputHelper _output;

        public AsyncStreamingTests(ITestOutputHelper output)
        {
            _output = output;
        }

#if NET472 //Depend on IAsyncResult in test execution
        [Fact]
        public void StreamBasicHttpBindingTest()
        {
            ThreadPool.SetMaxThreads(Common.numThreads, Common.numThreads);
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBasicHttpBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.AsyncStreamingService.IService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/AsyncStreamingService.svc")));
                var serviceChannel = factory.CreateChannel();
                
                SlowStreamConsumingClient(serviceChannel, 20);
                SlowMessageConsumingClient(serviceChannel, 20);
            }
        }

        [Fact] //fail
        public void StreamCustomBindingTest()
        {
            Startup._binding = "customBinding";
            ThreadPool.SetMaxThreads(Common.numThreads, Common.numThreads);
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var customBinding = ClientHelper.GetCustomBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.AsyncStreamingService.IService>(customBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/AsyncStreamingService.svc")));
                var serviceChannel = factory.CreateChannel();

                SlowStreamConsumingClient(serviceChannel, 20);
                SlowMessageConsumingClient(serviceChannel, 20);
            }
        }

        [Fact]
        public void VaryThreadPoolThreadsTest()
        {
            int requests = 3 * Common.requests;
            int numThreads = 3 * Common.numThreads;
            while (requests < 100)
            {
                ThreadPool.SetMaxThreads(numThreads, numThreads);
                var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
                using (host)
                {
                    host.Start();
                    var httpBinding = ClientHelper.GetBasicHttpBinding();
                    var factory = new System.ServiceModel.ChannelFactory<ClientContract.AsyncStreamingService.IService>(httpBinding,
                        new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/AsyncStreamingService.svc")));
                    var serviceChannel = factory.CreateChannel();

                    SlowStreamConsumingClient(serviceChannel, requests);
                    SlowMessageConsumingClient(serviceChannel, requests);
                }

                requests = 3 * requests;
                numThreads = 3 * numThreads;
            }
        }
#endif
        internal class Startup
        {
            public static string _binding = "";
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    Channels.Binding binding;
                    if ("customBinding" == _binding)
                    {
                        binding = ServiceHelper.GetCustomBinding();
                    }
                    else
                    {
                        binding = new BasicHttpBinding();
                    }
                    
                    builder.AddService<Services.AsyncStreamingService.Service>();
                    builder.AddServiceEndpoint<Services.AsyncStreamingService.Service, Services.AsyncStreamingService.IService>(binding, "/BasicWcfService/AsyncStreamingService.svc");
                });
            }
        }

        private void SlowStreamConsumingClient(ClientContract.AsyncStreamingService.IService serviceChannel, int requests)
        {
            List<IAsyncResult> responsesReceived = new List<IAsyncResult>();
            _output.WriteLine("Client sending {0} requests to get stream", requests);
            try
            {
                for (int i = 0; i < requests; i++)
                {
                    _output.WriteLine("Client Sending request..." + i);
                    Stream stream = serviceChannel.GetStream();
                    AsyncState state = new AsyncState(stream, i);
                    byte[] buffer = new byte[Common.streamBufferSize];
                    responsesReceived.Add(stream.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(SlowClientCallback), state));
                }

                Validate(responsesReceived, requests);
            }
            finally
            {
                foreach (IAsyncResult ar in responsesReceived)
                {
                    AsyncState state = (AsyncState)ar.AsyncState;
                    state.stream.Close();
                }
            }
        }

        private void SlowMessageConsumingClient(ClientContract.AsyncStreamingService.IService serviceChannel, int requests)
        {
            List<Message> responseMessages = new List<Message>();
            _output.WriteLine("Client sending {0} requests to get message", requests);
            try
            {
                for (int i = 0; i < requests; i++)
                {
                    _output.WriteLine("Client Sending request..." + i);
                    Message responseMessage = serviceChannel.GetMessage();
                    XmlDictionaryReader messageReader = responseMessage.GetReaderAtBodyContents();
                    if (responseMessage != null)
                    {
                        responseMessages.Add(responseMessage);
                    }
                }

                Validate(responseMessages, requests);
            }
            finally
            {
                foreach (Message responseMessage in responseMessages)
                {
                    responseMessage.Close();
                }
            }
        }

        private void Validate<T>(List<T> responseMessages, int requests)
        {
            _output.WriteLine("Wating for all the requests to complete");
            Thread.CurrentThread.Join(new TimeSpan(0, 0, 10));
            if (responseMessages.Count < requests)
            {
                _output.WriteLine("Error: Service did not response to expected number of requests");
                throw new ApplicationException("Service did not response to Expected number of clients");
            }
        }

        private class AsyncState
        {
            public AsyncState(Stream stream, int requestNum)
            {
                this.stream = stream;
                this.requestNum = requestNum;
            }
            public Stream stream;
            public int requestNum;
            public int totalBytesRead;
        }

        private static void SlowClientCallback(IAsyncResult ar)
        {
            AsyncState state = (AsyncState)ar.AsyncState;
            //Do not close the stream to ensure the client still holds on to the instance of the stream.
            state.stream.EndRead(ar);
        }
    }
}
