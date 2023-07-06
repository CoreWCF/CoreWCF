// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using DispatcherClient;
using Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public class ServiceKnownTypeTests
    {
        internal static System.ServiceModel.ChannelFactory<TContract> CreateFactory<TContract, TService>() where TService : class
        {
            System.ServiceModel.ChannelFactory<TContract> factory = DispatcherHelper.CreateChannelFactory<TService, TContract>(
                (services) =>
                {
                    services.AddScoped<TService>();
                });
            return factory;
        }

        internal static TContract CreateChannel<TContract>(System.ServiceModel.ChannelFactory<TContract> factory)
        {
            factory.Open();
            TContract channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            return channel;
        }

        internal static void CloseChannel<TContract>(System.ServiceModel.ChannelFactory<TContract> factory, TContract channel)
        {
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void ServiceKnownTypeTest_Ex()
        {
            var factory = ServiceKnownTypeTests.CreateFactory<IPingService_Ex, PingService_Ex>();
            var channel = ServiceKnownTypeTests.CreateChannel(factory);
            Assert.Throws<System.Xml.XmlException>(() =>
            {
                BaseMsg msg = channel.Ping("hello");
            });
            ServiceKnownTypeTests.CloseChannel(factory, channel);
        }

        [Fact]
        public static void ServiceKnownTypeTest_Inline()
        {
            var factory = ServiceKnownTypeTests.CreateFactory<IPingService_Inline, PingService_Inline>();
            var channel = ServiceKnownTypeTests.CreateChannel(factory);
            BaseMsg msg = channel.Ping("hello");
            Assert.Equal("hello", msg.msg);
            ServiceKnownTypeTests.CloseChannel(factory, channel);
        }

        [Fact]
        public static void ServiceKnownTypeTest_Inline_SSM()
        {
            var factory = ServiceKnownTypeTests.CreateFactory<IPingService_Inline_SSM, PingService_Inline_SSM>();
            var channel = ServiceKnownTypeTests.CreateChannel(factory);
            BaseMsg msg = channel.Ping("hello");
            Assert.Equal("hello", msg.msg);
            ServiceKnownTypeTests.CloseChannel(factory, channel);
        }

        [Fact]
        public static void ServiceKnownTypeTest_Method()
        {
            var factory = ServiceKnownTypeTests.CreateFactory<IPingService_Method, PingService_Method>();
            var channel = ServiceKnownTypeTests.CreateChannel(factory);
            BaseMsg msg = channel.Ping("hello");
            Assert.Equal("hello", msg.msg);
            ServiceKnownTypeTests.CloseChannel(factory, channel);
        }

        [Fact]
        public static void ServiceKnownTypeTest_Method_SSM()
        {
            var factory = ServiceKnownTypeTests.CreateFactory<IPingService_Method_SSM, PingService_Method_SSM>();
            var channel = ServiceKnownTypeTests.CreateChannel(factory);
            BaseMsg msg = channel.Ping("hello");
            Assert.Equal("hello", msg.msg);
            ServiceKnownTypeTests.CloseChannel(factory, channel);
        }
    }

    internal class PingService_Ex : PingServiceBase, IPingService_Ex { }

    [System.ServiceModel.ServiceContract]
    public interface IPingService_Ex
    {
        [System.ServiceModel.OperationContract]
        BaseMsg Ping(string msg);
    }

    internal class PingService_Inline : PingServiceBase, IPingService_Inline { }

    [System.ServiceModel.ServiceContract]
    [ServiceKnownType(typeof(DerivedMsg))]
    [System.ServiceModel.ServiceKnownType(typeof(DerivedMsg))]
    public interface IPingService_Inline
    {
        [System.ServiceModel.OperationContract]
        BaseMsg Ping(string msg);
    }

    internal class PingService_Inline_SSM : PingServiceBase, IPingService_Inline_SSM { }

    [System.ServiceModel.ServiceContract]
    [System.ServiceModel.ServiceKnownType(typeof(DerivedMsg))]
    public interface IPingService_Inline_SSM
    {
        [System.ServiceModel.OperationContract]
        BaseMsg Ping(string msg);
    }

    internal class PingService_Method : PingServiceBase, IPingService_Method { }

    [System.ServiceModel.ServiceContract]
    [ServiceKnownType("GetKnownTypes", typeof(DataProviderTypes))]
    [System.ServiceModel.ServiceKnownType("GetKnownTypes", typeof(DataProviderTypes))]
    public interface IPingService_Method
    {
        [System.ServiceModel.OperationContract]
        BaseMsg Ping(string msg);
    }

    internal class PingService_Method_SSM : PingServiceBase, IPingService_Method_SSM { }

    [System.ServiceModel.ServiceContract]
    [System.ServiceModel.ServiceKnownType("GetKnownTypes", typeof(DataProviderTypes))]
    public interface IPingService_Method_SSM
    {
        [System.ServiceModel.OperationContract]
        BaseMsg Ping(string msg);
    }

    [DataContract]
    public class BaseMsg
    {
        [DataMember]
        public string msg;
    }

    [DataContract]
    public class DerivedMsg : BaseMsg
    {
    }

    internal class PingServiceBase
    {
        public BaseMsg Ping(string msg)
        {
            return new DerivedMsg { msg = msg };
        }
    }

    public class DataProviderTypes
    {
        public static IEnumerable<Type> GetKnownTypes(ICustomAttributeProvider provider)
        {
            List<Type> lst = new List<Type>();
            lst.Add(typeof(DerivedMsg));
            return lst;
        }
    }
}
