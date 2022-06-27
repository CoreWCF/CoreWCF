using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Channels;
using CoreWCF.Description;
using dotnet6Server;
using MyContracts;

const string BASE_HTTP_URL = "http://localhost:5000";
const string BASE_HTTPS_URL = "https://localhost:5001";

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel((context, options) =>
{
    options.AllowSynchronousIO = true;
});

// Add WSDL support
builder.Services.AddServiceModelServices().AddServiceModelMetadata();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();
var app = builder.Build();

app.Urls.Add(BASE_HTTP_URL);
app.Urls.Add(BASE_HTTPS_URL);

app.UseServiceModel(builder =>
{
    builder.AddService<EchoService>((serviceOptions) =>
    {
        // Set the base addressrs that will be used for the service and acts as the WSDL endpoint 
        serviceOptions.BaseAddresses.Add(new Uri($"{BASE_HTTP_URL}/EchoService"));
        serviceOptions.BaseAddresses.Add(new Uri($"{BASE_HTTPS_URL}/EchoService"));
    })
    .AddServiceEndpoint<EchoService, IEchoService>(new BasicHttpBinding(), "/basichttp")
    .AddServiceEndpoint<EchoService, IEchoService>(new BasicHttpBinding(BasicHttpSecurityMode.Transport), "/basichttp");
});

// Enable WSDL for http & https
var serviceMetadataBehavior = app.Services.GetRequiredService<CoreWCF.Description.ServiceMetadataBehavior>();
serviceMetadataBehavior.HttpGetEnabled = serviceMetadataBehavior.HttpsGetEnabled = true;

app.Run();

