// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using Contracts;
using CoreWCF.Channels;
using CoreWCF.Channels.Configuration;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common.Configuration;
using CoreWCF.RabbitMQ.Tests.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.RabbitMQ.Tests
{
    public class IntegrationTests
    {
        private readonly ITestOutputHelper _output;

        public IntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact(Skip = "Requires RabbitMQ host with SSL")]
        public void ClassicQueueWithTls_ReceiveMessage_Success()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<ClassicQueueWithTLSStartup>(_output, nameof(ClassicQueueWithTls_ReceiveMessage_Success)).Build();
            using (host)
            {
                host.Start();
                MessageQueueHelper.SendMessageToQueue(ClassicQueueWithTLSStartup.ConnectionSettings);

                var resolver = new DependencyResolverHelper(host);
                var testService = resolver.GetService<TestService>();
                Assert.True(testService.ManualResetEvent.Wait(System.TimeSpan.FromSeconds(5)));
            }
        }

        // Automated tests use a Linux container to host RabbitMQ so this test is Linux-only
        [Fact]
        [Trait("Category", "LinuxOnly")]
        public void DefaultClassicQueueConfiguration_ReceiveMessage_Success()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<DefaultClassicQueueStartup>(_output).Build();
            using (host)
            {
                host.Start();
                MessageQueueHelper.SendMessageToQueue(DefaultClassicQueueStartup.ConnectionSettings);

                var resolver = new DependencyResolverHelper(host);
                var testService = resolver.GetService<TestService>();
                Assert.True(testService.ManualResetEvent.Wait(System.TimeSpan.FromSeconds(5)));
            }
        }

        // Automated tests use a Linux container to host RabbitMQ so this test is Linux-only
        [Fact]
        [Trait("Category", "LinuxOnly")]
        public void DefaultQuorumQueueConfiguration_ReceiveMessage_Success()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<DefaultQuorumQueueStartup>(_output, nameof(DefaultQuorumQueueConfiguration_ReceiveMessage_Success)).Build();
            using (host)
            {
                host.Start();
                MessageQueueHelper.SendMessageToQueue(DefaultQuorumQueueStartup.ConnectionSettings);

                var resolver = new DependencyResolverHelper(host);
                var testService = resolver.GetService<TestService>();
                Assert.True(testService.ManualResetEvent.Wait(System.TimeSpan.FromSeconds(5)));
            }
        }

        // Automated tests use a Linux container to host RabbitMQ so this test is Linux-only
        [Fact]
        [Trait("Category", "LinuxOnly")]
        public void DefaultQueueConfiguration_ReceiveMessage_Success()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<DefaultQueueStartup>(_output, nameof(DefaultQuorumQueueConfiguration_ReceiveMessage_Success)).Build();
            using (host)
            {
                host.Start();
                MessageQueueHelper.SendMessageToQueue(DefaultQueueStartup.ConnectionSettings);

                var resolver = new DependencyResolverHelper(host);
                var testService = resolver.GetService<TestService>();
                Assert.True(testService.ManualResetEvent.Wait(System.TimeSpan.FromSeconds(5)));
            }
        }
    }

    public class ClassicQueueWithTLSStartup
    {
        public static Uri Uri = new("net.amqps://HOST:PORT/amq.direct/QUEUE_NAME#ROUTING_KEY");
        private static readonly ICredentials s_credentials = new NetworkCredential(ConnectionFactory.DefaultUser, ConnectionFactory.DefaultPass);
        private static readonly SslOption s_sslOption = new SslOption
        {
            ServerName = Uri.Host,
            Enabled = true
        };

        public static RabbitMqConnectionSettings ConnectionSettings => RabbitMqConnectionSettings.FromUri(Uri, s_credentials, s_sslOption);

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<TestService>();
            services.AddServiceModelServices();
            services.AddQueueTransport();
        }
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseServiceModel(services =>
            {
                services.AddService<TestService>();
                services.AddServiceEndpoint<TestService, ITestContract>(
                    new RabbitMqBinding
                    {
                        SslOption = s_sslOption,
                        Credentials = s_credentials,
                        QueueConfiguration = new ClassicQueueConfiguration().AsTemporaryQueue()
                    },
                    Uri);
            });
        }
    }
    
    public class DefaultClassicQueueStartup
    {
        public static Uri Uri = new("net.amqp://localhost:5672/amq.direct/corewcf-test-default-classic-queue#corewcf-test-default-classic-key");
        private static readonly ICredentials s_credentials = new NetworkCredential(ConnectionFactory.DefaultUser, ConnectionFactory.DefaultPass);

        public static RabbitMqConnectionSettings ConnectionSettings => RabbitMqConnectionSettings.FromUri(Uri, s_credentials);

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<TestService>();
            services.AddServiceModelServices();
            services.AddQueueTransport();
        }
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseServiceModel(services =>
            {
                services.AddService<TestService>();
                services.AddServiceEndpoint<TestService, ITestContract>(
                    new RabbitMqBinding
                    {
                        Credentials = s_credentials,
                        QueueConfiguration = new QuorumQueueConfiguration()
                    },
                    Uri);
            });
        }
    }
    
    public class DefaultQuorumQueueStartup
    {
        public static Uri Uri = new("net.amqp://localhost:5672/amq.direct/corewcf-test-default-quorum-queue#corewcf-test-default-quorum-key");
        private static readonly ICredentials s_credentials = new NetworkCredential(ConnectionFactory.DefaultUser, ConnectionFactory.DefaultPass);

        public static RabbitMqConnectionSettings ConnectionSettings =>
            RabbitMqConnectionSettings.FromUri(Uri, s_credentials);

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<TestService>();
            services.AddServiceModelServices();
            services.AddQueueTransport();
        }
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseServiceModel(services =>
            {
                services.AddService<TestService>();
                services.AddServiceEndpoint<TestService, ITestContract>(
                    new RabbitMqBinding
                    {
                        Credentials = s_credentials,
                        QueueConfiguration = new QuorumQueueConfiguration()
                    },
                    Uri);
            });
        }
        }

    public class DefaultQueueStartup
    {
        public static Uri Uri = new("net.amqp://localhost:5672/amq.direct/corewcf-test-default-queue#corewcf-test-default-key");
        private static readonly ICredentials s_credentials = new NetworkCredential(ConnectionFactory.DefaultUser, ConnectionFactory.DefaultPass);

        public static RabbitMqConnectionSettings ConnectionSettings => RabbitMqConnectionSettings.FromUri(Uri, s_credentials);

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<TestService>();
            services.AddServiceModelServices();
            services.AddQueueTransport();
        }
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseServiceModel(services =>
            {
                services.AddService<TestService>();
                services.AddServiceEndpoint<TestService, ITestContract>(
                    new RabbitMqBinding
                    {
                        Credentials = s_credentials,
                        QueueConfiguration = new QuorumQueueConfiguration()
                    },
                    Uri);
            });
        }
    }
}
