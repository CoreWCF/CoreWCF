// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using DispatcherClient;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using CoreWCF;

namespace DependencyInjection
{
    public class ServiceLocatorFromOperationContextTests
    {
        Func<string, string> Identity = input => input;

        const string input = "ABC";

        public class SimpleServiceUsingServiceLocatorFromOperationContext : ISimpleService
        {
            public string Echo(string echo)
            {
                var serviceProvider = OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
                var identity = serviceProvider.GetService<Func<string, string>>();
                return identity(echo);
            }
        }

        [Fact]
        public void DIServicesShouldBeExposedThroughOperationContextInstanceContextExtensionsWhenServiceIsRegisteredWithinDI()
        {
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<SimpleServiceUsingServiceLocatorFromOperationContext, ISimpleService>(
              (services) =>
              {
                  services.AddSingleton(Identity);
                  services.AddTransient<SimpleServiceUsingServiceLocatorFromOperationContext>();
              });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            var result = channel.Echo(input);
            Assert.Equal(input, result);
            factory.Close();
        }

        [Fact]
        public void DIServicesShouldBeExposedThroughOperationContextInstanceContextExtensionsWhenServiceIsNotRegisteredWithinDI()
        {
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<SimpleServiceUsingServiceLocatorFromOperationContext, ISimpleService>(
              (services) =>
              {
                  services.AddSingleton(Identity);
              });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            var result = channel.Echo(input);
            Assert.Equal(input, result);
            factory.Close();
        }
    }
}
