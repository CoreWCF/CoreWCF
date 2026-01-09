// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Channels;
using ClientContract;
//using CoreWCF.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
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
            IHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IOpActionReplyActionBehavior>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/OpActionReplyActionBehaviorService.svc")));
                IOpActionReplyActionBehavior channel = factory.CreateChannel();

                switch (variation)
                {
                    case "defaultaction":
                        CallCheckDefaultAction(channel);
                        break;
                    case "defaultreplyaction":
                        CallCheckDefaultReplyAction(channel);
                        break;
                    case "customaction":
                        CallCheckCustomAction(channel);
                        break;
                    case "customreplyaction":
                        CallCheckCustomReplyAction(channel);
                        break;
                    case "uriaction":
                        CallCheckUriAction(channel);
                        break;
                    case "urireplyaction":
                        CallCheckUriReplyAction(channel);
                        break;
                    case "emptyaction":
                        CallCheckEmptyAction(channel);
                        break;
                    case "emptyreplyaction":
                        CallCheckEmptyReplyAction(channel);
                        break;
                    case "untypedaction":
                        CallCheckUntypedAction(channel);
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
        private void CallCheckDefaultAction(IOpActionReplyActionBehavior client)
        {
            int id = 1;
            string name = "Default Action";
            client.TestMethodCheckDefaultAction(id, name);
            Assert.True(ValidateAction(@"http://tempuri.org/IOpActionReplyActionBehavior/TestMethodCheckDefaultAction"));
        }

        private void CallCheckDefaultReplyAction(IOpActionReplyActionBehavior client)
        {
            int id = 1;
            string name = "Default ReplyAction";
            int result = client.TestMethodCheckDefaultReplyAction(id, name);
            Assert.Equal(id + 1, result);
        }

        private void CallCheckCustomAction(IOpActionReplyActionBehavior client)
        {
            int id = 1;
            string name = "Custom Action";
            client.TestMethodCheckCustomAction(id, name);
            Assert.True(ValidateAction("myAction"));
        }

        private void CallCheckCustomReplyAction(IOpActionReplyActionBehavior client)
        {
            int id = 1;
            string name = "Custom ReplyAction";
            int result = client.TestMethodCheckCustomReplyAction(id, name);
            Assert.Equal(id + 1, result);
        }

        private void CallCheckUriAction(IOpActionReplyActionBehavior client)
        {
            int id = 1;
            string name = "Uri Action";
            client.TestMethodCheckUriAction(id, name);
            Assert.True(ValidateAction(@"http://myAction"));
        }

        private void CallCheckUriReplyAction(IOpActionReplyActionBehavior client)
        {
            int id = 1;
            string name = "Uri ReplyAction";
            int result = client.TestMethodCheckUriReplyAction(id, name);
            Assert.Equal(id + 1, result);
        }

        private void CallCheckEmptyAction(IOpActionReplyActionBehavior client)
        {
            int id = 1;
            string name = "Empty Action";
            client.TestMethodCheckEmptyAction(id, name);
            Assert.True(ValidateAction(@"empty action"));
        }

        private void CallCheckEmptyReplyAction(IOpActionReplyActionBehavior client)
        {
            int id = 1;
            string name = "Empty ReplyAction";
            int result = client.TestMethodCheckEmptyReplyAction(id, name);
            Assert.Equal(id + 1, result);
        }

        private void CallCheckUntypedAction(IOpActionReplyActionBehavior client)
        {
            Message clientMessage = Message.CreateMessage(MessageVersion.Soap11, "myAction2");
            client.TestMethodUntypedAction(clientMessage);
            Assert.True(ValidateAction(@"myAction2"));
        }

        private void CallCheckUntypedReplyAction(IOpActionReplyActionBehavior client)
        {
            Message result = client.TestMethodCheckUntypedReplyAction();
            Assert.NotNull(result);
        }

        private bool ValidateAction(string expected)
        {
            string contents = System.IO.File.ReadAllText("resultAction.txt");
            return contents.Equals(expected);
        }

        #endregion Test Variations

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
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
