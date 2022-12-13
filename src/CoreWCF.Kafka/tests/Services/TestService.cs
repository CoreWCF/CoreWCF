// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF;

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

    public CountdownEvent CountdownEvent { get; } = new(0);
    public ConcurrentBag<string> Names { get; } = new();
}
