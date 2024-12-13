// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Telemetry;

internal sealed class ActionMetadata
{
    public ActionMetadata(string? contractName, string operationName)
    {
        this.ContractName = contractName;
        this.OperationName = operationName;
    }

    public string? ContractName { get; set; }

    public string OperationName { get; set; }
}
