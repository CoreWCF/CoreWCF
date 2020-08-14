﻿using ClientContract;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ServiceModel;
using Xunit;
using Xunit.Abstractions;
using System.IO;

namespace CoreWCF.Http.Tests
{
    public class StreamingServiceTests
    {
        private ITestOutputHelper _output;
        public const string TestString = "String to test";
        public const string FileToSend = "temp.dat";

        public StreamingServiceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        static StreamingServiceTests()
        {
            File.WriteAllText(FileToSend, "Streaming test file content.");
        }

        [Theory]
        //[InlineData("VoidStreamService")]
        //[InlineData("RefStreamService")] //issue: https://github.com/CoreWCF/CoreWCF/issues/196
        //[InlineData("StreamInOutService")]
        [InlineData("StreamStreamAsyncService")]
        [InlineData("InFileStreamService")]
        [InlineData("ReturnFileStreamService")]
        [InlineData("MessageContractStreamInOutService")]
        [InlineData("MessageContractStreamMutipleOperationsService")]
        public void StreamingInputOutputTest(string method)
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            Startup._method = method;
            using (host)
            {
                host.Start();
                switch (method)
                {
                    case "VoidStreamService":
                        VoidStreamService();
                        break;
                    case "StreamStreamAsyncService":
                        StreamStreamAsyncService();
                        break;
                    case "RefStreamService":
                        RefStreamService();
                        break;
                    case "StreamInOutService":
                        StreamInOutService();
                        break;
                    case "InFileStreamService":
                        InFileStreamService();
                        break;
                    case "ReturnFileStreamService":
                        ReturnFileStreamService();
                        break;
                    case "MessageContractStreamInOutService":
                        MessageContractStreamInOutService();
                        break;
                    case "MessageContractStreamMutipleOperationsService":
                        MessageContractStreamMutipleOperationsService();
                        break;
                    default:
                        break;
                }
            }
        }

        T GetProxy<T>()
        {
            var httpBinding = ClientHelper.GetBufferedModeBinding();
            ChannelFactory<T> channelFactory = new ChannelFactory<T>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/StreamingInputOutputService.svc")));
            T proxy = channelFactory.CreateChannel();
            return proxy;
        }

        private void StreamStreamAsyncService()
        {
            var clientProxy = GetProxy<IStreamStreamAsyncService>();
            Stream input = new ClientHelper.NoneSerializableStream();
            ClientHelper.PopulateStreamWithStringBytes(input, TestString);
            string response = ClientHelper.GetStringFrom(clientProxy.TwoWayMethodAsync(input).GetAwaiter().GetResult());
            Assert.Equal(TestString, response);
        }

        private void RefStreamService()
        {
            var clientProxy = GetProxy<IRefStreamService>();
            Stream input = new ClientHelper.NoneSerializableStream();
            ClientHelper.PopulateStreamWithStringBytes(input, TestString);
            clientProxy.Operation(ref input);
            string response = ClientHelper.GetStringFrom(input);
            Assert.Equal(TestString + "/" + TestString, response);
        }

        private void StreamInOutService()
        {
            var clientProxy = GetProxy<IStreamInOutService>();
            Stream input = new ClientHelper.NoneSerializableStream();
            ClientHelper.PopulateStreamWithStringBytes(input, TestString);
            clientProxy.Operation(input, out input);
            string response = ClientHelper.GetStringFrom(input);
            Assert.Equal(TestString + "/" + TestString, response);
        }

        private void InFileStreamService()
        {
            var clientProxy = GetProxy<IMessageContractStreamInReturnService>();
            if (!File.Exists(FileToSend))
            {
                throw new FileNotFoundException("Could not find file " + FileToSend);
            }

            using (FileStream file = File.OpenRead(FileToSend))
            {
                long fileLength = file.Length;
                _output.WriteLine("File size is " + fileLength);
                Stream input = file;
                var message = new MessageContractStreamNoHeader
                {
                    stream = input
                };
                MessageContractStreamOneIntHeader output = clientProxy.Operation(message);
                string response = ClientHelper.GetStringFrom(output.input);
                long size = long.Parse(response);
                Assert.Equal(fileLength, size);
            }
        }

        private void ReturnFileStreamService()
        {
            var clientProxy = GetProxy<IMessageContractStreamInReturnService>();
            MessageContractStreamNoHeader message = new MessageContractStreamNoHeader();
            message.stream = ClientHelper.GetStreamWithStringBytes(TestString);

            using (Stream stream = clientProxy.Operation(message).input)
            {
                long size = 0, read = 0;
                const int BUFFER = 1000;
                byte[] buffer = new byte[BUFFER];
                do
                {
                    read = stream.Read(buffer, 0, BUFFER);
                    size += read;
                } while (read > 0);

                FileStream file = File.OpenRead("temp.dat");
                Assert.Equal(file.Length, size);
            }
        }

