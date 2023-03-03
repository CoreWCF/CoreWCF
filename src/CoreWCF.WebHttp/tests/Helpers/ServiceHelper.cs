// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Helpers
{
    public static class ServiceHelper
    {
        public static IWebHostBuilder CreateWebHostBuilder<TStartup>(ITestOutputHelper outputHelper = default, [CallerMemberName] string callerMethodName = "") where TStartup : class =>
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
            .UseKestrel(options =>
            {
                options.Listen(IPAddress.Loopback, TcpPortHelper.GetFreeTcpPort(), listenOptions =>
                {
                    if (Debugger.IsAttached)
                    {
                        listenOptions.UseConnectionLogging();
                    }
                });
            })
            .UseStartup<TStartup>();

        public static IWebHostBuilder CreateWebHostBuilder(ITestOutputHelper outputHelper, Type startupType, [CallerMemberName] string callerMethodName = "") =>
            WebHost.CreateDefaultBuilder(Array.Empty<string>())
#if DEBUG
                .ConfigureLogging((ILoggingBuilder logging) =>
                {
                    logging.AddProvider(new XunitLoggerProvider(outputHelper, callerMethodName));
                    logging.AddFilter("Default", LogLevel.Debug);
                    logging.AddFilter("Microsoft", LogLevel.Debug);
                    logging.SetMinimumLevel(LogLevel.Debug);
                })
#endif // DEBUG
                .UseKestrel(options =>
                {
                    options.Listen(IPAddress.Loopback, TcpPortHelper.GetFreeTcpPort(), listenOptions =>
                    {
                        if (Debugger.IsAttached)
                        {
                            listenOptions.UseConnectionLogging();
                        }
                    });
                })
                .UseStartup(startupType);

        public static IWebHostBuilder CreateWebHostBuilderWithSsl<TStartup>(ITestOutputHelper outputHelper = default, [CallerMemberName] string callerMethodName = "") where TStartup : class =>
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
            .UseKestrel(options =>
            {
                options.Listen(IPAddress.Loopback, TcpPortHelper.GetFreeTcpPort(), listenOptions =>
                {
                    if (Debugger.IsAttached)
                    {
                        listenOptions.UseConnectionLogging();
                    }
                });
                options.Listen(IPAddress.Loopback, TcpPortHelper.GetFreeTcpPort(), listenOptions =>
                {
                    listenOptions.UseHttps();

                    if (Debugger.IsAttached)
                    {
                        listenOptions.UseConnectionLogging();
                    }
                });
            })
            .UseStartup<TStartup>();
    }
}
