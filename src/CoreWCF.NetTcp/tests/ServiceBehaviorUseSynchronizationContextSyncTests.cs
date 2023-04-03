// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using System.Threading;
using Contract;
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
    /// Verifies the effect of <see cref="ServiceBehaviorAttribute.UseSynchronizationContext"/> on synchronous operations.
    /// </summary>
    public class ServiceBehaviorUseSynchronizationContextSyncTests
    {
        public const string RelativeAddress = "/nettcp.svc/UseSynchronizationContextSync";

        private readonly ITestOutputHelper _output;

        public ServiceBehaviorUseSynchronizationContextSyncTests(ITestOutputHelper output)
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

            SyncServiceWithUseSynchronizationContextTrue serviceInstance =
                await RunAsync<SyncServiceWithUseSynchronizationContextTrue>(runCallOnThreadPool: true);

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
                int orgManagedThreadId = Environment.CurrentManagedThreadId;

                SyncServiceWithUseSynchronizationContextTrue serviceInstance =
                    await RunAsync<SyncServiceWithUseSynchronizationContextTrue>(runCallOnThreadPool: false);

                Assert.Null(serviceInstance.SynchronizationContextWhenCreated);
                Assert.Null(serviceInstance.SynchronizationContextWhenCalled);
                Assert.NotEqual(orgManagedThreadId, serviceInstance.ManagedThreadIdWhenCalled);
            });
        }

        [UIFact]
        public async Task UseSynchronizationContextFalseWithSynchronizationContextTestAsync()
        {
            SynchronizationContext orgSynchronizationContext = SynchronizationContext.Current;
            Assert.NotNull(orgSynchronizationContext); // Set by the UIFact attribute
            int orgManagedThreadId = Environment.CurrentManagedThreadId;

            SyncServiceWithUseSynchronizationContextFalse serviceInstance =
                await RunAsync<SyncServiceWithUseSynchronizationContextFalse>(runCallOnThreadPool: false);

            Assert.Null(serviceInstance.SynchronizationContextWhenCreated);
            Assert.Null(serviceInstance.SynchronizationContextWhenCalled);
            Assert.NotEqual(orgManagedThreadId, serviceInstance.ManagedThreadIdWhenCalled);
        }

        [Fact]
        public async Task UseSynchronizationContextFalseWithoutSynchronizationContextTestAsync()
        {
            await RunWithoutSynchronizationContextAsync(async () =>
            {
                Assert.Null(SynchronizationContext.Current);
                int orgManagedThreadId = Environment.CurrentManagedThreadId;

                SyncServiceWithUseSynchronizationContextFalse serviceInstance =
                    await RunAsync<SyncServiceWithUseSynchronizationContextFalse>(runCallOnThreadPool: false);

                Assert.Null(serviceInstance.SynchronizationContextWhenCreated);
                Assert.Null(serviceInstance.SynchronizationContextWhenCalled);
                Assert.NotEqual(orgManagedThreadId, serviceInstance.ManagedThreadIdWhenCalled);
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

        private async Task<TService> RunAsync<TService>(bool runCallOnThreadPool)
            where TService : class
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup<TService>>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ISyncService> factory = null;
                ISyncService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding();
                    System.ServiceModel.EndpointAddress remoteAddress = new(host.GetNetTcpAddressInUse() + RelativeAddress);
                    factory = new System.ServiceModel.ChannelFactory<ISyncService>(binding, remoteAddress);
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    if (runCallOnThreadPool)
                    {
                        await Task.Run(() => channel.DoStuff());
                    }
                    else
                    {
                        channel.DoStuff();
                    }
                    ((IChannel)channel).Close();
                    factory.Close();

                    TService serviceInstance = host.Services.GetRequiredService<TService>();
                    return serviceInstance;
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
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
                    builder.AddServiceEndpoint<TService, ISyncService>(
                        new NetTcpBinding(SecurityMode.None),
                        RelativeAddress);
                });
            }
        }

        [System.ServiceModel.ServiceContract]
        private interface ISyncService
        {
            [System.ServiceModel.OperationContract]
            void DoStuff();
        }

        private class SyncServiceBase : ISyncService
        {
            protected SyncServiceBase()
            {
                SynchronizationContextWhenCreated = SynchronizationContext.Current;
            }

            public SynchronizationContext SynchronizationContextWhenCreated { get; private set; }

            public SynchronizationContext SynchronizationContextWhenCalled { get; private set; }

            public int? ManagedThreadIdWhenCalled { get; private set; }

            public void DoStuff()
            {
                SynchronizationContextWhenCalled = SynchronizationContext.Current;
                ManagedThreadIdWhenCalled = Environment.CurrentManagedThreadId;
            }
        }

        [ServiceBehavior(UseSynchronizationContext = true)]
        private class SyncServiceWithUseSynchronizationContextTrue : SyncServiceBase
        {
        }

        [ServiceBehavior(UseSynchronizationContext = false)]
        private class SyncServiceWithUseSynchronizationContextFalse : SyncServiceBase
        {
        }
    }
}
