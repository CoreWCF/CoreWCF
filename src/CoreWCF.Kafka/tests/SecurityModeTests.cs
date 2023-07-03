// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Confluent.Kafka;
using Contracts;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Kafka.Tests.Helpers;
using CoreWCF.Queue.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Kafka.Tests;

public class SecurityModeTests : IntegrationTest
{
    private const string MessageTemplate =
        @"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" xmlns:a=""http://www.w3.org/2005/08/addressing"">"
        + @"<s:Header><a:Action s:mustUnderstand=""1"">http://tempuri.org/ITestContract/Create</a:Action></s:Header>"
        + @"<s:Body><Create xmlns=""http://tempuri.org/""><name>{0}</name></Create></s:Body>"
        + @"</s:Envelope>";

    public SecurityModeTests(ITestOutputHelper output)
        : base(output )
    {

    }

    public static IEnumerable<object[]> Get_KafkaProducerTest_TestVariations()
    {
        yield return new object[]
        {
            typeof(StartupSsl),
            new ProducerConfig
            {
                BootstrapServers = "localhost:9093",
                Acks = Acks.All,
                SecurityProtocol = SecurityProtocol.Ssl,
                SslCaLocation = SslHelper.GetSslCaLocation(),
                SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https,
            }
        };
        yield return new object[]
        {
            typeof(StartupSaslPlainText),
            new ProducerConfig
            {
                BootstrapServers = "localhost:9094",
                Acks = Acks.All,
                SecurityProtocol = SecurityProtocol.SaslPlaintext,
                SaslUsername = "producer1",
                SaslPassword = "producer1-secret",
                SaslMechanism = SaslMechanism.Plain
            }
        };
        yield return new object[]
        {
            typeof(StartupSaslSsl),
            new ProducerConfig
            {
                BootstrapServers = "localhost:9095",
                Acks = Acks.All,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SaslUsername = "producer2",
                SaslPassword = "producer2-secret",
                SaslMechanism = SaslMechanism.Plain,
                SslCaLocation = SslHelper.GetSslCaLocation(),
                SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https,
            }
        };
        yield return new object[]
        {
            typeof(StartupMutualSsl),
            new ProducerConfig
            {
                BootstrapServers = "localhost:9096",
                Acks = Acks.All,
                SecurityProtocol = SecurityProtocol.Ssl,
                SslCaPem = SslHelper.GetSslCaPem(),
                SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https,
                SslCertificateLocation = SslHelper.GetProducerSslCertificateLocation(),
                SslKeyLocation = SslHelper.GetProducerSslKeyLocation(),
                SslKeyPassword = SslHelper.GetProducerSslKeyPassword()
            }
        };
    }

