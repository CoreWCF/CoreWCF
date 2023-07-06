// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels.Configuration;
using Xunit;

namespace CoreWCF.RabbitMQ.Tests;

public class QueueDeclareConfigurationFixture
{
    public ClassicQueueConfiguration ClassicQueueConfiguration;
    public QuorumQueueConfiguration QuorumQueueConfiguration;
    public DefaultQueueConfiguration DefaultQueueConfiguration;

    public QueueDeclareConfigurationFixture()
    {
        ClassicQueueConfiguration = new ClassicQueueConfiguration();
        QuorumQueueConfiguration = new QuorumQueueConfiguration();
        DefaultQueueConfiguration = new DefaultQueueConfiguration();
    }
}

public class QueueDeclareConfigurationTests : IClassFixture<QueueDeclareConfigurationFixture>
{
    private QueueDeclareConfigurationFixture _fixture;

    public QueueDeclareConfigurationTests(QueueDeclareConfigurationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void ClassicQueueConfiguration_QueueType_Is_Classic()
    {
        Assert.Equal(RabbitMqQueueType.Classic, _fixture.ClassicQueueConfiguration.QueueType);
    }

    [Fact]
    public void AsTemporaryQueue_Sets_AutoDelete_To_True()
    {
        Assert.False(_fixture.ClassicQueueConfiguration.AutoDelete);

        _fixture.ClassicQueueConfiguration.AsTemporaryQueue();
        Assert.True(_fixture.ClassicQueueConfiguration.AutoDelete);
    }

    [Fact]
    public void QuorumQueueConfiguration_QueueType_Is_Quorum()
    {
        Assert.Equal(RabbitMqQueueType.Quorum, _fixture.QuorumQueueConfiguration.QueueType);
    }

    [Fact]
    public void QuorumQueueConfiguration_Throws_If_Durable_Is_Set_To_False()
    {
        Assert.Throws<ArgumentException>(() =>
            _fixture.QuorumQueueConfiguration.Durable = false);
    }

    [Fact]
    public void QuorumQueueConfiguration_Throws_If_Exclusive_Is_Set_To_True()
    {
        Assert.Throws<ArgumentException>(() =>
            _fixture.QuorumQueueConfiguration.Exclusive = true);
    }

    [Fact]
    public void QuorumQueueConfiguration_Throws_If_GlobalQosPrefetch_Is_Set_To_True()
    {
        Assert.Throws<ArgumentException>(() =>
            _fixture.QuorumQueueConfiguration.GlobalQosPrefetch = true);
    }

}
