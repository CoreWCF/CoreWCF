// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Threading.Tasks;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CoreWCF.Aspire.Explorer.Tests;

[ServiceContract]
public interface IEchoService
{
    [OperationContract]
    string Echo(string text);
}

public sealed class EchoService : IEchoService
{
    public string Echo(string text) => $"You said: {text}";
}

/// <summary>Hosts the <see cref="EchoService"/> in-process with metadata enabled, on a dynamic port.</summary>
public sealed class EchoServiceFixture : IAsyncLifetime
{
    private WebApplication? _app;

    /// <summary>The base address the service is listening on (for example <c>http://127.0.0.1:5xxxx</c>).</summary>
    public string BaseAddress { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
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
            serviceBuilder.AddService<EchoService>();
            serviceBuilder.AddServiceEndpoint<EchoService, IEchoService>(
                new BasicHttpBinding(BasicHttpSecurityMode.None), "/echo");

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