    [LinuxWhenCIOnlyTheory]
    [MemberData(nameof(Get_KafkaProducerTest_TestVariations))]
    public async Task KafkaProducerTest(Type startupType, ProducerConfig producerConfig)
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder(Output, startupType, ConsumerGroup, Topic).Build();
        using (host)
        {
            host.Start();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<TestService>();
            testService.CountdownEvent.Reset(1);
            using var producer = new ProducerBuilder<Null, string>(producerConfig)
                .SetKeySerializer(Serializers.Null)
                .SetValueSerializer(Serializers.Utf8)
                .Build();

            string name = Guid.NewGuid().ToString();
            string value = string.Format(MessageTemplate, name);

            var result = await producer.ProduceAsync(Topic, new Message<Null, string> { Value = value });

            Assert.True(result.Status == PersistenceStatus.Persisted);

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(10)));
            Assert.Contains(name, testService.Names);
        }
    }

    public static IEnumerable<object[]> Get_ClientSideKafkaBindingTests_TestVariations()
    {
        yield return new object[]
        {
            typeof(StartupSsl),
            new CoreWCF.ServiceModel.Channels.KafkaBinding
            {
                Security = new ServiceModel.Channels.KafkaSecurity()
                {
                    Mode = ServiceModel.Channels.KafkaSecurityMode.Transport,
                    Transport = new ServiceModel.Channels.KafkaTransportSecurity
                    {

                        CredentialType = ServiceModel.Channels.KafkaCredentialType.None,
                        CaPem = SslHelper.GetSslCaPem()
                    }
                }
            }, 9093
        };
        yield return new object[]
        {
            typeof(StartupSaslPlainText),
            new ServiceModel.Channels.KafkaBinding
            {
                Security = new ServiceModel.Channels.KafkaSecurity
                {
                    Mode = ServiceModel.Channels.KafkaSecurityMode.TransportCredentialOnly,
                    Transport = new ServiceModel.Channels.KafkaTransportSecurity()
                    {
                        CredentialType = ServiceModel.Channels.KafkaCredentialType.SaslPlain,
                        SaslUsernamePasswordCredential = new("consumer1", "consumer1-secret")
                    }
                }
            }, 9094
        };
        yield return new object[]
        {
            typeof(StartupSaslSsl),
            new ServiceModel.Channels.KafkaBinding
            {
                Security = new ServiceModel.Channels.KafkaSecurity
                {
                    Mode = ServiceModel.Channels.KafkaSecurityMode.Transport,
                    Transport = new ServiceModel.Channels.KafkaTransportSecurity()
                    {
                        CredentialType = ServiceModel.Channels.KafkaCredentialType.SaslPlain,
                        SaslUsernamePasswordCredential = new ServiceModel.Channels.SaslUsernamePasswordCredential("consumer2", "consumer2-secret"),
                        CaPem = SslHelper.GetSslCaPem()
                    }
                }
            }, 9095
        };
        yield return new object[]
        {
            typeof(StartupMutualSsl),
            new ServiceModel.Channels.KafkaBinding
            {
                Security = new ServiceModel.Channels.KafkaSecurity
                {
                    Mode = ServiceModel.Channels.KafkaSecurityMode.Transport,
                    Transport = new ServiceModel.Channels.KafkaTransportSecurity()
                    {
                        CredentialType = ServiceModel.Channels.KafkaCredentialType.SslKeyPairCertificate,
                        CaPem = SslHelper.GetSslCaPem(),
                        SslKeyPairCredential = new ServiceModel.Channels.SslKeyPairCredential()
                        {
                            SslCertificatePem = SslHelper.GetProducerSslCertificatePem(),
                            SslKeyPem = SslHelper.GetProducerSslKeyPem(),
                            SslKeyPassword = SslHelper.GetProducerSslKeyPassword()
                        }
                    }
                }
            }, 9096
        };
        var kafkaBinding = new ServiceModel.Channels.KafkaBinding();
        var customBinding = new System.ServiceModel.Channels.CustomBinding(kafkaBinding);
        var transport = customBinding.Elements.Find<ServiceModel.Channels.KafkaTransportBindingElement>();
        transport.SecurityProtocol = SecurityProtocol.Ssl;
        transport.SslCaLocation = SslHelper.GetSslCaLocation();
        transport.SslCertificateLocation = SslHelper.GetProducerSslCertificateLocation();
        transport.SslKeyLocation = SslHelper.GetProducerSslKeyLocation();
        transport.SslKeyPassword = SslHelper.GetProducerSslKeyPassword();
        yield return new object[] { typeof(StartupMutualSslCustomBinding), customBinding, 9096 };
    }

    [LinuxWhenCIOnlyTheory]
    [MemberData(nameof(Get_ClientSideKafkaBindingTests_TestVariations))]
    public void ClientSideKafkaBindingTests(Type startupType, System.ServiceModel.Channels.Binding binding, int port)
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder(Output, startupType, ConsumerGroup, Topic).Build();
        using (host)
        {
            host.Start();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<TestService>();
            testService.CountdownEvent.Reset(1);

            var factory = new System.ServiceModel.ChannelFactory<ITestContract>(binding,
                new System.ServiceModel.EndpointAddress(new Uri($"net.kafka://localhost:{port}/{Topic}")));
            ITestContract channel = factory.CreateChannel();

            string name = Guid.NewGuid().ToString();
            channel.Create(name);

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(10)));
            Assert.Contains(name, testService.Names);
        }
    }

    private class StartupSsl
    {
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
                var topicNameAccessor = app.ApplicationServices.GetService<TopicNameAccessor>();
                var consumerGroupAccessor = app.ApplicationServices.GetService<ConsumerGroupAccessor>();
                services.AddService<TestService>();
                services.AddServiceEndpoint<TestService, ITestContract>(new KafkaBinding
                {
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    DeliverySemantics = KafkaDeliverySemantics.AtMostOnce,
                    GroupId = consumerGroupAccessor.Invoke(),
                    Security = new KafkaSecurity
                    {
                        Mode = KafkaSecurityMode.Transport,
                        Transport = new KafkaTransportSecurity
                        {
                            CredentialType = KafkaCredentialType.None,
                            CaPem = SslHelper.GetSslCaPem()
                        }
                    }
                }, $"net.kafka://localhost:9093/{topicNameAccessor.Invoke()}");
            });
        }
    }

    private class StartupSaslPlainText
    {
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
                var topicNameAccessor = app.ApplicationServices.GetService<TopicNameAccessor>();
                var consumerGroupAccessor = app.ApplicationServices.GetService<ConsumerGroupAccessor>();
                services.AddService<TestService>();
                services.AddServiceEndpoint<TestService, ITestContract>(new KafkaBinding
                {
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    DeliverySemantics = KafkaDeliverySemantics.AtMostOnce,
                    GroupId = consumerGroupAccessor.Invoke(),
                    Security = new KafkaSecurity()
                    {
                        Mode = KafkaSecurityMode.TransportCredentialOnly,
                        Transport = new KafkaTransportSecurity
                        {
                            CredentialType = KafkaCredentialType.SaslPlain,
                            SaslUsernamePasswordCredential = new SaslUsernamePasswordCredential("consumer1", "consumer1-secret")
                        }
                    }
                }, $"net.kafka://localhost:9094/{topicNameAccessor.Invoke()}");
            });
        }
    }

    private class StartupSaslSsl
    {
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
                var topicNameAccessor = app.ApplicationServices.GetService<TopicNameAccessor>();
                var consumerGroupAccessor = app.ApplicationServices.GetService<ConsumerGroupAccessor>();
                services.AddService<TestService>();
                services.AddServiceEndpoint<TestService, ITestContract>(new KafkaBinding
                {
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    DeliverySemantics = KafkaDeliverySemantics.AtMostOnce,
                    GroupId = consumerGroupAccessor.Invoke(),
                    Security = new KafkaSecurity
                    {
                        Mode = KafkaSecurityMode.Transport,
                        Transport = new KafkaTransportSecurity
                        {
                            CredentialType = KafkaCredentialType.SaslPlain,
                            CaPem = SslHelper.GetSslCaPem(),
                            SaslUsernamePasswordCredential = new SaslUsernamePasswordCredential("consumer2", "consumer2-secret"),
                        }
                    }
                }, $"net.kafka://localhost:9095/{topicNameAccessor.Invoke()}");
            });
        }
    }

    private class StartupMutualSsl
    {
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
                var topicNameAccessor = app.ApplicationServices.GetService<TopicNameAccessor>();
                var consumerGroupAccessor = app.ApplicationServices.GetService<ConsumerGroupAccessor>();
                services.AddService<TestService>();
                services.AddServiceEndpoint<TestService, ITestContract>(new KafkaBinding
                {
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    DeliverySemantics = KafkaDeliverySemantics.AtMostOnce,
                    GroupId = consumerGroupAccessor.Invoke(),
                    Security = new KafkaSecurity()
                    {
                        Mode = KafkaSecurityMode.Transport,
                        Transport  = new KafkaTransportSecurity
                        {
                            CredentialType = KafkaCredentialType.SslKeyPairCertificate,
                            CaPem = SslHelper.GetSslCaPem(),
                            SslKeyPairCredential = new SslKeyPairCredential
                            {
                                SslCertificatePem = SslHelper.GetConsumerSslCertificatePem(),
                                SslKeyPem = SslHelper.GetConsumerSslKeyPem(),
                                SslKeyPassword = SslHelper.GetConsumerSslKeyPassword(),
                            }
                        }
                    }
                }, $"net.kafka://localhost:9096/{topicNameAccessor.Invoke()}");
            });
        }
    }

    private class StartupMutualSslCustomBinding
    {
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
                var topicNameAccessor = app.ApplicationServices.GetService<TopicNameAccessor>();
                var consumerGroupAccessor = app.ApplicationServices.GetService<ConsumerGroupAccessor>();
                services.AddService<TestService>();
                var kafkaBinding = new CoreWCF.Channels.KafkaBinding(KafkaDeliverySemantics.AtMostOnce);
                kafkaBinding.AutoOffsetReset = AutoOffsetReset.Earliest;
                CoreWCF.Channels.CustomBinding customBinding = new(kafkaBinding);
                var transport = customBinding.Elements.Find<CoreWCF.Channels.KafkaTransportBindingElement>();
                transport.GroupId = consumerGroupAccessor.Invoke();
                transport.SecurityProtocol = SecurityProtocol.Ssl;
                transport.SslCaLocation = SslHelper.GetSslCaLocation();
                transport.SslCertificateLocation = SslHelper.GetConsumerSslCertificateLocation();
                transport.SslKeyLocation = SslHelper.GetConsumerSslKeyLocation();
                transport.SslKeyPassword = SslHelper.GetConsumerSslKeyPassword();
                services.AddServiceEndpoint<TestService, ITestContract>(customBinding, $"net.kafka://localhost:9096/{topicNameAccessor.Invoke()}");
            });
        }
    }
}
