// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Channels;
using System.Xml.Linq;
using ClientContract;
//using CoreWCF.Channels;
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
    public class OpActionReplyActionBehaviorTests
    {
        private readonly ITestOutputHelper _output;

        public OpActionReplyActionBehaviorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("defaultaction")]
        [InlineData("customaction")]
        [InlineData("uriaction")]
        [InlineData("emptyaction")]
        [InlineData("untypedaction")]
        [InlineData("defaultreplyaction")]
        [InlineData("customreplyaction")]
        [InlineData("urireplyaction")]
        [InlineData("emptyreplyaction")]
        [InlineData("untypedreplyaction")]
        public void OperationContractActionReplyActionBehaviorTests(string variation)
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var serviceInstance = host.Services.GetService<OpActionReplyActionBehaviorService>();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IOpActionReplyActionBehavior>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/OpActionReplyActionBehaviorService.svc")));
                IOpActionReplyActionBehavior channel = factory.CreateChannel();

                switch (variation)
                {
                    case "defaultaction":
                        CallCheckDefaultAction(channel, serviceInstance);
                        break;
                    case "defaultreplyaction":
                        CallCheckDefaultReplyAction(channel);
                        break;
                    case "customaction":
                        CallCheckCustomAction(channel, serviceInstance);
                        break;
                    case "customreplyaction":
                        CallCheckCustomReplyAction(channel);
                        break;
                    case "uriaction":
                        CallCheckUriAction(channel, serviceInstance);
                        break;
                    case "urireplyaction":
                        CallCheckUriReplyAction(channel);
                        break;
                    case "emptyaction":
                        CallCheckEmptyAction(channel, serviceInstance);
                        break;
                    case "emptyreplyaction":
                        CallCheckEmptyReplyAction(channel);
                        break;
                    case "untypedaction":
                        CallCheckUntypedAction(channel, serviceInstance);
                        break;
                    case "untypedreplyaction":
                        CallCheckUntypedReplyAction(channel);
                        break;
                    default:
                        break;
                }
            }
        }

        #region Test Variations
        private void CallCheckDefaultAction(IOpActionReplyActionBehavior client, OpActionReplyActionBehaviorService serviceInstance)
        {
            int id = 1;
            string name = "Default Action";
            var guid = CallOperationWithTestId(client, (IOpActionReplyActionBehavior c) => c.TestMethodCheckDefaultAction(id, name));
            Assert.True(ValidateAction(@"http://tempuri.org/IOpActionReplyActionBehavior/TestMethodCheckDefaultAction", serviceInstance, guid));
        }

        private void CallCheckDefaultReplyAction(IOpActionReplyActionBehavior client)
        {
            int id = 1;
            string name = "Default ReplyAction";
            int result = client.TestMethodCheckDefaultReplyAction(id, name);
            Assert.Equal(id + 1, result);
        }

        private void CallCheckCustomAction(IOpActionReplyActionBehavior client, OpActionReplyActionBehaviorService serviceInstance)
        {
            int id = 1;
            string name = "Custom Action";
            var guid = CallOperationWithTestId(client, (IOpActionReplyActionBehavior c) => c.TestMethodCheckCustomAction(id, name));
            Assert.True(ValidateAction("myAction", serviceInstance, guid));
        }

        private void CallCheckCustomReplyAction(IOpActionReplyActionBehavior client)
        {
            int id = 1;
            string name = "Custom ReplyAction";
            int result = client.TestMethodCheckCustomReplyAction(id, name);
            Assert.Equal(id + 1, result);
        }

        private void CallCheckUriAction(IOpActionReplyActionBehavior client, OpActionReplyActionBehaviorService serviceInstance)
        {
            int id = 1;
            string name = "Uri Action";
            var guid = CallOperationWithTestId(client, (IOpActionReplyActionBehavior c) => c.TestMethodCheckUriAction(id, name));
            Assert.True(ValidateAction(@"http://myAction", serviceInstance, guid));
        }

        private void CallCheckUriReplyAction(IOpActionReplyActionBehavior client)
        {
            int id = 1;
            string name = "Uri ReplyAction";
            int result = client.TestMethodCheckUriReplyAction(id, name);
            Assert.Equal(id + 1, result);
        }

        private void CallCheckEmptyAction(IOpActionReplyActionBehavior client, OpActionReplyActionBehaviorService serviceInstance)
        {
            int id = 1;
            string name = "Empty Action";
            var guid = CallOperationWithTestId(client, (IOpActionReplyActionBehavior c) => c.TestMethodCheckEmptyAction(id, name));
            Assert.True(ValidateAction(@"empty action", serviceInstance, guid));
        }

        private void CallCheckEmptyReplyAction(IOpActionReplyActionBehavior client)
        {
            int id = 1;
            string name = "Empty ReplyAction";
            int result = client.TestMethodCheckEmptyReplyAction(id, name);
            Assert.Equal(id + 1, result);
        }

        private void CallCheckUntypedAction(IOpActionReplyActionBehavior client, OpActionReplyActionBehaviorService serviceInstance)
        {
            Message clientMessage = Message.CreateMessage(MessageVersion.Soap11, "myAction2");
            var guid = CallOperationWithTestId(client, (IOpActionReplyActionBehavior c) => c.TestMethodUntypedAction(clientMessage));
            Assert.True(ValidateAction(@"myAction2", serviceInstance, guid));
        }

        private void CallCheckUntypedReplyAction(IOpActionReplyActionBehavior client)
        {
            Message result = client.TestMethodCheckUntypedReplyAction();
            Assert.NotNull(result);
        }

        private Guid CallOperationWithTestId(IOpActionReplyActionBehavior client, Action<IOpActionReplyActionBehavior> clientCall)
        {
            var guid = Guid.NewGuid();
            using (System.ServiceModel.OperationContextScope scope = new System.ServiceModel.OperationContextScope((System.ServiceModel.IContextChannel)client))
            {
                System.ServiceModel.MessageHeader<Guid> header = new System.ServiceModel.MessageHeader<Guid>(guid);
                System.ServiceModel.OperationContext.Current.OutgoingMessageHeaders.Add(header.GetUntypedHeader("TestId", "http://corewcf.net/"));
                clientCall(client);
            }

            return guid;
        }

        private bool ValidateAction(string expected, OpActionReplyActionBehaviorService serviceInstance, Guid testId)
        {
            var result = serviceInstance.GetTestResult(testId);
            return result.Equals(expected);
        }

        #endregion Test Variations

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
                services.AddSingleton<OpActionReplyActionBehaviorService>();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<OpActionReplyActionBehaviorService>();
                    builder.AddServiceEndpoint<OpActionReplyActionBehaviorService, ServiceContract.IOpActionReplyActionBehavior>(new BasicHttpBinding(), "/BasicWcfService/OpActionReplyActionBehaviorService.svc");
                });
            }
        }
    }
}
