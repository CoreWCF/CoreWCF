// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using DispatcherClient;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using CoreWCF;
using CoreWCF.Dispatcher;

namespace DependencyInjection
{
    public class ImplementsServiceContractAttributeTests
    {
        [ImplementsServiceContract(typeof(ISimpleService))]
        [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
        public class SimpleServiceWithImplementsServiceContractAttribute
        {
            public string Echo(string echo) => echo;
            public string EchoFromServices(string echo, [FromServices] Func<string, string> identity) => identity(echo);
        }

        [Fact]
        public void ShouldNotThrow()
        {
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<SimpleServiceWithImplementsServiceContractAttribute, ISimpleService>(
              (services) =>
              {
                  services.AddTransient<SimpleServiceWithImplementsServiceContractAttribute>();
              });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            const string input = "ABC";

            try
            {
                var result = channel.Echo(input);
                Assert.Equal("ABC", result);
            }
            catch (Exception e)
            {
                throw;
            }
            finally
            {
                factory.Close();
            }
        }
    }
}
