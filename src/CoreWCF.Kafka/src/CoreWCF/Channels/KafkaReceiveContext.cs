// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;

namespace CoreWCF.Channels;

internal class KafkaReceiveContext : ReceiveContext
{
    private readonly ConsumeResult<Null, byte[]> _consumeResult;
    private readonly KafkaTransportPump _kafkaTransportPump;

    public KafkaReceiveContext(ConsumeResult<Null, byte[]> consumeResult, KafkaTransportPump kafkaTransportPump)
    {
        _consumeResult = consumeResult;
        _kafkaTransportPump = kafkaTransportPump;
        _kafkaTransportPump.IncrementReceiveContextCount();
    }

    protected override async Task OnAbandonAsync(CancellationToken token)
    {
        try
        {
            if (_kafkaTransportPump.TransportBindingElement.ErrorHandlingStrategy == KafkaErrorHandlingStrategy.DeadLetterQueue)
            {
                await _kafkaTransportPump.Producer.ProduceAsync(_kafkaTransportPump.TransportBindingElement.DeadLetterQueueTopic, new Message<Null, byte[]>() { Value = _consumeResult.Message.Value }, token);
            }

            if (_kafkaTransportPump.TransportBindingElement.DeliverySemantics == KafkaDeliverySemantics.AtLeastOnce)
            {
                if (_kafkaTransportPump.ConsumerConfig.EnableAutoCommit == false)
                {
                    _kafkaTransportPump.Consumer.Commit(_consumeResult);
                }
                else if (_kafkaTransportPump.ConsumerConfig.EnableAutoOffsetStore == false)
                {
                    _kafkaTransportPump.Consumer.StoreOffset(_consumeResult);
                }
            }
        }
        finally
        {
            _kafkaTransportPump.DecrementReceiveContextCount();
        }
    }

    protected override Task OnCompleteAsync(CancellationToken token)
    {
        try
        {
            if (_kafkaTransportPump.TransportBindingElement.DeliverySemantics == KafkaDeliverySemantics.AtLeastOnce)
            {
                if (_kafkaTransportPump.ConsumerConfig.EnableAutoCommit == false)
                {
                    _kafkaTransportPump.Consumer.Commit(_consumeResult);
                }
                else if (_kafkaTransportPump.ConsumerConfig.EnableAutoOffsetStore == false)
                {
                    _kafkaTransportPump.Consumer.StoreOffset(_consumeResult);
                }
            }
        }
        finally
        {
            _kafkaTransportPump.DecrementReceiveContextCount();
        }

        return Task.CompletedTask;
    }
}
