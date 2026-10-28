// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.IdentityModel.Selectors;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;
using Services;
using Xunit;
using Client = System.ServiceModel;
using ClientChannels = System.ServiceModel.Channels;
using ServerChannels = CoreWCF.Channels;

namespace CoreWCF.NetTcp.Tests
{
    public class UserNameOverTransportDuplexTests
    {
        private const string RelativeAddress = "/username-duplex";
        private const string Password = "test-password";
        private readonly ITestOutputHelper _output;

        public UserNameOverTransportDuplexTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        public async Task AuthenticatedDuplexRequestReply(bool secureConversation, bool gzip, bool asyncClient)
        {
            using (IWebHost host = CreateHost(secureConversation, gzip))
            {
                host.Start();
                Callback callback = new Callback();
                if (asyncClient)
                {
                    Client.DuplexChannelFactory<IAsyncUserNameDuplexService> factory = CreateFactory<IAsyncUserNameDuplexService>(host, callback, secureConversation, gzip);
                    IAsyncUserNameDuplexService channel = factory.CreateChannel();
                    try
                    {
                        Assert.Equal("user-1:hello", await channel.EchoAsync("hello"));
                        Assert.Equal("user-1:again", await channel.EchoAsync("again"));
                        CloseAndAssert((Client.IClientChannel)channel, factory);
                    }
                    finally
                    {
                        ServiceHelper.CloseServiceModelObjects((Client.IClientChannel)channel, factory);
                    }
                }
                else
                {
                    Client.DuplexChannelFactory<IUserNameDuplexService> factory = CreateFactory<IUserNameDuplexService>(host, callback, secureConversation, gzip);
                    IUserNameDuplexService channel = factory.CreateChannel();
                    try
                    {
                        Assert.Equal("user-1:hello", channel.Echo("hello"));
                        Assert.Equal("user-1:again", channel.Echo("again"));
                        CloseAndAssert((Client.IClientChannel)channel, factory);
                    }
                    finally
                    {
                        ServiceHelper.CloseServiceModelObjects((Client.IClientChannel)channel, factory);
                    }
                }
                Assert.Equal(new[] { "hello", "again" }, callback.Messages);
                Assert.Equal(2, host.Services.GetRequiredService<UserNameDuplexState>().DispatchedCalls);
            }
        }

        [Fact]
        public void InvalidCredentialsAreRejectedBeforeDispatch()
        {
            using (IWebHost host = CreateHost(false, true))
            {
                host.Start();
                Callback callback = new Callback();
                Client.DuplexChannelFactory<IUserNameDuplexService> factory = CreateFactory<IUserNameDuplexService>(host, callback, false, true, password: "incorrect");
                IUserNameDuplexService channel = factory.CreateChannel();
                try
                {
                    // Check actual credential rejection, not just an early transport failure.
                    // Delivery of a security fault to the client is not asserted here.
                    Assert.ThrowsAny<Client.CommunicationException>(() => channel.Echo("hello"));
                    Assert.Empty(callback.Messages);
                    Assert.Equal(0, host.Services.GetRequiredService<UserNameDuplexState>().DispatchedCalls);
                    Assert.Equal(1, host.Services.GetRequiredService<UserNameDuplexState>().RejectedCredentials);
                }
                finally
                {
                    ((Client.IClientChannel)channel).Abort();
                    factory.Abort();
                }
            }
        }

        [Theory]
        [InlineData("service-fault")]
        [InlineData("callback-fault")]
        public void TypedFaultKeepsTheChannelUsable(string value)
        {
            using (IWebHost host = CreateHost(false, true))
            {
                host.Start();
                Callback callback = new Callback();
                Client.DuplexChannelFactory<IUserNameDuplexService> factory = CreateFactory<IUserNameDuplexService>(host, callback, false, true);
                IUserNameDuplexService channel = factory.CreateChannel();
                try
                {
                    Client.FaultException<string> fault = Assert.Throws<Client.FaultException<string>>(() => channel.Echo(value));
                    Assert.Equal(value, fault.Detail);
                    Assert.Equal("user-1:after-fault", channel.Echo("after-fault"));
                    CloseAndAssert((Client.IClientChannel)channel, factory);
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((Client.IClientChannel)channel, factory);
                }
            }
        }

