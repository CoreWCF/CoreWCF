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
        public static void ServiceKnownTypeException()
        {
            var factory = ServiceKnownTypeTests.CreateFactory<IPingServiceEx, PingServiceEx>();
            var channel = ServiceKnownTypeTests.CreateChannel(factory);
            Assert.Throws<System.Xml.XmlException>(() =>
            {
                BaseMsg msg = channel.Ping("hello");
            });
            ServiceKnownTypeTests.CloseChannel(factory, channel);
        }

        [Fact]
        public static void ServiceKnownTypeTest1()
        {
            var factory = ServiceKnownTypeTests.CreateFactory<IPingService1, PingService1>();
            var channel = ServiceKnownTypeTests.CreateChannel(factory);
            BaseMsg msg = channel.Ping("hello");
            Assert.Equal("hello", msg.msg);
            ServiceKnownTypeTests.CloseChannel(factory, channel);
        }

        [Fact]
        public static void ServiceKnownTypeTest2()
        {
            var factory = ServiceKnownTypeTests.CreateFactory<IPingService2, PingService2>();
            var channel = ServiceKnownTypeTests.CreateChannel(factory);
            BaseMsg msg = channel.Ping("hello");
            Assert.Equal("hello", msg.msg);
            ServiceKnownTypeTests.CloseChannel(factory, channel);
        }

        [Fact]
        public static void ServiceKnownTypeTest3()
        {
            var factory = ServiceKnownTypeTests.CreateFactory<IPingService3, PingService3>();
            var channel = ServiceKnownTypeTests.CreateChannel(factory);
            BaseMsg msg = channel.Ping("hello");
            Assert.Equal("hello", msg.msg);
            ServiceKnownTypeTests.CloseChannel(factory, channel);
        }

        [Fact]
        public static void ServiceKnownTypeTest4()
        {
            var factory = ServiceKnownTypeTests.CreateFactory<IPingService4, PingService4>();
            var channel = ServiceKnownTypeTests.CreateChannel(factory);
            BaseMsg msg = channel.Ping("hello");
            Assert.Equal("hello", msg.msg);
            ServiceKnownTypeTests.CloseChannel(factory, channel);
        }
    }

    internal class PingServiceEx : IPingServiceEx
    {
        public BaseMsg Ping(string msg)
        {
            return new DerivedMsg { msg = msg };
        }

    }

    [ServiceContract]
    [System.ServiceModel.ServiceContract]
    public interface IPingServiceEx
    {
        [OperationContract]
        [System.ServiceModel.OperationContract]
        BaseMsg Ping(string msg);

    }

    internal class PingService1 : IPingService1
    {
        public BaseMsg Ping(string msg)
        {
            return new DerivedMsg { msg = msg };
        }

    }

    [ServiceContract]
    [System.ServiceModel.ServiceContract]
    [ServiceKnownType(typeof(DerivedMsg))]
    [System.ServiceModel.ServiceKnownType(typeof(DerivedMsg))]
    public interface IPingService1
    {
        [OperationContract]
        [System.ServiceModel.OperationContract]
        BaseMsg Ping(string msg);

    }

    internal class PingService2 : IPingService2
    {
        public BaseMsg Ping(string msg)
        {
            return new DerivedMsg { msg = msg };
        }

    }

    [ServiceContract]
    [System.ServiceModel.ServiceContract]
    [System.ServiceModel.ServiceKnownType(typeof(DerivedMsg))]
    public interface IPingService2
    {
        [OperationContract]
        [System.ServiceModel.OperationContract]
        BaseMsg Ping(string msg);

    }

    internal class PingService3 : IPingService3
    {
        public BaseMsg Ping(string msg)
        {
            return new DerivedMsg { msg = msg };
        }

    }

    [ServiceContract]
    [System.ServiceModel.ServiceContract]
    [ServiceKnownType("GetKnownTypes", typeof(DataProviderTypes))]
    [System.ServiceModel.ServiceKnownType("GetKnownTypes", typeof(DataProviderTypes))]
    public interface IPingService3
    {
        [OperationContract]
        [System.ServiceModel.OperationContract]
        BaseMsg Ping(string msg);

    }

    internal class PingService4 : IPingService4
    {
        public BaseMsg Ping(string msg)
        {
            return new DerivedMsg { msg = msg };
        }

    }

    [ServiceContract]
    [System.ServiceModel.ServiceContract]
    [System.ServiceModel.ServiceKnownType("GetKnownTypes", typeof(DataProviderTypes))]
    public interface IPingService4
    {
        [OperationContract]
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

    [DataContract]
    public class DerivedMsg2 : BaseMsg
    {
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
