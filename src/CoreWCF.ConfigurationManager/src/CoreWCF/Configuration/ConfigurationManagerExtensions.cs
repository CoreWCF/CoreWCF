// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoreWCF.Configuration
{
    public static class ConfigurationManagerExtensions
    {
        public static IServiceCollection AddConfigurationManagerFile(this IServiceCollection builder, string path)
        {           
            builder.AddSingleton<IConfigurationHolder, ConfigurationHolder>();
            builder.AddSingleton<IBindingFactory, BindingFactory>();
            builder.AddSingleton<IConfigureOptions<ServiceModelOptions>>(ctx => new ConfigurationManagerServiceModelOptions(ctx, path));          

            return builder;
        }
    }
}
