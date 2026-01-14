// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Xunit;

namespace CoreWCF.Kafka.Tests.Helpers;

/// <summary>
/// Manages Kafka container for integration tests.
/// This fixture sets up Kafka with SSL/SASL support matching the docker-compose configuration.
/// </summary>
public sealed class KafkaContainerFixture : IAsyncLifetime
{
    private INetwork _network;
    private IContainer _generateSecretsContainer;
    private IContainer _zookeeperContainer;
    private IContainer _kafkaContainer;

    public string BootstrapServers { get; private set; }
    public string KafkaContainerId { get; private set; }

    public async Task InitializeAsync()
    {
        // Create network
        _network = new NetworkBuilder()
            .WithName($"kafka-network-{Guid.NewGuid():N}")
            .Build();

        await _network.CreateAsync();

        // Get the path to kafka-secrets directory - it's in the source tree, not the output directory
        // Assembly is typically at: bin/Debug/CoreWCF.Kafka.Tests/{framework}/CoreWCF.Kafka.Tests.dll
        // Source is at: src/CoreWCF.Kafka/tests/kafka-secrets/
        string assemblyDir = Path.GetDirectoryName(typeof(KafkaContainerFixture).Assembly.Location);
        
        // Try to find kafka-secrets by traversing up from assembly location
        // This handles both standard build output and different configurations
        string secretsPath = null;
        string currentDir = assemblyDir;
        
        // Go up maximum 6 levels to find the repository root
        for (int i = 0; i < 6; i++)
        {
            var candidatePath = Path.Combine(currentDir, "src", "CoreWCF.Kafka", "tests", "kafka-secrets");
            if (Directory.Exists(candidatePath))
            {
                secretsPath = candidatePath;
                break;
            }
            currentDir = Path.GetFullPath(Path.Combine(currentDir, ".."));
        }
        
        if (secretsPath == null || !Directory.Exists(secretsPath))
        {
            throw new DirectoryNotFoundException($"Could not find kafka-secrets directory. Searched from assembly location: {assemblyDir}");
        }

        // Generate Kafka secrets first if files don't exist
        if (!File.Exists(Path.Combine(secretsPath, "broker.keystore.jks")))
        {
            _generateSecretsContainer = new ContainerBuilder()
                .WithImage("mcr.microsoft.com/openjdk/jdk:11-ubuntu")
                .WithName($"generate-kafka-secrets-{Guid.NewGuid():N}")
                .WithWorkingDirectory("/root/.local/share/kafka-secrets")
                .WithBindMount(secretsPath, "/root/.local/share/kafka-secrets")
                .WithEntrypoint("/bin/bash", "-c")
                .WithCommand("chmod +x /root/.local/share/kafka-secrets/generate-kafka-secrets.sh && /root/.local/share/kafka-secrets/generate-kafka-secrets.sh")
                .WithNetwork(_network)
                .Build();

            await _generateSecretsContainer.StartAsync();

            // Wait for secrets generation to complete
            var exitCode = await _generateSecretsContainer.GetExitCodeAsync();
            if (exitCode != 0)
            {
                throw new InvalidOperationException("Failed to generate Kafka secrets");
            }
        }

        // Start Zookeeper - required by Kafka
        _zookeeperContainer = new ContainerBuilder()
            .WithImage("confluentinc/cp-zookeeper:7.9.1")
            .WithName($"zookeeper-{Guid.NewGuid():N}")
            .WithNetwork(_network)
            .WithNetworkAliases("zookeeper")
            .WithEnvironment("ZOOKEEPER_CLIENT_PORT", "2181")
            .WithEnvironment("ZOOKEEPER_TICK_TIME", "2000")
            .WithEnvironment("ZOOKEEPER_ADMIN_ENABLE_SERVER", "false")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(2181))
            .Build();

        await _zookeeperContainer.StartAsync();

        // Start Kafka with configuration matching docker-compose.yml
        // Use fixed ports to match docker-compose and test expectations
        const int plainTextPort = 9092;
        const int sslPort = 9093;
        const int saslPlainTextPort = 9094;
        const int saslSslPort = 9095;
        const int mtlsPort = 9096;

