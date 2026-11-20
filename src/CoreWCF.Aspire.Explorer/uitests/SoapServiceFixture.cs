// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Aspire.Explorer.UITests;

/// <summary>An order, used to give one operation a parameter the formatted grid cannot express.</summary>
[DataContract]
public sealed class OrderRequest
{
    [DataMember]
    public string Sku { get; set; } = string.Empty;

    [DataMember]
    public int Quantity { get; set; }
}

[ServiceContract]
public interface ICalculatorService
{
    [OperationContract]
    int Add(int x, int y);

    [OperationContract]
    string Describe(string label);

    /// <summary>Always faults, so the response view's fault path has something to render.</summary>
    [OperationContract]
    string Fail(string reason);

    /// <summary>Takes a data contract, so the formatted request tab has to disable itself.</summary>
    [OperationContract]
    string PlaceOrder(OrderRequest request);
}

[ServiceContract]
public interface IInventoryService
{
    [OperationContract]
    bool IsInStock(string sku);

    [OperationContract]
    int GetQuantity(string sku);
}

public class CalculatorService : ICalculatorService
{
    public int Add(int x, int y) => x + y;

    public string Describe(string label) => $"Described: {label}";

    public string Fail(string reason) => throw new FaultException($"Failed on purpose: {reason}");

    public string PlaceOrder(OrderRequest request) => $"Ordered {request.Quantity} x {request.Sku}";
}

/// <summary>
/// The same contract hosted again over SOAP 1.2. A distinct service type, because metadata is
/// published per service: a second endpoint on <see cref="CalculatorService"/> would share one WSDL
/// document reachable only at the first endpoint's address, so the explorer could never discover it.
/// </summary>
public sealed class Soap12CalculatorService : CalculatorService
{
}

public sealed class InventoryService : IInventoryService
{
    public bool IsInStock(string sku) => !string.IsNullOrEmpty(sku);

    public int GetQuantity(string sku) => sku.Length * 10;
}

/// <summary>
/// Hosts two CoreWCF contracts in-process on a dynamic port, with metadata enabled. The explorer is
/// pointed at both endpoints, which gives the tree two services and six operations to work with.
/// </summary>
public sealed class SoapServiceFixture : IAsyncDisposable
{
    private WebApplication? _app;

    /// <summary>The base address the service is listening on, for example <c>http://127.0.0.1:5xxxx</c>.</summary>
    public string BaseAddress { get; private set; } = string.Empty;

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        builder.Services.AddServiceModelServices();
        builder.Services.AddServiceModelMetadata();
        builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

        _app = builder.Build();

        ((IApplicationBuilder)_app).UseServiceModel(serviceBuilder =>
        {
            serviceBuilder.AddService<CalculatorService>();
            serviceBuilder.AddServiceEndpoint<CalculatorService, ICalculatorService>(
                new BasicHttpBinding(BasicHttpSecurityMode.None), "/calc");

            // BasicHttpBinding is SOAP 1.1 only, and WSHttpBinding would require WS-Addressing,
            // which the explorer deliberately does not send. Compose 1.2 without addressing.
            serviceBuilder.AddService<Soap12CalculatorService>();
            serviceBuilder.AddServiceEndpoint<Soap12CalculatorService, ICalculatorService>(
                new CustomBinding(
                    new TextMessageEncodingBindingElement(
                        MessageVersion.CreateVersion(EnvelopeVersion.Soap12, AddressingVersion.None),
                        Encoding.UTF8),
                    new HttpTransportBindingElement()),
                "/calc12");

            serviceBuilder.AddService<InventoryService>();
            serviceBuilder.AddServiceEndpoint<InventoryService, IInventoryService>(
                new BasicHttpBinding(BasicHttpSecurityMode.None), "/inventory");

            var metadata = _app.Services.GetRequiredService<ServiceMetadataBehavior>();
            metadata.HttpGetEnabled = true;
        });

        await _app.StartAsync();
        BaseAddress = _app.Urls.First();
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
