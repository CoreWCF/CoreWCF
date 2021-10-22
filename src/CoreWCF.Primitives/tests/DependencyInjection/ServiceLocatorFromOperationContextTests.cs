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
        Func<string, string> Reverse = input =>
        {
            char[] charArray = input.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        };  

        public class SimpleServiceUsingServiceLocatorFromOperationContext : ISimpleService
        {
            public string Echo(string echo)
            {
                var serviceProvider = OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
                var reverse = serviceProvider.GetService<Func<string, string>>();
                return reverse(echo);
            }
        }

        [Fact]
        public void DIServicesShouldBeExposedThroughOperationContextInstanceContextExtensionsWhenServiceIsRegisteredWithinDI()
        {
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<SimpleServiceUsingServiceLocatorFromOperationContext, ISimpleService>(
              (services) =>
              {
                  services.AddSingleton(Reverse);
                  services.AddTransient<SimpleServiceUsingServiceLocatorFromOperationContext>();
              });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            const string input = "ABC";
            var result = channel.Echo(input);
            Assert.Equal("CBA", result);
            factory.Close();
        }

        [Fact]
        public void DIServicesShouldBeExposedThroughOperationContextInstanceContextExtensionsWhenServiceIsNotRegisteredWithinDI()
        {
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<SimpleServiceUsingServiceLocatorFromOperationContext, ISimpleService>(
              (services) =>
              {
                  services.AddSingleton(Reverse);
              });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            const string input = "ABC";
            var result = channel.Echo(input);
            Assert.Equal("CBA", result);
            factory.Close();
        }
    }
}
