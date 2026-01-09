// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.NetTcp.Tests
{
    /// <summary>
    /// Verifies the effect of <see cref="ServiceBehaviorAttribute.UseSynchronizationContext"/> on asynchronous operations.
    /// </summary>
    public class ServiceBehaviorUseSynchronizationContextAsyncTests
    {
        public const string RelativeAddress = "/nettcp.svc/UseSynchronizationContextAsync";
        private readonly ITestOutputHelper _output;

        public ServiceBehaviorUseSynchronizationContextAsyncTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// This test exists only to demonstrate what the UIFact attribute does.
        /// </summary>
        [UIFact]
        public async Task UIFactTestAsync()
        {
            int originalThread = Environment.CurrentManagedThreadId;
            await Task.Yield();
            Assert.Equal(originalThread, Environment.CurrentManagedThreadId);
        }

        [UIFact]
        public async Task UseSynchronizationContextTrueWithSynchronizationContextTestAsync()
        {
            SynchronizationContext orgSynchronizationContext = SynchronizationContext.Current;
            Assert.NotNull(orgSynchronizationContext); // Set by the UIFact attribute
            int orgManagedThreadId = Environment.CurrentManagedThreadId;

            AsyncServiceWithUseSynchronizationContextTrue serviceInstance =
                await RunAsync<AsyncServiceWithUseSynchronizationContextTrue>();

            Assert.Same(orgSynchronizationContext, serviceInstance.SynchronizationContextWhenCreated);
            Assert.Same(orgSynchronizationContext, serviceInstance.SynchronizationContextWhenCalled);
            Assert.Equal(orgManagedThreadId, serviceInstance.ManagedThreadIdWhenCalled);
        }

        [Fact]
        public async Task UseSynchronizationContextTrueWithoutSynchronizationContextTestAsync()
        {
            await RunWithoutSynchronizationContextAsync(async () =>
            {
                Assert.Null(SynchronizationContext.Current);

                AsyncServiceWithUseSynchronizationContextTrue serviceInstance =
                    await RunAsync<AsyncServiceWithUseSynchronizationContextTrue>();

                Assert.Null(serviceInstance.SynchronizationContextWhenCreated);
                Assert.Null(serviceInstance.SynchronizationContextWhenCalled);
                // NOTE: In this case, the original thread *can* be used when running the service,
                // so we can't verify that it differs.
            });
        }

        [UIFact]
        public async Task UseSynchronizationContextFalseWithSynchronizationContextTestAsync()
        {
            Assert.NotNull(SynchronizationContext.Current); // Set by the UIFact attribute

            AsyncServiceWithUseSynchronizationContextFalse serviceInstance =
                await RunAsync<AsyncServiceWithUseSynchronizationContextFalse>();

            Assert.Null(serviceInstance.SynchronizationContextWhenCreated);
            Assert.Null(serviceInstance.SynchronizationContextWhenCalled);
            // NOTE: In this case, the original thread *can* be used when running the service,
            // so we can't verify that it differs.
        }

        [Fact]
        public async Task UseSynchronizationContextFalseWithoutSynchronizationContextTestAsync()
        {
            await RunWithoutSynchronizationContextAsync(async () =>
            {
                Assert.Null(SynchronizationContext.Current);

                AsyncServiceWithUseSynchronizationContextFalse serviceInstance =
                    await RunAsync<AsyncServiceWithUseSynchronizationContextFalse>();

                Assert.Null(serviceInstance.SynchronizationContextWhenCreated);
                Assert.Null(serviceInstance.SynchronizationContextWhenCalled);
                // NOTE: In this case, the original thread *can* be used when running the service,
                // so we can't verify that it differs.
            });
        }

        private static async Task RunWithoutSynchronizationContextAsync(Func<Task> action)
        {
            SynchronizationContext orgSynchronizationContext = SynchronizationContext.Current;
            Assert.NotNull(orgSynchronizationContext); // Set by the Fact attribute
            SynchronizationContext.SetSynchronizationContext(null);

            try
            {
                await action();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(orgSynchronizationContext);
            }
        }

        private async Task<TService> RunAsync<TService>()
            where TService : class
        {
            IHost host = ServiceHelper.CreateWebHostBuilder<Startup<TService>>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<IAsyncService> factory = null;
                IAsyncService channel = null;
                await host.StartAsync();
                try
                {
                    System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding();
                    System.ServiceModel.EndpointAddress remoteAddress = new(host.GetNetTcpAddressInUse() + RelativeAddress);
                    factory = new System.ServiceModel.ChannelFactory<IAsyncService>(binding, remoteAddress);
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    await channel.DoStuffAsync();
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }

                TService serviceInstance = host.Services.GetRequiredService<TService>();
                return serviceInstance;
            }
        }

        private class Startup<TService>
            where TService : class
        {
            public static void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
                services.AddSingleton<TService>();
            }

            public static void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<TService>();
                    builder.AddServiceEndpoint<TService, IAsyncService>(
                        new NetTcpBinding(SecurityMode.None),
                        RelativeAddress);
                });
            }
        }

        [System.ServiceModel.ServiceContract]
        private interface IAsyncService
        {
            [System.ServiceModel.OperationContract]
            Task DoStuffAsync();
        }

        private abstract class AsyncServiceBase : IAsyncService
        {
            protected AsyncServiceBase()
            {
                SynchronizationContextWhenCreated = SynchronizationContext.Current;
            }

            public SynchronizationContext SynchronizationContextWhenCreated { get; private set; }

            public SynchronizationContext SynchronizationContextWhenCalled { get; private set; }

            public int? ManagedThreadIdWhenCalled { get; private set; }

            public async Task DoStuffAsync()
            {
                SynchronizationContextWhenCalled = SynchronizationContext.Current;
                ManagedThreadIdWhenCalled = Environment.CurrentManagedThreadId;
                await Task.Yield();
                return;
            }
        }

        [ServiceBehavior(UseSynchronizationContext = true)]
        private class AsyncServiceWithUseSynchronizationContextTrue : AsyncServiceBase
        {
        }

        [ServiceBehavior(UseSynchronizationContext = false)]
        private class AsyncServiceWithUseSynchronizationContextFalse : AsyncServiceBase
        {
        }
    }
}
