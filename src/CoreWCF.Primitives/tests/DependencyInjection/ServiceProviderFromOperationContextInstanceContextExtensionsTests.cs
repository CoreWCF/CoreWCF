// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using CoreWCF;
using DispatcherClient;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DependencyInjection
{
    public class ServiceProviderFromOperationContextInstanceContextExtensionsTests
    {
        const string input = "ABC";

        public class AssertNullServiceProviderService : ISimpleService
        {
            public string Echo(string echo)
            {
                Assert.Null(OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>());
                return echo;
            }
        }

        public class AssertNotNullServiceProviderService : ISimpleService
        {
            public string Echo(string echo)
            {
                Assert.NotNull(OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>());
                Assert.Single(OperationContext.Current.InstanceContext.Extensions.OfType<IServiceProvider>());
                return echo;
            }
        }

        [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
        public class PerCallSimpleServiceUsingServiceProviderFromOperationContext : AssertNotNullServiceProviderService { }

        [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession)]
        public class PerSessionSimpleServiceUsingServiceProviderFromOperationContext : AssertNotNullServiceProviderService { }

        [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
        public class SingleSimpleServiceUsingServiceProviderFromOperationContext : AssertNotNullServiceProviderService { }

        [Fact]
        public void ServiceProviderShouldBeExposedThroughOperationContextInstanceContextExtensionsWhenPerCallServiceIsRegisteredWithinDI()
        {
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<PerCallSimpleServiceUsingServiceProviderFromOperationContext, ISimpleService>(
              (services) =>
              {
                  services.AddTransient<PerCallSimpleServiceUsingServiceProviderFromOperationContext>();
              });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();

            channel.Echo(input);
            channel.Echo(input);

            factory.Close();
        }

        [Fact]
        public void ServiceProviderShouldBeExposedThroughOperationContextInstanceContextExtensionsWhenPerSessionServiceIsRegisteredWithinDI()
        {
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<PerSessionSimpleServiceUsingServiceProviderFromOperationContext, ISimpleService>(
              (services) =>
              {
                  services.AddTransient<PerSessionSimpleServiceUsingServiceProviderFromOperationContext>();
              });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();

            channel.Echo(input);
            channel.Echo(input);

            factory.Close();
        }

        [Fact]
        public void ServiceProviderShouldBeExposedThroughOperationContextInstanceContextExtensionsWhenSingleServiceIsRegisteredWithinDI()
        {
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<SingleSimpleServiceUsingServiceProviderFromOperationContext, ISimpleService>(
              (services) =>
              {
                  services.AddTransient<SingleSimpleServiceUsingServiceProviderFromOperationContext>();
              });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();

            channel.Echo(input);
            channel.Echo(input);

            factory.Close();
        }
    }
}
