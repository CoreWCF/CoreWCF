// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Threading;
using CoreWCF;

namespace Contracts;

[System.ServiceModel.ServiceContract]
[ServiceContract]
public interface IDispatcherTestContract
{
    [System.ServiceModel.OperationContract(IsOneWay = true)]
    [OperationContract(IsOneWay = true)]
    void Op1(string name);

    [System.ServiceModel.OperationContract(IsOneWay = true)]
    [OperationContract(IsOneWay = true)]
    void Op2(string name);
}

public class DispatcherTestService : IDispatcherTestContract
{
    public void Op1(string name)
    {
        Names1.Add(name);
        CountdownEvent1.Signal(1);
    }

    public void Op2(string name)
    {
        Names2.Add(name);
        CountdownEvent2.Signal(1);
    }

    public CountdownEvent CountdownEvent1 { get; } = new(0);
    public ConcurrentBag<string> Names1 { get; } = new();
    public CountdownEvent CountdownEvent2 { get; } = new(0);
    public ConcurrentBag<string> Names2 { get; } = new();
}
