// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Helpers;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace CoreWCF.Kafka.Tests.Helpers;

public delegate string TopicNameAccessor();
public delegate string DeadLetterQueueTopicNameAccessor();
public delegate string ConsumerGroupAccessor();

public static class ServiceHelper
{
    private static int GetFreeTcpPort()
    {
        TcpListener listener = new (IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public static IWebHostBuilder CreateWebHostBuilder<TStartup>(ITestOutputHelper outputHelper, [CallerMemberName] string callerMethodName = "")
        where TStartup : class
    {
        return WebHost.CreateDefaultBuilder(Array.Empty<string>())
#if DEBUG
            .ConfigureLogging((ILoggingBuilder logging) =>
            {
                logging.AddProvider(new XunitLoggerProvider(outputHelper, callerMethodName));
                logging.AddFilter("Default", LogLevel.Debug);
                logging.AddFilter("Microsoft", LogLevel.Debug);
                logging.SetMinimumLevel(LogLevel.Debug);
            })
#endif // DEBUG
            .UseUrls($"http://localhost:{GetFreeTcpPort()}")
            .UseStartup<TStartup>();
    }

    public static IWebHostBuilder CreateWebHostBuilder<TStartup>(ITestOutputHelper outputHelper, string consumerGroup, string topicName, [CallerMemberName] string callerMethodName = "")
        where TStartup : class
    {
        return WebHost.CreateDefaultBuilder(Array.Empty<string>())
#if DEBUG
            .ConfigureLogging((ILoggingBuilder logging) =>
            {
                logging.AddProvider(new XunitLoggerProvider(outputHelper, callerMethodName));
                logging.AddFilter("Default", LogLevel.Debug);
                logging.AddFilter("Microsoft", LogLevel.Debug);
                logging.SetMinimumLevel(LogLevel.Debug);
            })
#endif // DEBUG
            .ConfigureServices(services =>
            {
                services.AddSingleton<TopicNameAccessor>(() => topicName);
                services.AddSingleton<DeadLetterQueueTopicNameAccessor>(() => $"{topicName}-DLQ");
                services.AddSingleton<ConsumerGroupAccessor>(() => consumerGroup);
            })
            .UseUrls($"http://localhost:{GetFreeTcpPort()}")
            .UseStartup<TStartup>();
    }

    public static IWebHostBuilder CreateWebHostBuilder(ITestOutputHelper outputHelper, Type startupType, string consumerGroup, string topicName, [CallerMemberName] string callerMethodName = "")
    {
        return WebHost.CreateDefaultBuilder(Array.Empty<string>())
#if DEBUG
            .ConfigureLogging((ILoggingBuilder logging) =>
            {
                logging.AddProvider(new XunitLoggerProvider(outputHelper, callerMethodName));
                logging.AddFilter("Default", LogLevel.Debug);
                logging.AddFilter("Microsoft", LogLevel.Debug);
                logging.SetMinimumLevel(LogLevel.Debug);
            })
#endif // DEBUG
            .ConfigureServices(services =>
            {
                services.AddSingleton<TopicNameAccessor>(() => topicName);
                services.AddSingleton<DeadLetterQueueTopicNameAccessor>(() => topicName);
                services.AddSingleton<ConsumerGroupAccessor>(() => consumerGroup);
            })
            .UseUrls($"http://localhost:{GetFreeTcpPort()}")
            .UseStartup(startupType);
    }
}
