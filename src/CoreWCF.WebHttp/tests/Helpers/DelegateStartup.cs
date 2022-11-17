// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Helpers;

public class GenericWebServiceStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddServiceModelWebServices();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseServiceModel(builder =>
        {
            var serviceModelConfigs = app.ApplicationServices.GetServices<IConfigureServiceModel>()
                ?? Array.Empty<IConfigureServiceModel>();

            foreach (var configure in serviceModelConfigs)
            {
                configure.Configure(builder);
            }
        });
    }
}

public interface IConfigureServiceModel
{
    void Configure(IServiceBuilder builder);
}



public static class StartupExtensions
{
    public static IWebHostBuilder WithServiceBuilder(this IWebHostBuilder webHostBuilder, params Action<IServiceBuilder>[] configures)
    {
        return webHostBuilder
            .ConfigureServices(services =>
            {
                foreach(var config in configures)
                    services.AddSingleton<IConfigureServiceModel>(new ConfigureServiceModel(config));
            });
    }

    public static IWebHostBuilder WithServiceBuilder(this IWebHostBuilder webHostBuilder, Action<IServiceBuilder> config)
    {
        return webHostBuilder.WithServiceBuilder(new[] { config });
    }

    private class ConfigureServiceModel : IConfigureServiceModel
    {
        private readonly Action<IServiceBuilder> _configure;

        public ConfigureServiceModel(Action<IServiceBuilder> configure)
        {
            _configure = configure;
        }

        public void Configure(IServiceBuilder builder) => _configure(builder);
    }
}
