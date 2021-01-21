// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DispatcherClient;
using Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ErrorHandling
{
    public class ExceptionHandling
    {
        [Fact]
        public static void ServiceThrowsTimeoutException()
        {
            var factory = DispatcherHelper.CreateChannelFactory<ThrowingService, ISimpleService>(
                (services) =>
                {
                    services.AddSingleton(new ThrowingService(new TimeoutException()));
                });
            factory.Open();
            var channel = factory.CreateChannel();
            System.ServiceModel.FaultException exceptionThrown = null;
            try
            {
                var echo = channel.Echo("hello");
            }
            catch (System.ServiceModel.FaultException e)
            {
                exceptionThrown = e;
            }

            Assert.NotNull(exceptionThrown);
            Assert.True(exceptionThrown.Code.IsReceiverFault);
            Assert.Equal("InternalServiceFault", exceptionThrown.Code.SubCode.Name);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static async Task AsyncServiceThrowsTimeoutExceptionBeforeAwait()
        {
            var factory = DispatcherHelper.CreateChannelFactory<ThrowingAsyncService, ISimpleAsyncService>(
                (services) =>
                {
                    services.AddSingleton(new ThrowingAsyncService(new TimeoutException(), beforeAwait: true));
                });
            factory.Open();
            var channel = factory.CreateChannel();
            ((System.ServiceModel.IClientChannel)channel).Open();
            System.ServiceModel.FaultException exceptionThrown = null;
            try
            {
                var echo = await channel.EchoAsync("hello");
            }
            catch (System.ServiceModel.FaultException e)
            {
                exceptionThrown = e;
            }

            Assert.NotNull(exceptionThrown);
            Assert.True(exceptionThrown.Code.IsReceiverFault);
            Assert.Equal("InternalServiceFault", exceptionThrown.Code.SubCode.Name);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static async Task AsyncServiceThrowsTimeoutExceptionAfterAwait()
        {
            var factory = DispatcherHelper.CreateChannelFactory<ThrowingAsyncService, ISimpleAsyncService>(
                (services) =>
                {
                    services.AddSingleton(new ThrowingAsyncService(new TimeoutException(), beforeAwait: false));
                });
            factory.Open();
            var channel = factory.CreateChannel();
            ((System.ServiceModel.IClientChannel)channel).Open();
            System.ServiceModel.FaultException exceptionThrown = null;
            try
            {
                var echo = await channel.EchoAsync("hello");
            }
            catch (System.ServiceModel.FaultException e)
            {
                exceptionThrown = e;
            }

            Assert.NotNull(exceptionThrown);
            Assert.True(exceptionThrown.Code.IsReceiverFault);
            Assert.Equal("InternalServiceFault", exceptionThrown.Code.SubCode.Name);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }
    }

    public class ThrowingService : ISimpleService
    {
        private Exception _exception;

        public ThrowingService(Exception exception)
        {
            _exception = exception;
        }
        public string Echo(string echo)
        {
            throw _exception;
        }
    }

    public class ThrowingAsyncService : ISimpleAsyncService
    {
        private Exception _exception;
        private bool _beforeAwait;

        public ThrowingAsyncService(Exception exception, bool beforeAwait)
        {
            _exception = exception;
            _beforeAwait = beforeAwait;
        }

        public async Task<string> EchoAsync(string echo)
        {
            if (_beforeAwait)
            {
                throw _exception;
            }
            await Task.Delay(100);
            throw _exception;
        }
    }
}
