// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common;
using CoreWCF.Queue.Common.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CoreWCF.Kafka.Tests;

// Regression tests for the Kafka tombstone consume-pump halt: a tombstone
// (Message.Value == null, a legal log-compaction record) must NOT throw out
// of KafkaTransportPump.OnConsumeMessage. If it does, the pump's catch-all
// in the consume loop break;s and the endpoint stops processing all further
// messages until host restart (persistent DoS).
//
// The pump type and the OnConsumeMessage method are internal/private. Per security
// review, we use minimal reflection (InternalsVisibleTo is intentionally NOT added)
// to invoke OnConsumeMessage directly with a tombstone ConsumeResult. End-to-end
// pump survival is exercised by the broker-backed integration test in
// KafkaTombstoneIntegrationTests.
public class KafkaTombstoneRegressionTests
{
    private const string TestTopic = "topic-tombstone-regression";
    private static readonly Uri TestBaseAddress = new($"net.kafka://localhost:9092/{TestTopic}");

    [Fact]
    public async Task OnConsumeMessage_WithTombstone_DoesNotThrow_AndDispatchesEmptyBody()
    {
        // Arrange: build the pump via reflection (internal sealed) and inject the
        // private CountdownEvent that StartPumpAsync would normally initialise.
        Assembly kafkaAssembly = typeof(KafkaBinding).Assembly;
        Type pumpType = kafkaAssembly.GetType("CoreWCF.Channels.KafkaTransportPump", throwOnError: true);

        var bindingElement = new KafkaTransportBindingElement
        {
            DeliverySemantics = KafkaDeliverySemantics.AtMostOnce,
        };
        var serviceDispatcher = new FakeServiceDispatcher(TestBaseAddress, new KafkaBinding());
        ConstructorInfo pumpCtor = pumpType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            new[]
            {
                typeof(KafkaTransportBindingElement),
                typeof(Microsoft.Extensions.Logging.ILogger<>).MakeGenericType(pumpType),
                typeof(IServiceDispatcher),
                typeof(KafkaDeliverySemantics),
            },
            modifiers: null);
        Assert.NotNull(pumpCtor);

        Type loggerType = typeof(NullLogger<>).MakeGenericType(pumpType);
        object logger = loggerType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);

        object pump = pumpCtor.Invoke(new[]
        {
            (object)bindingElement,
            logger,
            serviceDispatcher,
            (object)KafkaDeliverySemantics.AtMostOnce,
        });

        try
        {
            FieldInfo countdownField = pumpType.GetField(
                "_receiveContextCountdownEvent",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            countdownField.SetValue(pump, new CountdownEvent(1));

            QueueMessageContext capturedContext = null;
            QueueMessageDispatcherDelegate dispatcher = ctx =>
            {
                capturedContext = ctx;
                return Task.CompletedTask;
            };

            var qtc = new QueueTransportContext { ServiceDispatcher = serviceDispatcher };
            typeof(QueueTransportContext)
                .GetProperty(nameof(QueueTransportContext.QueueMessageDispatcher))!
                .SetValue(qtc, dispatcher);

            var consumeResult = new ConsumeResult<byte[], byte[]>
            {
                Topic = TestTopic,
                Partition = new Partition(0),
                Offset = new Offset(0),
                Message = new Message<byte[], byte[]>
                {
                    Key = new byte[] { 1 },
                    Value = null, // <-- tombstone (legal Kafka log-compaction record)
                    Headers = new Headers(),
                },
            };

            MethodInfo onConsume = pumpType.GetMethod(
                "OnConsumeMessage",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            // Act
            Task dispatchTask;
            try
            {
                dispatchTask = (Task)onConsume.Invoke(pump, new object[] { consumeResult, qtc })!;
            }
            catch (TargetInvocationException tie) when (tie.InnerException is ArgumentNullException ane)
            {
                // Pre-fix behaviour: "new ReadOnlySequence<byte>((byte[])null)" throws.
                throw new Xunit.Sdk.XunitException(
                    $"Tombstone regression: OnConsumeMessage threw {ane.GetType().Name} " +
                    $"on tombstone. ParamName='{ane.ParamName}'. The pump's catch-all would " +
                    $"break out of the consume loop, halting all further message processing.");
            }

            await dispatchTask;

            // Assert: the dispatcher was called and the body is a non-null, empty pipe.
            Assert.NotNull(capturedContext);
            Assert.NotNull(capturedContext.QueueMessageReader);

            using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            ReadResult readResult = await capturedContext.QueueMessageReader.ReadAsync(readCts.Token);
            try
            {
                Assert.True(readResult.IsCompleted, "Tombstone body must produce a completed empty pipe.");
                Assert.Equal(0, readResult.Buffer.Length);
            }
            finally
            {
                capturedContext.QueueMessageReader.AdvanceTo(readResult.Buffer.End);
                await capturedContext.QueueMessageReader.CompleteAsync();
            }

            // Complete the receive context so the injected CountdownEvent decrements cleanly.
            await capturedContext.ReceiveContext.CompleteAsync(CancellationToken.None);
        }
        finally
        {
            (pump as IDisposable)?.Dispose();
        }
    }

    private sealed class FakeServiceDispatcher : IServiceDispatcher
    {
        public FakeServiceDispatcher(Uri baseAddress, Binding binding)
        {
            BaseAddress = baseAddress;
            Binding = binding;
        }

        public Uri BaseAddress { get; }
        public Binding Binding { get; }
        public ServiceHostBase Host => null;
        public IList<Type> SupportedChannelTypes => Array.Empty<Type>();

        public Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel channel)
            => throw new NotSupportedException();
    }
}
