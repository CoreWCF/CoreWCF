// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using System.Runtime.CompilerServices;
using CoreWCF.Configuration;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace Helpers
{
    internal static class ServiceHelper
    {
        public static IWebHostBuilder CreateWebHostBuilder<TStartup>(ITestOutputHelper outputHelper = default, [CallerMemberName] string basePath = "", [CallerMemberName] string callerMethodName = "") where TStartup : class =>
            WebHost.CreateDefaultBuilder(Array.Empty<string>())
#if DEBUG
            .ConfigureLogging((ILoggingBuilder logging) =>
            {
                if (outputHelper != default)
                    logging.AddProvider(new XunitLoggerProvider(outputHelper, callerMethodName));
                logging.AddFilter("Default", LogLevel.Debug);
                logging.AddFilter("Microsoft", LogLevel.Debug);
                logging.SetMinimumLevel(LogLevel.Debug);
            })
#endif // DEBUG
            .UseNetNamedPipe(options =>
            {
                options.Listen(new Uri("net.pipe://localhost/" + basePath + "/"));
            })
            .UseStartup<TStartup>();

        public static IHostBuilder CreateHostBuilder<TStartup>(ITestOutputHelper outputHelper = default, [CallerMemberName] string basePath = "", [CallerMemberName] string callerMethodName = "") where TStartup : class, new() =>
            Host.CreateDefaultBuilder(Array.Empty<string>())
#if DEBUG
            .ConfigureLogging((ILoggingBuilder logging) =>
            {
                if (outputHelper != default)
                    logging.AddProvider(new XunitLoggerProvider(outputHelper, callerMethodName));
                logging.AddFilter("Default", LogLevel.Debug);
                logging.AddFilter("Microsoft", LogLevel.Debug);
                logging.SetMinimumLevel(LogLevel.Debug);
            })
#endif // DEBUG
            .UseNetNamedPipe(options =>
            {
                options.Listen(new Uri("net.pipe://localhost/" + basePath + "/"));
            })
            .ConfigureServicesWithStartup<TStartup>();

    }
}
