// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Testcontainers.RabbitMq;
using Xunit;

namespace CoreWCF.RabbitMQ.Tests.Helpers;

/// <summary>
/// Manages RabbitMQ container for integration tests.
/// This fixture sets up RabbitMQ container similar to the docker-compose configuration.
/// </summary>
public sealed class RabbitMqContainerFixture : IAsyncLifetime
{
    private RabbitMqContainer _rabbitMqContainer;

    public string ConnectionString { get; private set; }
    public string Hostname { get; private set; }
    public int Port { get; private set; }
    public string Username { get; private set; } = "guest";
    public string Password { get; private set; } = "guest";

    public async Task InitializeAsync()
    {
        _rabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.11-management-alpine")
            .WithUsername(Username)
            .WithPassword(Password)
            .Build();

        await _rabbitMqContainer.StartAsync();

        ConnectionString = _rabbitMqContainer.GetConnectionString();
        Hostname = _rabbitMqContainer.Hostname;
        Port = _rabbitMqContainer.GetMappedPublicPort(5672);
    }

    public async Task DisposeAsync()
    {
        if (_rabbitMqContainer != null)
        {
            await _rabbitMqContainer.DisposeAsync();
        }
    }
}
