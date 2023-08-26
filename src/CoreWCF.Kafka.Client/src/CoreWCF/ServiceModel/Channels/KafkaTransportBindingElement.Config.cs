// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Confluent.Kafka;

namespace CoreWCF.ServiceModel.Channels;

public partial class KafkaTransportBindingElement
{
    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ClientConfig.Acks"/>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public Acks? Acks
    {
        get => Config.Acks;
        set
        {
            if (value.HasValue && !AcksHelper.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Config.Acks = value;
        }
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ClientConfig.MessageMaxBytes"/>
    /// </summary>
    public int? MessageMaxBytes
    {
        get => Config.MessageMaxBytes;
        set => Config.MessageMaxBytes = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ClientConfig.ReceiveMessageMaxBytes"/>
    /// </summary>
    public int? ReceiveMessageMaxBytes
    {
        get => Config.ReceiveMessageMaxBytes;
        set => Config.ReceiveMessageMaxBytes = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ClientConfig.SocketConnectionSetupTimeoutMs"/>
    /// </summary>
    public int? SocketConnectionSetupTimeoutMs
    {
        get => Config.SocketConnectionSetupTimeoutMs;
        set => Config.SocketConnectionSetupTimeoutMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ClientConfig.ConnectionsMaxIdleMs"/>
    /// </summary>
    public int? ConnectionsMaxIdleMs
    {
        get => Config.ConnectionsMaxIdleMs;
        set => Config.ConnectionsMaxIdleMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ClientConfig.ReconnectBackoffMs"/>
    /// </summary>
    public int? ReconnectBackoffMs
    {
        get => Config.ReconnectBackoffMs;
        set => Config.ReconnectBackoffMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ClientConfig.ReconnectBackoffMaxMs"/>
    /// </summary>
    public int? ReconnectBackoffMaxMs
    {
        get => Config.ReconnectBackoffMaxMs;
        set => Config.ReconnectBackoffMaxMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ClientConfig.StatisticsIntervalMs"/>
    /// </summary>
    public int? StatisticsIntervalMs
    {
        get => Config.StatisticsIntervalMs;
        set => Config.StatisticsIntervalMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ClientConfig.ApiVersionRequest"/>
    /// </summary>
    public bool? ApiVersionRequest
    {
        get => Config.ApiVersionRequest;
        set => Config.ApiVersionRequest = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ClientConfig.ApiVersionFallbackMs"/>
    /// </summary>
    public int? ApiVersionFallbackMs
    {
        get => Config.ApiVersionFallbackMs;
        set => Config.ApiVersionFallbackMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ClientConfig.BrokerVersionFallback"/>
    /// </summary>
    public string BrokerVersionFallback
    {
        get => Config.BrokerVersionFallback;
        set => Config.BrokerVersionFallback = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ProducerConfig.CompressionType"/>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public CompressionType? CompressionType
    {
        get => Config.CompressionType;
        set
        {
            if (value.HasValue && !CompressionTypeHelper.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Config.CompressionType = value;
        }
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ProducerConfig.CompressionLevel"/>
    /// </summary>
    public int? CompressionLevel
    {
        get => Config.CompressionLevel;
        set => Config.CompressionLevel = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ProducerConfig.MessageSendMaxRetries"/>
    /// </summary>
    public int? MessageSendMaxRetries
    {
        get => Config.MessageSendMaxRetries;
        set => Config.MessageSendMaxRetries = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ProducerConfig.RetryBackoffMs"/>
    /// </summary>
    public int? RetryBackoffMs
    {
        get => Config.RetryBackoffMs;
        set => Config.RetryBackoffMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ProducerConfig.EnableIdempotence"/>
    /// </summary>
    public bool? EnableIdempotence
    {
        get => Config.EnableIdempotence;
        set => Config.EnableIdempotence = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ProducerConfig.LingerMs"/>
    /// </summary>
    public double? LingerMs
    {
        get => Config.LingerMs;
        set => Config.LingerMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ProducerConfig.RequestTimeoutMs"/>
    /// </summary>
    public int? RequestTimeoutMs
    {
        get => Config.RequestTimeoutMs;
        set => Config.RequestTimeoutMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ProducerConfig.BatchSize"/>
    /// </summary>
    public int? BatchSize
    {
        get => Config.BatchSize;
        set => Config.BatchSize = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ProducerConfig.BatchNumMessages"/>
    /// </summary>
    public int? BatchNumMessages
    {
        get => Config.BatchNumMessages;
        set => Config.BatchNumMessages = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ProducerConfig.MessageTimeoutMs"/>
    /// </summary>
    public int? MessageTimeoutMs
    {
        get => Config.MessageTimeoutMs;
        set => Config.MessageTimeoutMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ProducerConfig.TransactionalId"/>
    /// </summary>
    public string TransactionalId
    {
        get => Config.TransactionalId;
        set => Config.TransactionalId = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ProducerConfig.TransactionTimeoutMs"/>
    /// </summary>
    public int? TransactionTimeoutMs
    {
        get => Config.TransactionTimeoutMs;
        set => Config.TransactionTimeoutMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ProducerConfig.Partitioner"/>
    /// </summary>
    public Partitioner? Partitioner
    {
        get => Config.Partitioner;
        set
        {
            if (value.HasValue && !PartitionerHelper.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            Config.Partitioner = value;
        }
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ProducerConfig.QueueBufferingMaxMessages"/>
    /// </summary>
    public int? QueueBufferingMaxMessages
    {
        get => Config.QueueBufferingMaxMessages;
        set => Config.QueueBufferingMaxMessages = value;
    }

    /// <summary>
    /// <inheritdoc cref="Confluent.Kafka.ProducerConfig.QueueBufferingMaxKbytes"/>
    /// </summary>
    public int? QueueBufferingMaxKbytes
    {
        get => Config.QueueBufferingMaxKbytes;
        set => Config.QueueBufferingMaxKbytes = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.Debug"/>
    /// </summary>
    public string Debug
    {
        get => Config.Debug;
        set => Config.Debug = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.ClientId"/>
    /// </summary>
    public string ClientId
    {
        get => Config.ClientId;
        set => Config.ClientId = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.MessageCopyMaxBytes"/>
    /// </summary>
    public int? MessageCopyMaxBytes
    {
        get => Config.MessageMaxBytes;
        set => Config.MessageMaxBytes = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.MaxInFlight"/>
    /// </summary>
    public int? MaxInFlight
    {
        get => Config.MaxInFlight;
        set => Config.MaxInFlight= value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.TopicMetadataRefreshIntervalMs"/>
    /// </summary>
    public int? TopicMetadataRefreshIntervalMs
    {
        get => Config.TopicMetadataRefreshIntervalMs;
        set => Config.TopicMetadataRefreshIntervalMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.MetadataMaxAgeMs"/>
    /// </summary>
    public int? MetadataMaxAgeMs
    {
        get => Config.MetadataMaxAgeMs;
        set => Config.MetadataMaxAgeMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.TopicMetadataRefreshFastIntervalMs"/>
    /// </summary>
    public int? TopicMetadataRefreshFastIntervalMs
    {
        get => Config.TopicMetadataRefreshFastIntervalMs;
        set => Config.TopicMetadataRefreshFastIntervalMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.TopicMetadataRefreshSparse"/>
    /// </summary>
    public bool? TopicMetadataRefreshSparse
    {
        get => Config.TopicMetadataRefreshSparse;
        set => Config.TopicMetadataRefreshSparse = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.TopicMetadataPropagationMaxMs"/>
    /// </summary>
    public int? TopicMetadataPropagationMaxMs
    {
        get => Config.TopicMetadataPropagationMaxMs;
        set => Config.TopicMetadataPropagationMaxMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.TopicBlacklist"/>
    /// </summary>
    public string TopicBlacklist
    {
        get => Config.TopicBlacklist;
        set => Config.TopicBlacklist = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SocketTimeoutMs"/>
    /// </summary>
    public int? SocketTimeoutMs
    {
        get => Config.SocketTimeoutMs;
        set => Config.SocketTimeoutMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SocketSendBufferBytes"/>
    /// </summary>
    public int? SocketSendBufferBytes
    {
        get => Config.SocketSendBufferBytes;
        set => Config.SocketSendBufferBytes = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SocketReceiveBufferBytes"/>
    /// </summary>
    public int? SocketReceiveBufferBytes
    {
        get => Config.SocketReceiveBufferBytes;
        set => Config.SocketReceiveBufferBytes = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SocketKeepaliveEnable"/>
    /// </summary>
    public bool? SocketKeepaliveEnable
    {
        get => Config.SocketKeepaliveEnable;
        set => Config.SocketKeepaliveEnable = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SocketNagleDisable"/>
    /// </summary>
    public bool? SocketNagleDisable
    {
        get => Config.SocketNagleDisable;
        set => Config.SocketNagleDisable = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SocketMaxFails"/>
    /// </summary>
    public int? SocketMaxFails
    {
        get => Config.SocketMaxFails;
        set => Config.SocketMaxFails = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.BrokerAddressTtl"/>
    /// </summary>
    public int? BrokerAddressTtl
    {
        get => Config.BrokerAddressTtl;
        set => Config.BrokerAddressTtl = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.BrokerAddressFamily"/>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public BrokerAddressFamily? BrokerAddressFamily
    {
        get => Config.BrokerAddressFamily;
        set
        {
            if (value.HasValue && !BrokerAddressFamilyHelper.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Config.BrokerAddressFamily = value;
        }
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.LogQueue"/>
    /// </summary>
    public bool? LogQueue
    {
        get => Config.LogQueue;
        set => Config.LogQueue = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.LogThreadName"/>
    /// </summary>
    public bool? LogThreadName
    {
        get => Config.LogThreadName;
        set => Config.LogThreadName = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.EnableRandomSeed"/>
    /// </summary>
    public bool? EnableRandomSeed
    {
        get => Config.EnableRandomSeed;
        set => Config.EnableRandomSeed = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.LogConnectionClose"/>
    /// </summary>
    public bool? LogConnectionClose
    {
        get => Config.LogConnectionClose;
        set => Config.LogConnectionClose = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.InternalTerminationSignal"/>
    /// </summary>
    public int? InternalTerminationSignal
    {
        get => Config.InternalTerminationSignal;
        set => Config.InternalTerminationSignal = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.ApiVersionRequestTimeoutMs"/>
    /// </summary>
    public int? ApiVersionRequestTimeoutMs
    {
        get => Config.ApiVersionRequestTimeoutMs;
        set => Config.ApiVersionRequestTimeoutMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.PluginLibraryPaths"/>
    /// </summary>
    public string PluginLibraryPaths
    {
        get => Config.PluginLibraryPaths;
        set => Config.PluginLibraryPaths = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.ClientRack"/>
    /// </summary>
    public string ClientRack
    {
        get => Config.ClientRack;
        set => Config.ClientRack = value;
    }

    /// <summary>
    /// <inheritdoc cref="ProducerConfig.EnableBackgroundPoll"/>
    /// </summary>
    public bool? EnableBackgroundPoll
    {
        get => Config.EnableBackgroundPoll;
        set => Config.EnableBackgroundPoll = value;
    }

    /// <summary>
    /// <inheritdoc cref="ProducerConfig.EnableDeliveryReports"/>
    /// </summary>
    public bool? EnableDeliveryReports
    {
        get => Config.EnableDeliveryReports;
        set => Config.EnableDeliveryReports = value;
    }

    /// <summary>
    /// <inheritdoc cref="ProducerConfig.DeliveryReportFields"/>
    /// </summary>
    public string DeliveryReportFields
    {
        get => Config.DeliveryReportFields;
        set => Config.DeliveryReportFields = value;
    }

    /// <summary>
    /// <inheritdoc cref="ProducerConfig.QueueBufferingBackpressureThreshold"/>
    /// </summary>
    public int? QueueBufferingBackpressureThreshold
    {
        get => Config.QueueBufferingBackpressureThreshold;
        set => Config.QueueBufferingBackpressureThreshold = value;
    }

    /// <summary>
    /// <inheritdoc cref="ProducerConfig.StickyPartitioningLingerMs"/>
    /// </summary>
    public int? StickyPartitioningLingerMs
    {
        get => Config.StickyPartitioningLingerMs;
        set => Config.StickyPartitioningLingerMs = value;
    }
}
