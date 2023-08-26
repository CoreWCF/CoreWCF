// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
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
    internal IConsumer<Null, byte[]> Consumer { get; private set; }
    internal ConsumerConfig ConsumerConfig { get; private set; }
    internal IProducer<Null, byte[]> Producer { get; private set; }
    internal string Topic { get; }
    internal KafkaTransportBindingElement TransportBindingElement { get; }
    private CountdownEvent _receiveContextCountdownEvent;
    private object _disposeLock = new();
    private readonly Uri _baseAddress;
    private CancellationTokenSource _cts;
    private AsyncManualResetEvent _mres;
    private bool _isStarted;
    private TimeSpan _closeTimeout;

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
        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);;
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
        Consumer = new ConsumerBuilder<Null, byte[]>(ConsumerConfig)
            .SetKeyDeserializer(Deserializers.Null)
            .SetValueDeserializer(Deserializers.ByteArray)
            .SetLogHandler(OnLog)
            .SetErrorHandler(OnError)
            .Build();

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

    private void OnLog(IConsumer<Null, byte[]> consumer, LogMessage logMessage)
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

    private void OnError(IConsumer<Null, byte[]> consumer, Error error)
    {
        if (error.IsFatal)
        {
            _cts.Cancel();
        }
    }

    private Task OnConsumeMessage(ConsumeResult<Null, byte[]> consumeResult,
        QueueTransportContext queueTransportContext)
    {
        var receiveContext = new KafkaReceiveContext(consumeResult, this);
        var context = new QueueMessageContext
        {
            QueueMessageReader = PipeReader.Create(new ReadOnlySequence<byte>(consumeResult.Message.Value)),
            LocalAddress = new EndpointAddress(queueTransportContext.ServiceDispatcher.BaseAddress),
            QueueTransportContext = queueTransportContext,
            ReceiveContext = receiveContext
        };

        return queueTransportContext.QueueMessageDispatcher(context);;
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
}
