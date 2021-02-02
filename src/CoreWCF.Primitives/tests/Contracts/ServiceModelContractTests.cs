// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Primitives.Tests.Contracts;
using DispatcherClient;
using Helpers;
using Xunit;


namespace Contracts
{
    public class ServiceModelContractTests
    {
        [Fact]
        public static void AttributeNoPropertiesContract()
        {
            System.ServiceModel.ChannelFactory<IServiceModelSimpleService> factory = DispatcherHelper.CreateChannelFactory<ServiceModelSimpleService, IServiceModelSimpleService>();
            factory.Open();
            IServiceModelSimpleService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            string echo = channel.Echo("hello");
            Assert.Equal("hello", echo);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void AttributesForMessageContract()
        {
            System.ServiceModel.ChannelFactory<IServiceModelSimpleService> factory = DispatcherHelper.CreateChannelFactory<ServiceModelSimpleService, IServiceModelSimpleService>();
            factory.Open();
            IServiceModelSimpleService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            EchoMessageResponse echo = channel.EchoWithMessageContract(new EchoMessageRequest() { Text = "Message Hello", APIKey = "DEVKEYTOTEST" });
            Assert.NotNull(echo);
            Assert.NotEmpty(echo.SayHello);
            Assert.NotEmpty(echo.SayHi);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void AttributeWithNameNamespaceActionReplyActionContract()
        {
            System.ServiceModel.ChannelFactory<IServiceModelServiceWithPropertiesSet> factory = DispatcherHelper.CreateChannelFactory<ServiceModelSimpleService, IServiceModelServiceWithPropertiesSet>();
            factory.Open();
            IServiceModelServiceWithPropertiesSet channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            string echo = channel.Echo("hello");
            Assert.Equal("hello", echo);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }
    }

    [System.ServiceModel.ServiceContract]
    public interface IServiceModelSimpleService
    {
        [System.ServiceModel.OperationContract]
        string Echo(string echo);

        [System.ServiceModel.OperationContract]
        EchoMessageResponse EchoWithMessageContract(EchoMessageRequest request);
    }

    [System.ServiceModel.ServiceContract(Name = "NotTheDefaultServiceName", Namespace = "http://tempuri.org/NotTheDefaultServiceNamespace")]
    public interface IServiceModelServiceWithPropertiesSet
    {
        [System.ServiceModel.OperationContract(Name = "NotTheDefaultOperationName", Action = "corewcf://corewcf.corewcf/OddAction", ReplyAction = "corewcf://corewcf.corewcf/OddReplyAction")]
        string Echo(string echo);
    }

    public class ServiceModelSimpleService : ServiceModelBaseService, IServiceModelSimpleService, IServiceModelServiceWithPropertiesSet { }

    public class ServiceModelBaseService
    {
        public string Echo(string echo)
        {
            return echo;
        }

        public EchoMessageResponse EchoWithMessageContract(EchoMessageRequest request)
        {
            EchoMessageResponse echoMessageResponse = new EchoMessageResponse
            {
                SayHello = "Saying Hello " + request.Text,
                SayHi = "Saying Hi " + request.Text
            };
            return echoMessageResponse;
        }
    }

}
