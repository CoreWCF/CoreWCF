﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common;
using CoreWCF.Runtime;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels;

internal sealed class KafkaTransportPump : QueueTransportPump, IDisposable
{
    private readonly ILogger<KafkaTransportPump> _logger;
    private readonly KafkaDeliverySemantics _kafkaDeliverySemantics;
    private IConsumer<byte[], byte[]> Consumer { get; set; }
    private ConsumerConfig ConsumerConfig { get; set; }
    internal IProducer<Null, byte[]> Producer { get; private set; }
    private string Topic { get; }
    internal KafkaTransportBindingElement TransportBindingElement { get; }
    private CountdownEvent _receiveContextCountdownEvent;
    private readonly object _disposeLock = new();
    private readonly Uri _baseAddress;
    private CancellationTokenSource _cts;
    private AsyncManualResetEvent _mres;
    private bool _isStarted;
    private readonly TimeSpan _closeTimeout;
    internal TopicPartitionOffsetTracker OffsetTracker { get; private set; }

    private static readonly (bool? EnableAutoCommit, bool? EnableAutoOffsetStore) s_atMostOnceConfigValues = (false, null);
    private static readonly (bool? EnableAutoCommit, bool? EnableAutoOffsetStore) s_atLeastOncePerMessageCommitConfigValues = (false, null);
    private static readonly (bool? EnableAutoCommit, bool? EnableAutoOffsetStore) s_atLeastOnceBatchCommitConfigValues = (true, false);
    private static readonly Regex s_topicNameRegex =
        new(@"^[a-zA-Z0-9\.\-_]{1,255}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    public KafkaTransportPump(KafkaTransportBindingElement transportBindingElement,
        ILogger<KafkaTransportPump> logger,
        IServiceDispatcher serviceDispatcher, KafkaDeliverySemantics kafkaDeliverySemantics)
    {
        _logger = logger;
        _kafkaDeliverySemantics = kafkaDeliverySemantics;
        Topic = serviceDispatcher.BaseAddress.PathAndQuery.TrimStart('/');
        if (string.IsNullOrEmpty(Topic) || !s_topicNameRegex.IsMatch(Topic))
        {
            throw new NotSupportedException(string.Format(SR.InvalidTopicName, Topic));
        }

        if (transportBindingElement.ErrorHandlingStrategy == KafkaErrorHandlingStrategy.DeadLetterQueue)
        {
            if (string.IsNullOrEmpty(transportBindingElement.DeadLetterQueueTopic))
            {
                throw new NotSupportedException(SR.InvalidDeadLetterQueueTopicName);
            }

            if (!s_topicNameRegex.IsMatch(transportBindingElement.DeadLetterQueueTopic))
            {
                throw new NotSupportedException(string.Format(SR.InvalidTopicName, Topic));
            }
        }
        TransportBindingElement = (KafkaTransportBindingElement)transportBindingElement.Clone();
        _baseAddress = serviceDispatcher.BaseAddress;
        _closeTimeout = serviceDispatcher.Binding.CloseTimeout;
    }

    public override Task StartPumpAsync(QueueTransportContext queueTransportContext, CancellationToken token)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _mres = new();
        _mres.Reset();
        _receiveContextCountdownEvent = new(1);

        _isStarted = true;

        ConsumerConfig = new();
        foreach (var property in TransportBindingElement.Config)
        {
            ConsumerConfig.Set(property.Key, property.Value);
        }
        ConsumerConfig.BootstrapServers = _baseAddress.Authority;
        var (enableAutoCommit, enableAutoOffsetStore) = GetCommitStrategyConfigValues(ConsumerConfig, _kafkaDeliverySemantics);
        ConsumerConfig.EnableAutoCommit = enableAutoCommit;
        ConsumerConfig.EnableAutoOffsetStore = enableAutoOffsetStore;
        Consumer = new ConsumerBuilder<byte[], byte[]>(ConsumerConfig)
            .SetKeyDeserializer(Deserializers.ByteArray)
            .SetValueDeserializer(Deserializers.ByteArray)
            .SetLogHandler(OnLog)
            .SetErrorHandler(OnError)
            .Build();
        OffsetTracker = _kafkaDeliverySemantics == KafkaDeliverySemantics.AtLeastOnce
            ? new TopicPartitionOffsetTracker(Consumer, ConsumerConfig, _logger)
            : null;

        Consumer.Subscribe(Topic);

