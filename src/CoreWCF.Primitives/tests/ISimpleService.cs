// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF;

[ServiceContract]
[System.ServiceModel.ServiceContract]
public interface ISimpleService
{
    [OperationContract]
    [System.ServiceModel.OperationContract]
    string Echo(string echo);
}

[ServiceContract]
[System.ServiceModel.ServiceContract]
public interface ISimpleAsyncService
{
    [OperationContract]
    [System.ServiceModel.OperationContract]
    Task<string> EchoAsync(string echo);
}

[ServiceContract(SessionMode = SessionMode.Required)]
[System.ServiceModel.ServiceContract(SessionMode = System.ServiceModel.SessionMode.Required)]
public interface ISimpleSessionService : ISimpleService
{
}
