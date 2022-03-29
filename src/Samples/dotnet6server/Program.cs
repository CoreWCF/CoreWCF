using CoreWCF;
using CoreWCF.Configuration;
using dotnet6Server;
using MyContracts;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel((context, options) =>
{
    options.AllowSynchronousIO = true;
});

builder.Services.AddServiceModelServices();
var app = builder.Build();

app.UseServiceModel(builder =>
{
    builder.AddService<EchoService>()
    .AddServiceEndpoint<EchoService, IEchoService>(new BasicHttpBinding(), "/EchoService/basichttp");
});

app.Run();



