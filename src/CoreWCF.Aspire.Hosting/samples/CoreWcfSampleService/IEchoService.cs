// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;
using CoreWCF;

namespace CoreWcfSampleService;

[ServiceContract]
public interface IEchoService
{
    [OperationContract]
    string Echo(string text);

    // Several simple parameters: exercises the explorer's formatted parameter grid.
    [OperationContract]
    int Add(int x, int y);

    // Complex return type: the response is a nested element rather than a flat value.
    [OperationContract]
    OrderDetails GetOrderDetails(int orderId);

    // Complex *parameter*: the explorer cannot build a formatted request for this one and
    // falls back to the raw XML editor.
    [OperationContract]
    string PlaceOrder(OrderRequest request);

    // Always faults: exercises the SOAP fault rendering path.
    [OperationContract]
    string Fail(string reason);
}

[DataContract]
public sealed class OrderDetails
{
    [DataMember]
    public int OrderId { get; set; }

    [DataMember]
    public string? Customer { get; set; }

    [DataMember]
    public decimal Total { get; set; }

    [DataMember]
    public DateTime PlacedOn { get; set; }
}

[DataContract]
public sealed class OrderRequest
{
    [DataMember]
    public string? Sku { get; set; }

    [DataMember]
    public int Quantity { get; set; }

    [DataMember]
    public string? Customer { get; set; }
}

public class EchoService : IEchoService
{
    public string Echo(string text) => $"You said: {text}";

    public int Add(int x, int y) => x + y;

    public OrderDetails GetOrderDetails(int orderId) => new()
    {
        OrderId = orderId,
        Customer = "Contoso Ltd.",
        Total = 249.95m,
        PlacedOn = new DateTime(2026, 1, 15, 9, 30, 0, DateTimeKind.Utc),
    };

    public string PlaceOrder(OrderRequest request) =>
        $"Accepted {request.Quantity} x {request.Sku} for {request.Customer}.";

    public string Fail(string reason) =>
        throw new FaultException($"The operation failed on purpose: {reason}");
}

/// <summary>
/// The same contract, hosted again over SOAP 1.2.
/// <para>
/// A distinct service type rather than a second endpoint on <see cref="EchoService"/>, because
/// metadata is published per service: two endpoints on one service share a single WSDL document,
/// which lists both bindings but is only reachable at the first endpoint's address. Giving the SOAP
/// 1.2 endpoint its own service gives it its own <c>?singleWsdl</c>, which is what a client - the
/// explorer included - needs in order to discover it.
/// </para>
/// </summary>
public sealed class Soap12EchoService : EchoService
{
}
