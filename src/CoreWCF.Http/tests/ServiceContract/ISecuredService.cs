// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace ServiceContract;

[System.ServiceModel.ServiceContract]
public interface ISecuredService
{
    [System.ServiceModel.OperationContract]
    string Echo(string text);
}
