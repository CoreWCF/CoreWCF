// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Description;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public static class ServiceHostBaseTests
    {
        [Fact]
        public static void CredentialsNotNull()
        {
            var host = new TestServiceHost();
            var creds = host.Credentials;
            Assert.NotNull(creds);
            Assert.IsType<ServiceCredentials>(creds);
            // Make sure same instance is returned each time
            Assert.Same(creds, host.Credentials);
        }

        [Fact]
        public static void CustomCredentials()
        {
            var host = new TestServiceHost();
            var testCreds = new TestServiceCredentials();
            Assert.NotNull(host.Description);
            Assert.NotNull(host.Description.Behaviors);
            host.Description.Behaviors.Add(testCreds);
            var creds = host.Credentials;
            Assert.NotNull(creds);
            Assert.IsType<TestServiceCredentials>(creds);
            // Make sure same instance is returned each time
            Assert.Same(testCreds, host.Credentials);
        }

        [Fact]
        public static async Task ServiceHostBaseOpenCloseAbort()
        {
            var host = new TestServiceHost();
            Assert.Equal(CommunicationState.Created, host.State);
            await host.OpenAsync();
            Assert.Equal(CommunicationState.Opened, host.State);
            Assert.True(host.OnOpenAsynCalled);
            await host.CloseAsync();
            Assert.Equal(CommunicationState.Closed, host.State);
            Assert.True(host.OnCloseAsynCalled);
            host.Abort();
            Assert.False(host.OnAbortCalled);
            host = new TestServiceHost();
            await host.OpenAsync();
            Assert.Equal(CommunicationState.Opened, host.State);
            host.Abort();
            Assert.True(host.OnAbortCalled);
            Assert.Equal(CommunicationState.Closed, host.State);
        }

        public class TestServiceHost : ServiceHostBase
        {
            private SimpleService _serviceInstance = new SimpleService();
            public bool OnOpenAsynCalled { get; set; }
            public bool OnCloseAsynCalled { get; set; }
            public bool OnAbortCalled { get; set; }

            public TestServiceHost()
            {
                InitializeDescription(new UriSchemeKeyedCollection());
            }

            protected override ServiceDescription CreateDescription(out IDictionary<string, ContractDescription> implementedContracts)
            {
                var description = ServiceDescription.GetService(_serviceInstance);
                var cd = ContractDescription.GetContract<SimpleService>(typeof(ISimpleService));
                implementedContracts = new Dictionary<string, ContractDescription>();
                implementedContracts[cd.ConfigurationName] = cd;
                return description;
            }

            protected override void OnAbort()
            {
                Assert.Equal(CommunicationState.Closing, State);
                OnAbortCalled = true;
            }

            protected override void OnClosing()
            {
                Assert.Equal(CommunicationState.Closing, State);
                base.OnClosing();
                Assert.Equal(CommunicationState.Closing, State);
            }

            protected override Task OnCloseAsync(CancellationToken token)
            {
                Assert.Equal(CommunicationState.Closing, State);
                OnCloseAsynCalled = true;
                return Task.CompletedTask;
            }

            protected override void OnClosed()
            {
                Assert.Equal(CommunicationState.Closing, State);
                base.OnClosed();
                Assert.Equal(CommunicationState.Closed, State);
            }

            protected override void OnOpening()
            {
                Assert.Equal(CommunicationState.Opening, State);
                base.OnOpening();
                Assert.Equal(CommunicationState.Opening, State);
            }

            protected override Task OnOpenAsync(CancellationToken token)
            {
                Assert.Equal(CommunicationState.Opening, State);
                OnOpenAsynCalled = true;
                return Task.CompletedTask;
            }

            protected override void OnOpened()
            {
                Assert.Equal(CommunicationState.Opening, State);
                base.OnOpened();
                Assert.Equal(CommunicationState.Opened, State);
            }

            protected override void ApplyConfiguration()
            {
            }
        }

        public class TestServiceCredentials : ServiceCredentials { }
    }
}
