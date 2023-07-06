// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using DispatcherClient;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public class InstanceContextIsSingletonTests
    {
        const string input = "ABC";

        public class AssertInstanceContextIsSingletonService : ISimpleService
        {
            public string Echo(string echo)
            {
                Assert.True(OperationContext.Current.InstanceContext.IsSingleton);
                return echo;
            }
        }

        public class AssertInstanceContextIsNotSingletonService : ISimpleService
        {
            public string Echo(string echo)
            {
                Assert.False(OperationContext.Current.InstanceContext.IsSingleton);
                return echo;
            }
        }

        [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
        public class PerCallSimpleService : AssertInstanceContextIsNotSingletonService { }

        [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession)]
        public class PerSessionSimpleService : AssertInstanceContextIsNotSingletonService { }

        [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
        public class SingleSimpleService : AssertInstanceContextIsSingletonService { }

        [Fact]
        public void IsSingletonShouldBeFalseWhenInstanceContextModeIsPerCallAndServiceIsRegisteredInDI()
        {
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<PerCallSimpleService, ISimpleService>(
              (services) =>
              {
                  services.AddTransient<PerCallSimpleService>();
              });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();

            channel.Echo(input);

            factory.Close();
        }

        [Fact]
        public void IsSingletonShouldBeFalseWhenInstanceContextModeIsPerCall()
        {
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<PerCallSimpleService, ISimpleService>();
            factory.Open();
            ISimpleService channel = factory.CreateChannel();

            channel.Echo(input);

            factory.Close();
        }

        [Fact]
        public void IsSingletonShouldBeFalseWhenInstanceContextModeIsPerSessionAndServiceIsRegisteredInDI()
        {
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<PerSessionSimpleService, ISimpleService>(
              (services) =>
              {
                  services.AddTransient<PerSessionSimpleService>();
              });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();

            channel.Echo(input);

            factory.Close();
        }

        [Fact]
        public void IsSingletonShouldBeFalseWhenInstanceContextModeIsPerSession()
        {
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<PerSessionSimpleService, ISimpleService>();
            factory.Open();
            ISimpleService channel = factory.CreateChannel();

            channel.Echo(input);

            factory.Close();
        }

        [Fact]
        public void IsSingletonShouldBeTrueWhenInstanceContextModeIsSingleAndServiceIsRegisteredInDI()
        {
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<SingleSimpleService, ISimpleService>(
              (services) =>
              {
                  services.AddTransient<SingleSimpleService>();
              });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();

            channel.Echo(input);

            factory.Close();
        }

        [Fact]
        public void IsSingletonShouldBeTrueWhenInstanceContextModeIsSingle()
        {
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<SingleSimpleService, ISimpleService>();
            factory.Open();
            ISimpleService channel = factory.CreateChannel();

            channel.Echo(input);

            factory.Close();
        }
    }
}
