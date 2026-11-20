// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Aspire.Explorer.Model;

/// <summary>
/// The operation the detail pane is showing, together with the service and contract it came from.
/// A single record rather than a tuple, so it can travel through <c>EventCallback&lt;T&gt;</c>.
/// </summary>
public sealed record OperationSelection(ServiceNode Service, WsdlContract Contract, WsdlOperation Operation)
{
    /// <summary>The address the operation is invoked against.</summary>
    public string EndpointAddress => Service.Descriptor.EndpointAddress;

    /// <summary>A stable identity for the tree, used to map a selected tree item back to an operation.</summary>
    public string Id => MakeId(Service.Name, Contract.Name, Operation.Name);

    public static string MakeId(string serviceName, string contractName, string operationName)
        => $"{serviceName}|{contractName}|{operationName}";
}