        if (TransportBindingElement.ErrorHandlingStrategy == KafkaErrorHandlingStrategy.DeadLetterQueue)
        {
            ProducerConfig producerConfig = new();
            foreach (var property in TransportBindingElement.Config)
            {
                producerConfig.Set(property.Key, property.Value);
            }
            producerConfig.BootstrapServers = _baseAddress.Authority;
            producerConfig.Acks = Acks.All;
            Producer = new ProducerBuilder<Null, byte[]>(producerConfig)
                .SetKeySerializer(Serializers.Null)
                .SetValueSerializer(Serializers.ByteArray)
                .Build();
        }

        Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = Consumer.Consume(_cts.Token);
                    if (ConsumerConfig.EnablePartitionEof == true && consumeResult.IsPartitionEOF)
                    {
                        continue;
                    }

                    _logger.LogInformation("Received message from kafka at {topicPartitionOffset}", consumeResult.TopicPartitionOffset);
                    if (_kafkaDeliverySemantics == KafkaDeliverySemantics.AtMostOnce)
                    {
                        Consumer.Commit(consumeResult);
                    }
                    else if (_kafkaDeliverySemantics == KafkaDeliverySemantics.AtLeastOnce)
                    {
                        OffsetTracker.Received(consumeResult);
                    }

                    await OnConsumeMessage(consumeResult, queueTransportContext);

                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsumeException e)
                {
                    if (e.Error.IsFatal)
                    {
                        _logger.LogCritical("Exit consume loop {code} {error}", e.Error.Code, e.Error.Reason);
                        break;
                    }

                    _logger.LogError(e, "Consume error {code} {error}", e.Error.Code, e.Error.Reason);
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e, "Unexpected error");
                    break;
                }
            }
            _mres.Set();
        }, _cts.Token);
        return Task.CompletedTask;
    }

    public override async Task StopPumpAsync(CancellationToken token)
    {
        if (!_isStarted)
        {
            return;
        }

        _cts.Cancel();
        await _mres.WaitAsync(token);
        _cts.Dispose();

        if (ConsumerConfig.EnableAutoCommit == true)
        {
            // When EnableAutoCommit is true, offset are either manually stored locally or automatically (if EnableAutoOffsetStore is true).
            // Then a background librdkafka thread will commit them at AutoCommitIntervalMs frequency which defaults to 5000ms
            // Thus we should give AutoCommitIntervalMs time before closing the consumer
            await Task.Delay(TimeSpan.FromMilliseconds(ConsumerConfig.AutoCommitIntervalMs ?? 5000));
        }

        _receiveContextCountdownEvent.Signal();
        using CancellationTokenSource closeCts = new (_closeTimeout);
        try
        {
            _receiveContextCountdownEvent.Wait(closeCts.Token);
        }
        catch (OperationCanceledException e)
        {
            // no-op
            // Consumer.Close and Producer.Flush will allow to gracefully handle that service stops
        }

        _receiveContextCountdownEvent.Dispose();
        if (TransportBindingElement.ErrorHandlingStrategy == KafkaErrorHandlingStrategy.DeadLetterQueue)
        {
            Producer.Flush(closeCts.Token);
        }

        Consumer.Close();
    }

    private static (bool? EnableAutoCommit, bool? EnableAutoOffsetStore) GetCommitStrategyConfigValues(ConsumerConfig consumerConfig, KafkaDeliverySemantics kafkaDeliverySemantics) =>
        (kafkaDeliverySemantics, consumerConfig) switch
        {
            // KafkaBinding
            (KafkaDeliverySemantics.AtMostOnce, { EnableAutoCommit: null, EnableAutoOffsetStore: null }) => s_atMostOnceConfigValues,
            (KafkaDeliverySemantics.AtLeastOnce, { EnableAutoCommit: null, EnableAutoOffsetStore: null } ) => s_atLeastOnceBatchCommitConfigValues,
            // CustomBinding
            (KafkaDeliverySemantics.AtMostOnce, { EnableAutoCommit: false, EnableAutoOffsetStore: null }) => s_atMostOnceConfigValues,
            (KafkaDeliverySemantics.AtLeastOnce, { EnableAutoCommit: true, EnableAutoOffsetStore: false } ) => s_atLeastOnceBatchCommitConfigValues,
            (KafkaDeliverySemantics.AtLeastOnce, { EnableAutoCommit: false, EnableAutoOffsetStore: null }) => s_atLeastOncePerMessageCommitConfigValues,
            _ => throw new NotSupportedException(string.Format(SR.InvalidKafkaConfiguration, kafkaDeliverySemantics, consumerConfig.EnableAutoCommit, consumerConfig.EnableAutoOffsetStore))
        };

    private void OnLog(IConsumer<byte[], byte[]> consumer, LogMessage logMessage)
    {
        const string format = "{0}:{1}";
        switch (logMessage.Level)
        {
            case SyslogLevel.Debug:
                _logger.LogDebug(format, logMessage.Name, logMessage.Message);
                break;
            case SyslogLevel.Notice:
            case SyslogLevel.Info:
                _logger.LogInformation(format, logMessage.Name, logMessage.Message);
                break;
            case SyslogLevel.Warning:
                _logger.LogWarning(format, logMessage.Name, logMessage.Message);
                break;
            case SyslogLevel.Error:
                _logger.LogError(format, logMessage.Name, logMessage.Message);
                break;
            case SyslogLevel.Alert:
            case SyslogLevel.Critical:
            case SyslogLevel.Emergency:
                _logger.LogCritical(format, logMessage.Name, logMessage.Message);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(logMessage.Level));
        }
    }

    private void OnError(IConsumer<byte[], byte[]> consumer, Error error)
    {
        if (error.IsFatal)
        {
            _cts.Cancel();
        }
    }

    private Task OnConsumeMessage(ConsumeResult<byte[], byte[]> consumeResult,
        QueueTransportContext queueTransportContext)
    {
        var receiveContext = new KafkaReceiveContext(consumeResult, this);
        var context = new QueueMessageContext
        {
            ReceiveContext = receiveContext,
            QueueTransportContext = queueTransportContext,
            LocalAddress = new EndpointAddress(queueTransportContext.ServiceDispatcher.BaseAddress),
            QueueMessageReader = PipeReader.Create(new ReadOnlySequence<byte>(consumeResult.Message.Value)),
            Properties =
            {
                [KafkaMessageProperty.Name] = new KafkaMessageProperty(consumeResult)
            }
        };

        return queueTransportContext.QueueMessageDispatcher(context);
    }

    internal void IncrementReceiveContextCount()
    {
        _receiveContextCountdownEvent.AddCount();
    }

    internal void DecrementReceiveContextCount()
    {
        _receiveContextCountdownEvent.Signal();
    }

    public void Dispose()
    {
        lock (_disposeLock)
        {
            if (TransportBindingElement.ErrorHandlingStrategy == KafkaErrorHandlingStrategy.DeadLetterQueue)
            {
                Producer?.Dispose();
                Producer = null;
            }
            Consumer?.Dispose();
            Consumer = null;
            _cts?.Dispose();
            _mres?.Dispose();
        }
    }

    internal class TopicPartitionOffsetTracker
    {
        private readonly ConcurrentDictionary<TopicPartition, SortedList<ConsumeResult<byte[], byte[]>, bool>> _topicPartitions = new();
        private readonly IConsumer<byte[], byte[]> _consumer;
        private readonly ConsumerConfig _config;
        private readonly ILogger<KafkaTransportPump> _logger;

        public TopicPartitionOffsetTracker(IConsumer<byte[], byte[]> consumer, ConsumerConfig config, ILogger<KafkaTransportPump> logger)
        {
            _consumer = consumer;
            _config = config;
            _logger = logger;
        }

        public void Received(ConsumeResult<byte[], byte[]> consumeResult)
        {
            SortedList<ConsumeResult<byte[], byte[]>, bool> sortedList =
                _topicPartitions.GetOrAdd(consumeResult.TopicPartition, new SortedList<ConsumeResult<byte[], byte[]>, bool>(ConsumeResultComparer.Default));
            lock (sortedList)
            {
                sortedList.Add(consumeResult, false);
            }
        }

        public void MarkAsProcessed(ConsumeResult<byte[], byte[]> consumeResult)
        {
            ConsumeResult<byte[], byte[]> highestConsumeResult = null;
            SortedList<ConsumeResult<byte[], byte[]>, bool> sortedList = _topicPartitions[consumeResult.TopicPartition];
            lock (sortedList)
            {
                sortedList[consumeResult] = true;
                while (sortedList.Count > 0 && sortedList.Values[0])
                {
                    ConsumeResult<byte[], byte[]> first = sortedList.Keys[0];
                    highestConsumeResult = first;
                    sortedList.RemoveAt(0);
                }

                if (highestConsumeResult != null)
                {
                    if (_config.EnableAutoCommit == false)
                    {
                        _consumer.Commit(highestConsumeResult);
                        _logger.LogDebug("Commit {topicPartitionOffset}",
                            highestConsumeResult.TopicPartitionOffset);
                    }
                    else if (_config.EnableAutoOffsetStore == false)
                    {
                        _consumer.StoreOffset(highestConsumeResult);
                        _logger.LogDebug("StoreOffsets {topicPartitionOffset}",
                            highestConsumeResult.TopicPartitionOffset);
                    }
                }
            }
        }

        private class ConsumeResultComparer : IComparer<ConsumeResult<byte[], byte[]>>
        {
            public static ConsumeResultComparer Default { get; } = new();

            public int Compare(ConsumeResult<byte[], byte[]> x, ConsumeResult<byte[], byte[]> y)
            {
                Fx.AssertAndThrow(x.TopicPartition == y.TopicPartition, "ConsumeResult instances must be from the same TopicPartition");
                return x.Offset.Value.CompareTo(y.Offset.Value);
            }
        }
    }
}
