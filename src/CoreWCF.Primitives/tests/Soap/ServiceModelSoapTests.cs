// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Primitives.Tests.Soap;
using DispatcherClient;
using Helpers;
using Xunit;

namespace Soap
{
    public class ServiceModelSoapTests
    {
        [Fact]
        public static void Echo_OperationFormatUseEncoded()
        {
            System.ServiceModel.ChannelFactory<IEchoSoapService> factory = DispatcherHelper.CreateChannelFactory<EchoSoapService, IEchoSoapService>();
            factory.Open();
            IEchoSoapService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            string echo = channel.EchoEncoded("hello");
            Assert.Equal("hello", echo);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void Echo_OperationFormatUseLiteral()
        {
            System.ServiceModel.ChannelFactory<IEchoSoapService> factory = DispatcherHelper.CreateChannelFactory<EchoSoapService, IEchoSoapService>();
            factory.Open();
            IEchoSoapService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            string echo = channel.EchoLiteral("hello");
            Assert.Equal("hello", echo);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void ComplexMessage_OperationFormatUseEncoded()
        {
            System.ServiceModel.ChannelFactory<IEchoSoapService> factory = DispatcherHelper.CreateChannelFactory<EchoSoapService, IEchoSoapService>();
            factory.Open();
            IEchoSoapService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            var expected = new ComplexMessage
            {
                Date = System.DateTime.Now,
                Entities = new InnerComplexMessage[]
                {
                    new InnerComplexMessage
                    {
                        Guid = System.Guid.NewGuid(),
                    },
                },
            };
            ComplexMessage actual = channel.GetComplexMessageEncoded(expected);
            Assert.Equal(expected, actual);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void ComplexMessage_OperationFormatUseLiteral()
        {
            System.ServiceModel.ChannelFactory<IEchoSoapService> factory = DispatcherHelper.CreateChannelFactory<EchoSoapService, IEchoSoapService>();
            factory.Open();
            IEchoSoapService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            var expected = new ComplexMessage
            {
                Date = System.DateTime.Now,
                Entities = new InnerComplexMessage[]
                {
                    new InnerComplexMessage
                    {
                        Guid = System.Guid.NewGuid(),
                    },
                },
            };
            ComplexMessage actual = channel.GetComplexMessageLiteral(expected);
            Assert.Equal(expected, actual);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }
    }
}
