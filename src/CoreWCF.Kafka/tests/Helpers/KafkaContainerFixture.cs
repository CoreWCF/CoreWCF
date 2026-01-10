// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.Kafka;
using Xunit;

namespace CoreWCF.Kafka.Tests.Helpers;

/// <summary>
/// Manages Kafka container for integration tests.
/// This fixture sets up Kafka with SSL/SASL support similar to the docker-compose configuration.
/// </summary>
public sealed class KafkaContainerFixture : IAsyncLifetime
{
    private INetwork _network;
    private IContainer _generateSecretsContainer;
    private KafkaContainer _kafkaContainer;

    public string BootstrapServers { get; private set; }

    public async Task InitializeAsync()
    {
        // Only run on Linux as Windows doesn't support Linux containers in CI
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
        {
            return;
        }

        // Create network
        _network = new NetworkBuilder()
            .WithName($"kafka-network-{Guid.NewGuid():N}")
            .Build();

        await _network.CreateAsync();

        // Get the path to kafka-secrets directory - it's in the source tree, not the output directory
        // We need to traverse up from the assembly location to find it
        string assemblyDir = Path.GetDirectoryName(typeof(KafkaContainerFixture).Assembly.Location);
        // Go up 4 levels: net8.0 -> CoreWCF.Kafka.Tests -> Debug -> bin -> CoreWCF (repo root)
        string secretsPath = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", "src", "CoreWCF.Kafka", "tests", "kafka-secrets"));
        
        if (!Directory.Exists(secretsPath))
        {
            throw new DirectoryNotFoundException($"Could not find kafka-secrets directory. Searched: {secretsPath}. Assembly location: {assemblyDir}");
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

        // Start Kafka with TestContainers module
        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.9.1")
            .WithNetwork(_network)
            .WithNetworkAliases("broker")
            .WithBindMount(secretsPath, "/etc/kafka/secrets")
            .WithEnvironment("KAFKA_SSL_TRUSTSTORE_FILENAME", "broker.truststore.jks")
            .WithEnvironment("KAFKA_SSL_TRUSTSTORE_CREDENTIALS", "broker.truststore.jks.cred")
            .WithEnvironment("KAFKA_SSL_KEYSTORE_FILENAME", "broker.keystore.jks")
            .WithEnvironment("KAFKA_SSL_KEYSTORE_CREDENTIALS", "broker.keystore.jks.cred")
            .WithEnvironment("KAFKA_SSL_KEY_CREDENTIALS", "broker.keystore.jks.cred")
            .WithEnvironment("KAFKA_SECURITY_PROTOCOL", "PLAINTEXT,SSL,SASL_PLAINTEXT,SASL_SSL")
            .WithEnvironment("KAFKA_SASL_ENABLED_MECHANISMS", "PLAIN")
            .WithEnvironment("KAFKA_OPTS", "-Djava.security.auth.login.config=/etc/kafka/secrets/broker_jaas.conf")
            .Build();

        await _kafkaContainer.StartAsync();

        // Set bootstrap servers - need to extract just host:port from the full URI returned by GetBootstrapAddress()
        var bootstrapAddress = _kafkaContainer.GetBootstrapAddress();
        // GetBootstrapAddress() returns something like "plaintext://127.0.0.1:32770/"
        // We need to extract just "127.0.0.1:32770"
        var uri = new Uri(bootstrapAddress);
        BootstrapServers = $"{uri.Host}:{uri.Port}";
    }

    public async Task DisposeAsync()
    {
        if (_kafkaContainer != null)
        {
            await _kafkaContainer.DisposeAsync();
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
