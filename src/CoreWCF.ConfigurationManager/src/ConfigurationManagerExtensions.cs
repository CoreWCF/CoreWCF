// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using CoreWCF.Channels;
using CoreWCF.Description;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoreWCF.Configuration
{
    public static class ConfigurationManagerExtensions
    {
        public static IServiceCollection AddConfigurationManagerFile(this IServiceCollection builder, string path, bool isOptional = false)
        {
            builder.AddSingleton<IContractResolver, DefaultContractResolver>();
            builder.AddSingleton<IConfigurationHolder, ConfigurationHolder>();
            builder.AddSingleton<IConfigureOptions<ServiceModelOptions>>(ctx => new ConfigurationManagerServiceModelOptions(ctx, path, isOptional));

            return builder;
        }
    }

    public class ServiceModelService
    {
    }
    public interface IContractResolver
    {
        Type ResolveContract(string name);

        ContractDescription ResolveDescription(Type type);
    }

    internal class DefaultContractResolver : IContractResolver
    {
        public virtual ContractDescription ResolveDescription(Type type)
        {
            throw new NotImplementedException();
           // return ContractDescription.GetContract(type);
        }

        public virtual Type ResolveContract(string name)
        {
            var items = AppDomain.CurrentDomain.GetAssemblies().OrderBy(t => t.FullName).ToList();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetType(name) is Type found)
                {
                    return found;
                }
            }

            throw new ServiceModelConfigurationException($"Could not resolve contract '{name}'");
        }
    }
}
