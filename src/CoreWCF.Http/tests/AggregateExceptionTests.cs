// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class AggregateExceptionTests
    {
        private readonly ITestOutputHelper _output;
        public AggregateExceptionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("ServiceOpWithChainedTasks_ThrowFaultExceptionInOneTask")]
        [InlineData("ServiceOpWithChainedTasks_ThrowFaultExceptionInOneTask_WithTask")]
        [InlineData("ServiceOpWithMultipleTasks")]
        [InlineData("ServiceOpWithMultipleTasks_WithTask")]
        [InlineData("SimpleOperationThrowingFault")]
        [InlineData("SimpleOperationThrowingFault_WithTask")]
        public async Task ServiceOp_ThrowsFaultException(string serviceOpType)
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<AggregateExceptionStartup>(_output).Build();
            using (host)
            {
                await host.StartAsync();
                ClientContract.IAggregateExceptionService sampleServiceClient = ClientHelper.GetProxy<ClientContract.IAggregateExceptionService>();
                try
                {
                    switch (serviceOpType)
                    {
                        case "SimpleOperationThrowingFault":
                            sampleServiceClient.SimpleOperationThrowingFault();
                            break;
                        case "SimpleOperationThrowingFault_WithTask":
                            {
                                Task task = sampleServiceClient.SimpleOperationThrowingFaultAsync();
                                task.Wait();
                                break;
                            }
                        case "ServiceOpWithMultipleTasks":
                            sampleServiceClient.ServiceOpWithMultipleTasks();
                            break;
                        case "ServiceOpWithMultipleTasks_WithTask":
                            {
                                Task task2 = sampleServiceClient.ServiceOpWithMultipleTasksAsync();
                                task2.Wait();
                                break;
                            }
                        case "ServiceOpWithChainedTasks_ThrowFaultExceptionInOneTask":
                            sampleServiceClient.ServiceOpWithChainedTasks_ThrowFaultExceptionInOneTask();
                            break;
                        case "ServiceOpWithChainedTasks_ThrowFaultExceptionInOneTask_WithTask":
                            {
                                Task task3 = sampleServiceClient.ServiceOpWithChainedTasks_ThrowFaultExceptionInOneTaskAsync();
                                task3.Wait();
                                break;
                            }
                    }
                    throw new Exception("Expected fault but got result successfully.");
                }
                catch (System.ServiceModel.FaultException<ClientContract.SampleServiceFault> faultEx)
                {
                    VerifyFaultThrown(faultEx);
                }
                catch (AggregateException ex)
                {
                    System.ServiceModel.FaultException<ClientContract.SampleServiceFault> faultEx2 = ex.InnerExceptions[0] as System.ServiceModel.FaultException<ClientContract.SampleServiceFault>;
                    VerifyFaultThrown(faultEx2);
                }
                catch (Exception ex2)
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine("Expected a fault exception of type 'SampleServiceFault' but got the following.");
                    stringBuilder.Append("Message: " + ex2.GetType().ToString());
                    stringBuilder.AppendLine(ex2.Message);
                    stringBuilder.Append("StackTrace: ");
                    stringBuilder.AppendLine(ex2.StackTrace);
                    throw new Exception(stringBuilder.ToString());
                }
            }
        }

        private void VerifyFaultThrown(System.ServiceModel.FaultException<ClientContract.SampleServiceFault> faultEx)
        {
            if (faultEx == null)
            {
                throw new ArgumentNullException(nameof(faultEx));
            }
            ClientContract.SampleServiceFault detail = faultEx.Detail;
            Assert.True(detail.ID.Equals("101") && detail.Message.Equals("Error has occurred while performing an operation."));
        }
    }

    internal class AggregateExceptionStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<Services.AggregateExceptionService>();
                builder.AddServiceEndpoint<Services.AggregateExceptionService, IAggregateExceptionService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
            });
        }
    }
}
