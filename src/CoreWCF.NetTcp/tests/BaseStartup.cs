// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Linq;
using CoreWCF.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

public abstract class BaseStartup<TService, TContract> where TService : class
{
    public void ConfigureServices(IServiceCollection services)
    {
        //services.AddServiceModelServices();
    }

    public void Configure(IApplicationBuilder app)
    {
        //var serverAddressesFeature = app.ServerFeatures.Get<IServerAddressesFeature>();
        //Assert.NotNull(serverAddressesFeature);
        //Assert.NotEmpty(serverAddressesFeature.Addresses);
        //string address = serverAddressesFeature.Addresses.Where(url => url.StartsWith("net.tcp", StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
        //Assert.False(string.IsNullOrEmpty(address));
        //string netTcplisteningUrl = "net.tcp://localhost:11808";
        //UriBuilder uriBuilder = new UriBuilder(new Uri(netTcplisteningUrl));
        //Debug.Assert(ServiceBaseAddress.StartsWith("/"), $"{nameof(ServiceBaseAddress)} must start with /");
        //uriBuilder.Path = ServiceBaseAddress;
        //Uri baseAddress = uriBuilder.Uri;
        //IService service = app.UseService<TService>(baseAddress);
        //service.UseServiceEndpoint<TContract>(Binding, RelativeEndpointAddress);
        //app.UseMiddleware<ServiceModelMiddleware>(app);
    }

    public abstract string ServiceBaseAddress { get; }

    public abstract Binding Binding { get; }

    public abstract string RelativeEndpointAddress { get; }
}
