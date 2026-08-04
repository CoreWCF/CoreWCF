// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;

namespace CoreWcfSampleService;

// A second contract on the same host, so the explorer's tree has more than one branch.
[ServiceContract]
public interface IInventoryService
{
    [OperationContract]
    bool IsInStock(string sku);

    [OperationContract]
    int GetQuantity(string sku, string warehouse);

    [OperationContract]
    string[] ListWarehouses();
}

public sealed class InventoryService : IInventoryService
{
    private static readonly string[] s_warehouses = new[] { "London", "Rotterdam", "Seattle" };

    public bool IsInStock(string sku) => !string.IsNullOrEmpty(sku) && !sku.EndsWith('0');

    public int GetQuantity(string sku, string warehouse) =>
        Math.Abs(HashCode.Combine(sku, warehouse)) % 500;

    public string[] ListWarehouses() => s_warehouses;
}
