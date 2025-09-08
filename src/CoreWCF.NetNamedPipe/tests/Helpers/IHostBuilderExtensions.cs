// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Helpers
{
    internal static class IHostBuilderExtensions
    {
        public static IHostBuilder ConfigureServicesWithStartup<TStartup>(this IHostBuilder hostBuilder) where TStartup : class, new()
        {
            var startupInstance = new TStartup();
            var methods = typeof(TStartup).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            var configureServices = methods.FirstOrDefault(m => m.Name == "ConfigureServices" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(IServiceCollection)) is MethodInfo configureServicesMethod
                ? (Action<IServiceCollection>)Delegate.CreateDelegate(typeof(Action<IServiceCollection>), startupInstance, configureServicesMethod)
                : null;
            if (configureServices != null)
            {
                hostBuilder.ConfigureServices((hostContext, services) =>
                {
                    configureServices(services);
                });
            }

            return hostBuilder;
        }

        public static IHost ConfigureUsingStartup<TStartup>(this IHost host) where TStartup : class, new()
        {
            var startupInstance = new TStartup();
            var methods = typeof(TStartup).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            var configure = methods.FirstOrDefault(m => m.Name == "Configure" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(IHost)) is MethodInfo configureMethod
                ? (Action<IHost>)Delegate.CreateDelegate(typeof(Action<IHost>), startupInstance, configureMethod)
                : null;
            if (configure != null)
            {
                configure(host);
            }

            return host;
        }
    }
}
