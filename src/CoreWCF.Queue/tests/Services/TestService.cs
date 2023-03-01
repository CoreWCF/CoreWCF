// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace CoreWCF.Queue.Tests.Services;

[ServiceContract]
public interface ITestContract
{
    [OperationContract(IsOneWay = true)]
    void Create(string name);

    [OperationContract(IsOneWay = true)]
    void Throw(string name);
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
        CountdownEvent.Signal(1);
        throw new Exception();
    }

    public CountdownEvent CountdownEvent { get; } = new(0);
    public ConcurrentBag<string> Names { get; } = new();
}
