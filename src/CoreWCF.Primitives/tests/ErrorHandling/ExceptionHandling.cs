// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF;
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
            System.ServiceModel.FaultException exceptionThrown = Assert.Throws<System.ServiceModel.FaultException>(() =>
            {
                _ = channel.Echo("hello");
            });
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
            System.ServiceModel.FaultException exceptionThrown = await Assert.ThrowsAsync<System.ServiceModel.FaultException>(async () =>
            {
                _ = await channel.EchoAsync("hello");
            });
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
            System.ServiceModel.FaultException exceptionThrown = await Assert.ThrowsAsync<System.ServiceModel.FaultException>(async () =>
            {
                _ = await channel.EchoAsync("hello");
            });
            Assert.NotNull(exceptionThrown);
            Assert.True(exceptionThrown.Code.IsReceiverFault);
            Assert.Equal("InternalServiceFault", exceptionThrown.Code.SubCode.Name);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        [UseCulture("en-US")]
        public static void ServiceThrowsExceptionDetailsIncludedInFault()
        {
            string exceptionMessage = "This is the exception message";
            string stackTraceTopMethod = "   at ErrorHandling.ThrowingService.Echo(String echo)";
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<ThrowingDetailInFaultService, ISimpleService>(
                (services) =>
                {
                    services.AddSingleton(new ThrowingDetailInFaultService(new Exception(exceptionMessage)));
                });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            ((System.ServiceModel.IClientChannel)channel).Open();
            System.ServiceModel.FaultException<System.ServiceModel.ExceptionDetail> exceptionThrown = Assert.Throws<System.ServiceModel.FaultException<System.ServiceModel.ExceptionDetail>>(() =>
            {
                _ = channel.Echo("hello");
            });
            Assert.NotNull(exceptionThrown);
            Assert.NotNull(exceptionThrown.Detail);
            Assert.True(exceptionThrown.Code.IsReceiverFault);
            System.ServiceModel.ExceptionDetail detail = exceptionThrown.Detail;
            Assert.Equal(exceptionMessage, detail.Message);
            Assert.StartsWith(stackTraceTopMethod, detail.StackTrace);
            Assert.Equal("InternalServiceFault", exceptionThrown.Code.SubCode.Name);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class ThrowingDetailInFaultService : ThrowingService
    {
        public ThrowingDetailInFaultService(Exception exception) : base(exception) { }
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
