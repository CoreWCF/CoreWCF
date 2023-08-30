// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using Xunit;

namespace CoreWCF.RabbitMQ.Tests;

public class RabbitMqConnectionSettingsFixture
{
    public RabbitMqConnectionSettings ConnectionSettingsFromStandardUri;
    public RabbitMqConnectionSettings ConnectionSettingsFromUriWithNoExchange;
    public RabbitMqConnectionSettings ConnectionSettingsFromUriWithNoQueueName;
    public RabbitMqConnectionSettings ConnectionSettingsFromUriWithNoRoutingKey;
    public RabbitMqConnectionSettings ConnectionSettingsFromStandardUriWithTLS;

    public RabbitMqConnectionSettingsFixture()
    {
        ConnectionSettingsFromStandardUri = RabbitMqConnectionSettings.FromUri(new Uri("net.amqp://username:password@servicehostname:5671/exchange/queuename#routingKey"));
        ConnectionSettingsFromUriWithNoExchange = RabbitMqConnectionSettings.FromUri(new Uri("net.amqp://username:password@servicehostname:5671/queuename#routingKey"));
        ConnectionSettingsFromUriWithNoQueueName = RabbitMqConnectionSettings.FromUri(new Uri("net.amqp://username:password@servicehostname:5671/exchange/#routingKey"));
        ConnectionSettingsFromUriWithNoRoutingKey = RabbitMqConnectionSettings.FromUri(new Uri("net.amqp://username:password@servicehostname:5671/exchange/queuename"));
        ConnectionSettingsFromStandardUriWithTLS = RabbitMqConnectionSettings.FromUri(new Uri("net.amqps://username:password@servicehostname:5671/exchange/queuename#routingKey"));
    }
}

public class RabbitMqConnectionSettingsTests : IClassFixture<RabbitMqConnectionSettingsFixture>
{
    private readonly RabbitMqConnectionSettingsFixture _fixture;

    public RabbitMqConnectionSettingsTests(RabbitMqConnectionSettingsFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void ConnectionSettingsFromStandardUri_Populates_Host()
    {
        Assert.Equal("servicehostname", _fixture.ConnectionSettingsFromStandardUri.Host);
    }

    [Fact]
    public void ConnectionSettingsFromStandardUri_Populates_Port()
    {
        Assert.Equal(5671, _fixture.ConnectionSettingsFromStandardUri.Port);
    }

    [Fact]
    public void ConnectionSettingsFromStandardUri_Populates_Exchange()
    {
        Assert.Equal("exchange", _fixture.ConnectionSettingsFromStandardUri.Exchange);
    }

    [Fact]
    public void ConnectionSettingsFromStandardUri_Populates_QueueName()
    {
        Assert.Equal("queuename", _fixture.ConnectionSettingsFromStandardUri.QueueName);
    }

    [Fact]
    public void ConnectionSettingsFromStandardUri_Populates_RoutingKey()
    {
        Assert.Equal("routingKey", _fixture.ConnectionSettingsFromStandardUri.RoutingKey);
    }

    [Fact]
    public void ConnectionSettingsFromUriWithNoExchange_Uses_EmptyString_As_Exchange()
    {
        Assert.Equal(string.Empty, _fixture.ConnectionSettingsFromUriWithNoExchange.Exchange);
    }

    [Fact]
    public void ConnectionSettingsFromUriWithNoQueueName_Generates_A_QueueName()
    {
        Assert.StartsWith("corewcf-temp-", _fixture.ConnectionSettingsFromUriWithNoQueueName.QueueName);

        var queueNameSuffix = _fixture.ConnectionSettingsFromUriWithNoQueueName.QueueName
            .Replace("corewcf-temp-", string.Empty);
        Assert.True(Guid.TryParse(queueNameSuffix, out _));
    }

    [Fact]
    public void ConnectionSettingsFromUriWithNoRoutingKey_Uses_QueueName_As_RoutingKey()
    {
        Assert.Equal("queuename", _fixture.ConnectionSettingsFromUriWithNoRoutingKey.RoutingKey);
    }

    [Fact]
    public void ConnectionSettingsFromStandardUriWithTLS_Enables_Ssl()
    {
        Assert.True(_fixture.ConnectionSettingsFromStandardUriWithTLS.SslOption.Enabled);
    }

    [Fact]
    public void ConnectionSettingsFromStandardUriWithTLS_Sets_SslServer_To_Host()
    {
        Assert.Equal(_fixture.ConnectionSettingsFromStandardUriWithTLS.Host, _fixture.ConnectionSettingsFromStandardUriWithTLS.SslOption.ServerName);
    }
}
