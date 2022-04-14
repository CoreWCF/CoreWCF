// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace CoreWCF.ConfigurationManager.Tests
{
    public static class ServiceHelper
    {
        public static IWebHostBuilder CreateWebHostBuilder<TStartup>(ITestOutputHelper outputHelper, IPAddress ipAddress, int port)
            where TStartup : class
        {
            IWebHostBuilder result = WebHost.CreateDefaultBuilder(Array.Empty<string>());
#if DEBUG
            result = result.ConfigureLogging((ILoggingBuilder logging) =>
            {
                logging.AddProvider(new XunitLoggerProvider(outputHelper));
                logging.AddFilter("Default", LogLevel.Debug);
                logging.AddFilter("Microsoft", LogLevel.Debug);
                logging.SetMinimumLevel(LogLevel.Debug);
            });
#endif // DEBUG
            if (ipAddress == IPAddress.Any)
                result = result.UseNetTcp(port);
            else
                result = result.UseNetTcp(ipAddress, port);
            return result
              .UseStartup<TStartup>();
        }
    }
}
