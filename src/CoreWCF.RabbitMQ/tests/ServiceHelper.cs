// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace CoreWCF.RabbitMQ.Tests
{
    public static class ServiceHelper
    {
        public static IHost CreateHost<TStartup>(ITestOutputHelper outputHelper, [CallerMemberName] string callerMethodName = "")
            where TStartup : class
        {
            var builder = WebApplication.CreateBuilder();
            
#if DEBUG
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(new XunitLoggerProvider(outputHelper, callerMethodName));
            builder.Logging.AddFilter("Default", LogLevel.Debug);
            builder.Logging.AddFilter("Microsoft", LogLevel.Debug);
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif // DEBUG

            // Create an instance of the startup class to configure services
            var startup = Activator.CreateInstance<TStartup>();
            var configureServicesMethod = typeof(TStartup).GetMethod("ConfigureServices");
            configureServicesMethod?.Invoke(startup, new object[] { builder.Services });

            var app = builder.Build();

            // Call Configure method on the startup class
            var configureMethod = typeof(TStartup).GetMethod("Configure");
            if (configureMethod != null)
            {
                var parameters = configureMethod.GetParameters();
                var args = new object[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType == typeof(IApplicationBuilder))
                    {
                        args[i] = app;
                    }
                    else
                    {
                        args[i] = app.Services.GetRequiredService(parameters[i].ParameterType);
                    }
                }
                configureMethod.Invoke(startup, args);
            }

            return app;
        }
    }
}
