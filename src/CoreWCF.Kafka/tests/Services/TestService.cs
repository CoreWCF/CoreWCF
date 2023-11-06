// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF;
using CoreWCF.Channels;

namespace Contracts;

[System.ServiceModel.ServiceContract]
[ServiceContract]
public interface ITestContract
{
    [System.ServiceModel.OperationContract(IsOneWay = true)]
    [OperationContract(IsOneWay = true)]
    void Create(string name);

    [System.ServiceModel.OperationContract(IsOneWay = true)]
    [OperationContract(IsOneWay = true)]
    void Throw(string name);

    [System.ServiceModel.OperationContract(IsOneWay = true, Name = "CreateAsync")]
    [OperationContract(IsOneWay = true, Name = "CreateAsync")]
    Task CreateAsync(string name);

    [System.ServiceModel.OperationContract(IsOneWay = true)]
    [OperationContract(IsOneWay = true)]
    void DoSomethingBlocking();

    [System.ServiceModel.OperationContract(IsOneWay = true)]
    [OperationContract(IsOneWay = true)]
    void DoSomething();

    [System.ServiceModel.OperationContract(IsOneWay = true)]
    [OperationContract(IsOneWay = true)]
    void StoreKafkaMessageProperty();
}

public class TestService : ITestContract
{
    public void Create(string name)
    {
        Names.Add(name);
        CountdownEvent.Signal(1);
    }

    public void Throw(string name)
    {
        throw new Exception(nameof(Throw));
    }

    public Task CreateAsync(string name)
    {
        Names.Add(name);
        CountdownEvent.Signal(1);
        return Task.CompletedTask;
    }

    public void DoSomething()
    {
        CountdownEvent.Signal(1);
    }

    public void DoSomethingBlocking()
    {
        BlockingManualResetEvent.Wait();
        CountdownEvent.Signal(1);
    }

    public void StoreKafkaMessageProperty()
    {
        KafkaMessageProperty = CoreWCF.OperationContext.Current.IncomingMessageProperties.TryGetValue(KafkaMessageProperty.Name, out var kafkaMessageProperty)
            ? kafkaMessageProperty as KafkaMessageProperty
            : null;
        CountdownEvent.Signal(1);
    }

    public CountdownEvent CountdownEvent { get; } = new(0);
    public ConcurrentBag<string> Names { get; } = new();
    public ManualResetEventSlim BlockingManualResetEvent { get; set; } = new(false);

    public KafkaMessageProperty KafkaMessageProperty { get; set; }
}
