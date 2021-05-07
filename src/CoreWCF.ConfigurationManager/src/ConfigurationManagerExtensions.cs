// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoreWCF.Configuration
{
    public static class ConfigurationManagerExtensions
    {
        public static IServiceCollection AddConfigurationManagerFile(this IServiceCollection builder, string path, bool isOptional = false)
        {           
            builder.AddSingleton<IConfigurationHolder, ConfigurationHolder>();
            builder.AddSingleton<ICreateBinding, BindingFactory>();
            builder.AddSingleton<IConfigureOptions<ServiceModelOptions>>(ctx => new ConfigurationManagerServiceModelOptions(ctx, path, isOptional));

            return builder;
        }
    }
}