        _kafkaContainer = new ContainerBuilder()
            .WithImage("confluentinc/cp-kafka:7.9.1")
            .WithName($"broker-{Guid.NewGuid():N}")
            .WithNetwork(_network)
            .WithNetworkAliases("broker")
            .WithPortBinding(plainTextPort, plainTextPort)
            .WithPortBinding(sslPort, sslPort)
            .WithPortBinding(saslPlainTextPort, saslPlainTextPort)
            .WithPortBinding(saslSslPort, saslSslPort)
            .WithPortBinding(mtlsPort, mtlsPort)
            .WithBindMount(secretsPath, "/etc/kafka/secrets")
            .WithCreateParameterModifier(p =>
            {
                if (Socket.OSSupportsIPv6)
                {
                    // Enable IPv6 in the container
                    p.HostConfig ??= new HostConfig();
                    p.HostConfig.PortBindings ??= new Dictionary<string, IList<PortBinding>>();
                    p.HostConfig.PortBindings[$"{plainTextPort}/tcp"] = new List<PortBinding>
                    {
                      new() { HostIP = "0.0.0.0", HostPort = plainTextPort.ToString() }, // IPv4
                      new() { HostIP = "::",      HostPort = plainTextPort.ToString() }  // IPv6
                    };
                    p.HostConfig.PortBindings[$"{sslPort}/tcp"] = new List<PortBinding>
                    {
                      new() { HostIP = "0.0.0.0", HostPort = sslPort.ToString() }, // IPv4
                      new() { HostIP = "::",      HostPort = sslPort.ToString() }  // IPv6
                    };
                    p.HostConfig.PortBindings[$"{saslPlainTextPort}/tcp"] = new List<PortBinding>
                    {
                      new() { HostIP = "0.0.0.0", HostPort = saslPlainTextPort.ToString() }, // IPv4
                      new() { HostIP = "::",      HostPort = saslPlainTextPort.ToString() }  // IPv6
                    };
                    p.HostConfig.PortBindings[$"{saslSslPort}/tcp"] = new List<PortBinding>
                    {
                      new() { HostIP = "0.0.0.0", HostPort = saslSslPort.ToString() }, // IPv4
                      new() { HostIP = "::",      HostPort = saslSslPort.ToString() }  // IPv6
                    };
                    p.HostConfig.PortBindings[$"{mtlsPort}/tcp"] = new List<PortBinding>
                    {
                      new() { HostIP = "0.0.0.0", HostPort = mtlsPort.ToString() }, // IPv4
                      new() { HostIP = "::",      HostPort = mtlsPort.ToString() }  // IPv6
                    };
                }
            })
            .WithEnvironment("KAFKA_BROKER_ID", "1")
            .WithEnvironment("KAFKA_ZOOKEEPER_CONNECT", "zookeeper:2181")
            .WithEnvironment("ZOOKEEPER_SASL_ENABLED", "false")
            // Configure listener security protocol map - match docker-compose exactly
            .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "INTERNAL:PLAINTEXT,HOSTPLAINTEXT:PLAINTEXT,HOSTSSL:SSL,HOSTSASLPLAINTEXT:SASL_PLAINTEXT,HOSTSASLSSL:SASL_SSL,HOSTMTLS:SSL")
            .WithEnvironment("KAFKA_ADVERTISED_LISTENERS", $"INTERNAL://broker:29092,HOSTPLAINTEXT://localhost:{plainTextPort},HOSTSSL://localhost:{sslPort},HOSTSASLPLAINTEXT://localhost:{saslPlainTextPort},HOSTSASLSSL://localhost:{saslSslPort},HOSTMTLS://localhost:{mtlsPort}")
            .WithEnvironment("KAFKA_INTER_BROKER_LISTENER_NAME", "INTERNAL")
            .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_MIN_ISR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_CONFLUENT_SUPPORT_METRICS_ENABLE", "false")
            .WithEnvironment("KAFKA_AUTO_CREATE_TOPICS_ENABLE", "true")
            // SASL configuration
            .WithEnvironment("KAFKA_SASL_ENABLED_MECHANISMS", "PLAIN")
            // SSL configuration - match docker-compose
            .WithEnvironment("KAFKA_SSL_TRUSTSTORE_FILENAME", "broker.truststore.jks")
            .WithEnvironment("KAFKA_SSL_TRUSTSTORE_CREDENTIALS", "broker.truststore.jks.cred")
            .WithEnvironment("KAFKA_SSL_KEYSTORE_FILENAME", "broker.keystore.jks")
            .WithEnvironment("KAFKA_SSL_KEYSTORE_CREDENTIALS", "broker.keystore.jks.cred")
            .WithEnvironment("KAFKA_SSL_KEY_CREDENTIALS", "broker.keystore.jks.cred")
            // SSL client authentication - match docker-compose settings
            .WithEnvironment("KAFKA_SSL_CLIENT_AUTH", "required")  // Default at broker level
            .WithEnvironment("KAFKA_LISTENER_NAME_HOSTSASLSSL_SSL_CLIENT_AUTH", "none")  // No client auth for SASL+SSL
            .WithEnvironment("KAFKA_LISTENER_NAME_HOSTSSL_SSL_CLIENT_AUTH", "none")  // No client auth for SSL only
            .WithEnvironment("KAFKA_LISTENER_NAME_HOSTMTLS_SSL_CLIENT_AUTH", "required")  // Require client auth for MTLS
            .WithEnvironment("KAFKA_OPTS", "-Djava.security.auth.login.config=/etc/kafka/secrets/broker_jaas.conf")  // JAAS configuration for SASL - match docker-compose
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(9092))
            .Build();

        await _kafkaContainer.StartAsync();

        // Set bootstrap servers to the plaintext port
        BootstrapServers = $"localhost:{plainTextPort}";
        
        // Store container ID for pause/unpause operations
        KafkaContainerId = _kafkaContainer.Id.ToString();
    }

    public async Task DisposeAsync()
    {
        if (_kafkaContainer != null)
        {
            await _kafkaContainer.DisposeAsync();
        }

        if (_zookeeperContainer != null)
        {
            await _zookeeperContainer.DisposeAsync();
        }

        if (_generateSecretsContainer != null)
        {
            await _generateSecretsContainer.DisposeAsync();
        }

        if (_network != null)
        {
            await _network.DeleteAsync();
            await _network.DisposeAsync();
        }
    }
}