        [Fact]
        public void ReconnectUsesTheNewCredentialsAndCallback()
        {
            using (IWebHost host = CreateHost(false, true))
            {
                host.Start();
                Callback first = new Callback();
                Callback second = new Callback();
                EchoAndClose(host, first, "user-1", "first");
                EchoAndClose(host, second, "user-2", "second");
                Assert.Equal(new[] { "first" }, first.Messages);
                Assert.Equal(new[] { "second" }, second.Messages);
            }
        }

        private void EchoAndClose(IWebHost host, Callback callback, string userName, string value)
        {
            Client.DuplexChannelFactory<IUserNameDuplexService> factory = CreateFactory<IUserNameDuplexService>(host, callback, false, true, userName);
            IUserNameDuplexService channel = factory.CreateChannel();
            try
            {
                Assert.Equal(userName + ":" + value, channel.Echo(value));
                CloseAndAssert((Client.IClientChannel)channel, factory);
            }
            finally
            {
                ServiceHelper.CloseServiceModelObjects((Client.IClientChannel)channel, factory);
            }
        }

        [Fact]
        public async Task HostStopsWithAnActiveAuthenticatedChannel()
        {
            using (IWebHost host = CreateHost(false, true))
            {
                host.Start();
                Client.DuplexChannelFactory<IUserNameDuplexService> factory = CreateFactory<IUserNameDuplexService>(host, new Callback(), false, true);
                IUserNameDuplexService channel = factory.CreateChannel();
                try
                {
                    Assert.Equal("user-1:before-stop", channel.Echo("before-stop"));
                    using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken))
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(15));
                        await host.StopAsync(timeout.Token);
                        Assert.False(timeout.IsCancellationRequested);
                    }
                }
                finally
                {
                    ((Client.IClientChannel)channel).Abort();
                    factory.Abort();
                }
            }
        }

        private static void CloseAndAssert(Client.ICommunicationObject channel, Client.ICommunicationObject factory)
        {
            channel.Close();
            factory.Close();
            Assert.Equal(Client.CommunicationState.Closed, channel.State);
            Assert.Equal(Client.CommunicationState.Closed, factory.State);
        }

        private IWebHost CreateHost(bool secureConversation, bool gzip)
        {
            return ServiceHelper.CreateWebHostBuilder<Startup>(_output)
                .ConfigureServices(services => services.AddSingleton(new Options { SecureConversation = secureConversation, Gzip = gzip }))
                .Build();
        }

        private static Client.DuplexChannelFactory<T> CreateFactory<T>(IWebHost host, Callback callback, bool secureConversation,
            bool gzip, string userName = "user-1", string password = Password)
        {
            ClientChannels.BindingElementCollection elements;
            if (secureConversation)
            {
                Client.NetTcpBinding binding = new Client.NetTcpBinding(Client.SecurityMode.TransportWithMessageCredential);
                binding.Security.Message.ClientCredentialType = Client.MessageCredentialType.UserName;
                elements = binding.CreateBindingElements();
            }
            else
            {
                elements = new ClientChannels.BindingElementCollection
                {
                    ClientChannels.SecurityBindingElement.CreateUserNameOverTransportBindingElement(),
                    new ClientChannels.BinaryMessageEncodingBindingElement(),
                    new ClientChannels.SslStreamSecurityBindingElement(),
                    new ClientChannels.TcpTransportBindingElement()
                };
            }
            elements.Find<ClientChannels.BinaryMessageEncodingBindingElement>().CompressionFormat = gzip
                ? ClientChannels.CompressionFormat.GZip : ClientChannels.CompressionFormat.None;
            ClientChannels.CustomBinding custom = new ClientChannels.CustomBinding(elements)
            {
                OpenTimeout = TimeSpan.FromSeconds(10),
                SendTimeout = TimeSpan.FromSeconds(10),
                CloseTimeout = TimeSpan.FromSeconds(5)
            };
            UriBuilder address = new UriBuilder(host.GetNetTcpAddressInUse() + RelativeAddress) { Host = "localhost" };
            Client.DuplexChannelFactory<T> factory = new Client.DuplexChannelFactory<T>(new Client.InstanceContext(callback), custom,
                new Client.EndpointAddress(address.Uri));
            factory.Credentials.UserName.UserName = userName;
            factory.Credentials.UserName.Password = password;
            // Follow the transport tests' certificate setup without changing machine trust.
            factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new Client.Security.X509ServiceCertificateAuthentication
            {
                CertificateValidationMode = Client.Security.X509CertificateValidationMode.None
            };
            return factory;
        }

        private sealed class Options
        {
            public bool SecureConversation { get; set; }
            public bool Gzip { get; set; }
        }

        [Client.CallbackBehavior(UseSynchronizationContext = false, ConcurrencyMode = Client.ConcurrencyMode.Multiple)]
        private sealed class Callback : IUserNameDuplexCallback
        {
            public ConcurrentQueue<string> Messages { get; } = new ConcurrentQueue<string>();

            public string Echo(string value)
            {
                Messages.Enqueue(value);
                if (value == "callback-fault")
                {
                    throw new Client.FaultException<string>(value);
                }
                return value;
            }
        }

        public sealed class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
                services.AddSingleton<UserNameDuplexState>();
                services.AddTransient<UserNameDuplexService>();
            }

            public void Configure(IApplicationBuilder app)
            {
                Options options = app.ApplicationServices.GetRequiredService<Options>();
                ServerChannels.BindingElementCollection elements;
                if (options.SecureConversation)
                {
                    NetTcpBinding binding = new NetTcpBinding(SecurityMode.TransportWithMessageCredential);
                    binding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
                    elements = binding.CreateBindingElements();
                }
                else
                {
                    elements = new ServerChannels.BindingElementCollection
                    {
                        new ServerChannels.BinaryMessageEncodingBindingElement(),
                        ServerChannels.SecurityBindingElement.CreateUserNameOverTransportBindingElement(),
                        new ServerChannels.SslStreamSecurityBindingElement(),
                        new ServerChannels.TcpTransportBindingElement()
                    };
                }
                elements.Find<ServerChannels.BinaryMessageEncodingBindingElement>().CompressionFormat = options.Gzip
                    ? ServerChannels.CompressionFormat.GZip : ServerChannels.CompressionFormat.None;
                app.UseServiceModel(builder =>
                {
                    builder.AddService<UserNameDuplexService>(options => options.DebugBehavior.IncludeExceptionDetailInFaults = true);
                    builder.AddServiceEndpoint<UserNameDuplexService, IUserNameDuplexService>(new ServerChannels.CustomBinding(elements), RelativeAddress);
                    builder.ConfigureServiceHostBase<UserNameDuplexService>(host =>
                    {
                        ServiceCredentials credentials = new ServiceCredentials();
                        credentials.ServiceCertificate.Certificate = ServiceHelper.GetServiceCertificate();
                        credentials.UserNameAuthentication.UserNamePasswordValidationMode = Security.UserNamePasswordValidationMode.Custom;
                        credentials.UserNameAuthentication.CustomUserNamePasswordValidator = new Validator(app.ApplicationServices.GetRequiredService<UserNameDuplexState>());
                        host.Description.Behaviors.Add(credentials);
                    });
                });
            }
        }

        private sealed class Validator : UserNamePasswordValidator
        {
            private readonly UserNameDuplexState _state;

            public Validator(UserNameDuplexState state)
            {
                _state = state;
            }

            public override ValueTask ValidateAsync(string userName, string password)
            {
                if ((userName != "user-1" && userName != "user-2") || password != Password)
                {
                    Interlocked.Increment(ref _state.RejectedCredentials);
                    throw new IdentityModel.Tokens.SecurityTokenException("Invalid credentials.");
                }
                return default;
            }
        }
    }
}
