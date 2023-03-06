// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.ServiceModel;
using System.Threading.Tasks;
using ClientContract;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class StreamingServiceTests
    {
        private readonly ITestOutputHelper _output;
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
        public async Task StreamingInputOutputTest(string method)
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            Startup._method = method;
            using (host)
            {
                await host.StartAsync();
                switch (method)
                {
                    case "VoidStreamService":
                        VoidStreamService(host);
                        break;
                    case "StreamStreamAsyncService":
                        StreamStreamAsyncService(host);
                        break;
                    case "RefStreamService":
                        RefStreamService(host);
                        break;
                    case "StreamInOutService":
                        StreamInOutService(host);
                        break;
                    case "InFileStreamService":
                        InFileStreamService(host);
                        break;
                    case "ReturnFileStreamService":
                        ReturnFileStreamService(host);
                        break;
                    case "MessageContractStreamInOutService":
                        MessageContractStreamInOutService(host);
                        break;
                    case "MessageContractStreamMutipleOperationsService":
                        MessageContractStreamMutipleOperationsService(host);
                        break;
                    default:
                        break;
                }
            }
        }

        private T GetProxy<T>(IWebHost host)
        {
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            ChannelFactory<T> channelFactory = new ChannelFactory<T>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/StreamingInputOutputService.svc")));
            T proxy = channelFactory.CreateChannel();
            return proxy;
        }

        private void StreamStreamAsyncService(IWebHost host)
        {
            IStreamStreamAsyncService clientProxy = GetProxy<IStreamStreamAsyncService>(host);
            Stream input = new ClientHelper.NoneSerializableStream();
            ClientHelper.PopulateStreamWithStringBytes(input, TestString);
            string response = ClientHelper.GetStringFrom(clientProxy.TwoWayMethodAsync(input).GetAwaiter().GetResult());
            Assert.Equal(TestString, response);
        }

        private void RefStreamService(IWebHost host)
        {
            IRefStreamService clientProxy = GetProxy<IRefStreamService>(host);
            Stream input = new ClientHelper.NoneSerializableStream();
            ClientHelper.PopulateStreamWithStringBytes(input, TestString);
            clientProxy.Operation(ref input);
            string response = ClientHelper.GetStringFrom(input);
            Assert.Equal(TestString + "/" + TestString, response);
        }

        private void StreamInOutService(IWebHost host)
        {
            IStreamInOutService clientProxy = GetProxy<IStreamInOutService>(host);
            Stream input = new ClientHelper.NoneSerializableStream();
            ClientHelper.PopulateStreamWithStringBytes(input, TestString);
            clientProxy.Operation(input, out input);
            string response = ClientHelper.GetStringFrom(input);
            Assert.Equal(TestString + "/" + TestString, response);
        }

        private void InFileStreamService(IWebHost host)
        {
            IMessageContractStreamInReturnService clientProxy = GetProxy<IMessageContractStreamInReturnService>(host);
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

        private void ReturnFileStreamService(IWebHost host)
        {
            IMessageContractStreamInReturnService clientProxy = GetProxy<IMessageContractStreamInReturnService>(host);
            MessageContractStreamNoHeader message = new MessageContractStreamNoHeader
            {
                stream = ClientHelper.GetStreamWithStringBytes(TestString)
            };

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

        private void MessageContractStreamInOutService(IWebHost host)
        {
            IMessageContractStreamInReturnService clientProxy = GetProxy<IMessageContractStreamInReturnService>(host);
            MessageContractStreamNoHeader input = ClientHelper.GetMessageContractStreamNoHeader(TestString);
            MessageContractStreamOneIntHeader output = clientProxy.Operation(input);
            string response = ClientHelper.GetStringFrom(output.input);
            Assert.Equal(TestString, response);
        }

        private void MessageContractStreamMutipleOperationsService(IWebHost host)
        {
            IMessageContractStreamMutipleOperationsService clientProxy = GetProxy<IMessageContractStreamMutipleOperationsService>(host);
            Stream input = ClientHelper.GetStreamWithStringBytes(TestString);
            MessageContractStreamOneIntHeader message = new MessageContractStreamOneIntHeader
            {
                input = input
            };
            MessageContractStreamTwoHeaders output = clientProxy.Operation2(message);
            string response = ClientHelper.GetStringFrom(output.Stream);

            MessageContractStreamOneStringHeader message2 = new MessageContractStreamOneStringHeader
            {
                input = ClientHelper.GetStreamWithStringBytes(TestString)
            };
            MessageContractStreamNoHeader output2 = clientProxy.Operation1(message2);
            string response2 = ClientHelper.GetStringFrom(output2);

            Assert.Equal(TestString, response);
            Assert.Equal(TestString, response2);
        }

        private void VoidStreamService(IWebHost host)
        {
            IVoidStreamService clientProxy = GetProxy<IVoidStreamService>(host);
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

            public void Configure(IApplicationBuilder app)
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
