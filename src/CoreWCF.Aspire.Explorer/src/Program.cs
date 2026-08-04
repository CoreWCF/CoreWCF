// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Aspire.Explorer.Services;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

// WebApplicationBuilder only wires up static web assets when the environment is Development, so a
// non-published run in any other environment serves no Razor class library content: every
// _content/... asset and the scoped-CSS bundle 404, and the whole UI renders unstyled. Doing it
// explicitly removes that trap. It is a no-op once published, because publishing copies the assets
// into wwwroot and leaves no manifest behind for this to read.
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddFluentUIComponents();

builder.Services.AddSingleton<ServiceCatalog>();
builder.Services.AddHttpClient<WsdlExplorerService>();
// No HttpClient: the invoker builds its own WCF channel per call.
builder.Services.AddSingleton<SoapInvoker>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapHealthChecks("/health");

app.Run();

/// <summary>Marker type so the test host (WebApplicationFactory) can reference this entry point.</summary>
public partial class Program;
