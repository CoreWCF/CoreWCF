// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Confluent.Kafka;

namespace CoreWCF.Channels;

public partial class KafkaTransportBindingElement
{
    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.EnableAutoOffsetStore"/>
    /// </summary>
    public bool? EnableAutoOffsetStore
    {
        get => Config.EnableAutoOffsetStore;
        set => Config.EnableAutoOffsetStore = value;
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.EnableAutoCommit"/>
    /// </summary>
    public bool? EnableAutoCommit
    {
        get => Config.EnableAutoCommit;
        set =>Config.EnableAutoCommit = value;
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.AutoOffsetReset"/>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public AutoOffsetReset? AutoOffsetReset
    {
        get => Config.AutoOffsetReset;
        set
        {
            if (value.HasValue && !AutoOffsetResetHelper.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Config.AutoOffsetReset = value;
        }
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.IsolationLevel"/>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public IsolationLevel? IsolationLevel
    {
        get => Config.IsolationLevel;
        set
        {
            if (value.HasValue && !IsolationLevelHelper.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Config.IsolationLevel = value;
        }
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.MessageMaxBytes"/>
    /// </summary>
    public int? MessageMaxBytes
    {
        get => Config.MessageMaxBytes;
        set => Config.MessageMaxBytes = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.ReceiveMessageMaxBytes"/>
    /// </summary>
    public int? ReceiveMessageMaxBytes
    {
        get => Config.ReceiveMessageMaxBytes;
        set => Config.ReceiveMessageMaxBytes = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SocketConnectionSetupTimeoutMs"/>
    /// </summary>
    public int? SocketConnectionSetupTimeoutMs
    {
        get => Config.SocketConnectionSetupTimeoutMs;
        set => Config.SocketConnectionSetupTimeoutMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.ConnectionsMaxIdleMs"/>
    /// </summary>
    public int? ConnectionsMaxIdleMs
    {
        get => Config.ConnectionsMaxIdleMs;
        set => Config.ConnectionsMaxIdleMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.ReconnectBackoffMs"/>
    /// </summary>
    public int? ReconnectBackoffMs
    {
        get => Config.ReconnectBackoffMs;
        set => Config.ReconnectBackoffMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.ReconnectBackoffMaxMs"/>
    /// </summary>
    public int? ReconnectBackoffMaxMs
    {
        get => Config.ReconnectBackoffMaxMs;
        set => Config.ReconnectBackoffMaxMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.StatisticsIntervalMs"/>
    /// </summary>
    public int? StatisticsIntervalMs
    {
        get => Config.StatisticsIntervalMs;
        set => Config.StatisticsIntervalMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.ApiVersionRequest"/>
    /// </summary>
    public bool? ApiVersionRequest
    {
        get => Config.ApiVersionRequest;
        set => Config.ApiVersionRequest = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.ApiVersionFallbackMs"/>
    /// </summary>
    public int? ApiVersionFallbackMs
    {
        get => Config.ApiVersionFallbackMs;
        set => Config.ApiVersionFallbackMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.BrokerVersionFallback"/>
    /// </summary>
    public string BrokerVersionFallback
    {
        get => Config.BrokerVersionFallback;
        set => Config.BrokerVersionFallback = value;
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.GroupId"/>
    /// </summary>
    public string GroupId
    {
        get => Config.GroupId;
        set => Config.GroupId = value;
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.GroupInstanceId"/>
    /// </summary>
    public string GroupInstanceId
    {
        get => Config.GroupInstanceId ;
        set => Config.GroupInstanceId = value;
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.PartitionAssignmentStrategy"/>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public PartitionAssignmentStrategy? PartitionAssignmentStrategy
    {
        get => Config.PartitionAssignmentStrategy;
        set
        {
            if (value.HasValue && !PartitionAssignmentStrategyHelper.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Config.PartitionAssignmentStrategy = value;
        }
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.SessionTimeoutMs"/>
    /// </summary>
    public int? SessionTimeoutMs
    {
        get => Config.SessionTimeoutMs;
        set => Config.SessionTimeoutMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.MaxPollIntervalMs"/>
    /// </summary>
    public int? MaxPollIntervalMs
    {
        get => Config.MaxPollIntervalMs;
        set => Config.MaxPollIntervalMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.AutoCommitIntervalMs"/>
    /// </summary>
    public int? AutoCommitIntervalMs
    {
        get => Config.AutoCommitIntervalMs;
        set => Config.AutoCommitIntervalMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.QueuedMinMessages"/>
    /// </summary>
    public int? QueuedMinMessages
    {
        get => Config.QueuedMinMessages;
        set => Config.QueuedMinMessages = value;
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.QueuedMaxMessagesKbytes"/>
    /// </summary>
    public int? QueuedMaxMessagesKbytes
    {
        get => Config.QueuedMaxMessagesKbytes;
        set => Config.QueuedMaxMessagesKbytes = value;
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.MaxPartitionFetchBytes"/>
    /// </summary>
    public int? MaxPartitionFetchBytes
    {
        get => Config.MaxPartitionFetchBytes;
        set => Config.MaxPartitionFetchBytes = value;
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.FetchMaxBytes"/>
    /// </summary>
    public int? FetchMaxBytes
    {
        get => Config.FetchMaxBytes;
        set => Config.FetchMaxBytes = value;
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.FetchErrorBackoffMs"/>
    /// </summary>
    public int? FetchErrorBackoffMs
    {
        get => Config.FetchErrorBackoffMs;
        set => Config.FetchErrorBackoffMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.CheckCrcs"/>
    /// </summary>
    public bool? CheckCrcs
    {
        get => Config.CheckCrcs;
        set => Config.CheckCrcs = value;
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
    /// <inheritdoc cref="ConsumerConfig.HeartbeatIntervalMs"/>
    /// </summary>
    public int? HeartbeatIntervalMs
    {
        get => Config.HeartbeatIntervalMs;
        set => Config.HeartbeatIntervalMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.GroupProtocolType"/>
    /// </summary>
    public string GroupProtocolType
    {
        get => Config.GroupProtocolType;
        set => Config.GroupProtocolType = value;
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.CoordinatorQueryIntervalMs"/>
    /// </summary>
    public int? CoordinatorQueryIntervalMs
    {
        get => Config.CoordinatorQueryIntervalMs;
        set => Config.CoordinatorQueryIntervalMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.FetchWaitMaxMs"/>
    /// </summary>
    public int? FetchWaitMaxMs
    {
        get => Config.FetchWaitMaxMs;
        set => Config.FetchWaitMaxMs = value;
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.FetchMinBytes"/>
    /// </summary>
    public int? FetchMinBytes
    {
        get => Config.FetchMinBytes;
        set => Config.FetchMinBytes = value;
    }

    /// <summary>
    /// <inheritdoc cref="ConsumerConfig.EnablePartitionEof"/>
    /// </summary>
    public bool? EnablePartitionEof
    {
        get => Config.EnablePartitionEof;
        set => Config.EnablePartitionEof = value;
    }
}
