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
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<ThrowingService, ISimpleService>(
                (services) =>
                {
                    services.AddSingleton(new ThrowingService(new TimeoutException()));
                });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            System.ServiceModel.FaultException exceptionThrown = null;
            try
            {
                string echo = channel.Echo("hello");
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
            System.ServiceModel.ChannelFactory<ISimpleAsyncService> factory = DispatcherHelper.CreateChannelFactory<ThrowingAsyncService, ISimpleAsyncService>(
                (services) =>
                {
                    services.AddSingleton(new ThrowingAsyncService(new TimeoutException(), beforeAwait: true));
                });
            factory.Open();
            ISimpleAsyncService channel = factory.CreateChannel();
            ((System.ServiceModel.IClientChannel)channel).Open();
            System.ServiceModel.FaultException exceptionThrown = null;
            try
            {
                string echo = await channel.EchoAsync("hello");
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
            System.ServiceModel.ChannelFactory<ISimpleAsyncService> factory = DispatcherHelper.CreateChannelFactory<ThrowingAsyncService, ISimpleAsyncService>(
                (services) =>
                {
                    services.AddSingleton(new ThrowingAsyncService(new TimeoutException(), beforeAwait: false));
                });
            factory.Open();
            ISimpleAsyncService channel = factory.CreateChannel();
            ((System.ServiceModel.IClientChannel)channel).Open();
            System.ServiceModel.FaultException exceptionThrown = null;
            try
            {
                string echo = await channel.EchoAsync("hello");
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
        private readonly Exception _exception;

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
        private readonly Exception _exception;
        private readonly bool _beforeAwait;

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