        private void MessageContractStreamInOutService()
        {
            var clientProxy = GetProxy<IMessageContractStreamInReturnService>();
            MessageContractStreamNoHeader input = ClientHelper.GetMessageContractStreamNoHeader(TestString);
            MessageContractStreamOneIntHeader output = clientProxy.Operation(input);
            string response = ClientHelper.GetStringFrom(output.input);
            Assert.Equal(TestString, response);
        }

        private void MessageContractStreamMutipleOperationsService()
        {
            var clientProxy = GetProxy<IMessageContractStreamMutipleOperationsService>();
            Stream input = ClientHelper.GetStreamWithStringBytes(TestString);
            MessageContractStreamOneIntHeader message = new MessageContractStreamOneIntHeader();
            message.input = input;
            MessageContractStreamTwoHeaders output = clientProxy.Operation2(message);
            string response = ClientHelper.GetStringFrom(output.Stream);

            MessageContractStreamOneStringHeader message2 = new MessageContractStreamOneStringHeader();
            message2.input = ClientHelper.GetStreamWithStringBytes(TestString);
            MessageContractStreamNoHeader output2 = clientProxy.Operation1(message2);
            string response2 = ClientHelper.GetStringFrom(output2);

            Assert.Equal(TestString, response);
            Assert.Equal(TestString, response2);
        }

        private void VoidStreamService()
        {
            var clientProxy = GetProxy<IVoidStreamService>();
            Stream input = ClientHelper.GetStreamWithStringBytes(TestString);
            clientProxy.Operation(input);
        }

        internal class Startup
        {
            public static string _method;
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    switch (_method)
                    {
                        case "VoidStreamService":
                            builder.AddService<Services.VoidStreamService>();
                            builder.AddServiceEndpoint<Services.VoidStreamService, ServiceContract.IVoidStreamService>(new BasicHttpBinding(), "/BasicWcfService/StreamingInputOutputService.svc");
                            break;
                        case "StreamStreamAsyncService":
                            builder.AddService<Services.StreamStreamAsyncService>();
                            builder.AddServiceEndpoint<Services.StreamStreamAsyncService, ServiceContract.IStreamStreamAsyncService>(new BasicHttpBinding(), "/BasicWcfService/StreamingInputOutputService.svc");
                            break;
                        case "RefStreamService":
                            builder.AddService<Services.RefStreamService>();
                            builder.AddServiceEndpoint<Services.RefStreamService, ServiceContract.IRefStreamService>(new BasicHttpBinding(), "/BasicWcfService/StreamingInputOutputService.svc");
                            break;
                        case "StreamInOutService":
                            builder.AddService<Services.StreamInOutService>();
                            builder.AddServiceEndpoint<Services.StreamInOutService, ServiceContract.IStreamInOutService>(new BasicHttpBinding(), "/BasicWcfService/StreamingInputOutputService.svc");
                            break;
                        case "InFileStreamService":
                            builder.AddService<Services.InFileStreamService>();
                            builder.AddServiceEndpoint<Services.InFileStreamService, ServiceContract.IMessageContractStreamInReturnService>(new BasicHttpBinding(), "/BasicWcfService/StreamingInputOutputService.svc");
                            break;
                        case "ReturnFileStreamService":
                            builder.AddService<Services.ReturnFileStreamService>();
                            builder.AddServiceEndpoint<Services.ReturnFileStreamService, ServiceContract.IMessageContractStreamInReturnService>(new BasicHttpBinding(), "/BasicWcfService/StreamingInputOutputService.svc");
                            break;
                        case "MessageContractStreamInOutService":
                            builder.AddService<Services.MessageContractStreamInOutService>();
                            builder.AddServiceEndpoint<Services.MessageContractStreamInOutService, ServiceContract.IMessageContractStreamInReturnService>(new BasicHttpBinding(), "/BasicWcfService/StreamingInputOutputService.svc");
                            break;
                        case "MessageContractStreamMutipleOperationsService":
                            builder.AddService<Services.MessageContractStreamMutipleOperationsService>();
                            builder.AddServiceEndpoint<Services.MessageContractStreamMutipleOperationsService, ServiceContract.IMessageContractStreamMutipleOperationsService>(new BasicHttpBinding(), "/BasicWcfService/StreamingInputOutputService.svc");
                            break;
                        default:
                            break;
                    }
                });
            }
        }
    }
}
