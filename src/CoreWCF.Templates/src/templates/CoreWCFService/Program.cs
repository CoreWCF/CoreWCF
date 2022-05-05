var builder = WebApplication.CreateBuilder();

builder.Services.AddServiceModelServices();
#if(!NoWsdl)
builder.Services.AddServiceModelMetadata();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();
#endif
builder.WebHost.ConfigureKestrel(options => 
{
    options.AllowSynchronousIO = true;
});

var app = builder.Build();

app.UseServiceModel(serviceBuilder =>
{
    serviceBuilder.AddService<Service>();
#if (NoHttps)
    serviceBuilder.AddServiceEndpoint<Service, IService>(new BasicHttpBinding(), "/Service.svc");
#else
    serviceBuilder.AddServiceEndpoint<Service, IService>(new BasicHttpBinding(BasicHttpSecurityMode.Transport), "/Service.svc");
#endif
#if (!NoWsdl)
    var serviceMetadataBehavior = app.Services.GetRequiredService<ServiceMetadataBehavior>();
#if (NoHttps)
    serviceMetadataBehavior.HttpGetEnabled = true;
#else
    serviceMetadataBehavior.HttpsGetEnabled = true;
#endif
#endif
});

app.Run();
