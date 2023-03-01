// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common.Configuration;
using CoreWCF.Queue.Tests.Helpers;
using CoreWCF.Queue.Tests.InMemoryQueue;
using CoreWCF.Queue.Tests.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Queue.Tests;

public class InMemoryQueueIntegrationTests
{
    private const string CreateMessageTemplate =
        @"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" xmlns:a=""http://www.w3.org/2005/08/addressing"">"
        + @"<s:Header><a:Action s:mustUnderstand=""1"">http://tempuri.org/ITestContract/Create</a:Action></s:Header>"
        + @"<s:Body><Create xmlns=""http://tempuri.org/""><name>{0}</name></Create></s:Body>"
        + @"</s:Envelope>";

    private const string ThrowMessageTemplate =
        @"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" xmlns:a=""http://www.w3.org/2005/08/addressing"">"
        + @"<s:Header><a:Action s:mustUnderstand=""1"">http://tempuri.org/ITestContract/Throw</a:Action></s:Header>"
        + @"<s:Body><Throw xmlns=""http://tempuri.org/""><name>{0}</name></Throw></s:Body>"
        + @"</s:Envelope>";

    private readonly ITestOutputHelper _output;

    public InMemoryQueueIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ReceiveContext_CompleteAsync_When_Success()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
        using (host)
        {
            host.Start();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<TestService>();
            var queue = resolver.GetService<ConcurrentQueue<string>>();
            var receiveContextInterceptor = resolver.GetService<ReceiveContextInterceptor>();

            testService.CountdownEvent.Reset(1);
            receiveContextInterceptor.CompleteCountdownEvent.Reset(1);

            string name = Guid.NewGuid().ToString();
            string message = string.Format(CreateMessageTemplate, name);

            queue.Enqueue(message);

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(receiveContextInterceptor.CompleteCountdownEvent.Wait(TimeSpan.FromSeconds(5)));

            Assert.Contains(name, testService.Names);
        }
    }

    [Fact]
    public void ReceiveContext_AbandonAsync_When_Service_Throw()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
        using (host)
        {
            host.Start();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<TestService>();
            var queue = resolver.GetService<ConcurrentQueue<string>>();
            var receiveContextInterceptor = resolver.GetService<ReceiveContextInterceptor>();
            receiveContextInterceptor.AbandonCountdownEvent.Reset(1);

            string name = Guid.Empty.ToString();
            string message = string.Format(ThrowMessageTemplate, name);

            queue.Enqueue(message);

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(receiveContextInterceptor.AbandonCountdownEvent.Wait(TimeSpan.FromSeconds(5)));
        }
    }

    [Fact]
    public void ReceiveContext_AbandonAsync_WhenFault()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
        using (host)
        {
            host.Start();
            var resolver = new DependencyResolverHelper(host);
            var queue = resolver.GetService<ConcurrentQueue<string>>();
            var receiveContextInterceptor = resolver.GetService<ReceiveContextInterceptor>();
            receiveContextInterceptor.AbandonCountdownEvent.Reset(1);

            string message = "poison-pill";

            queue.Enqueue(message);

            Assert.True(receiveContextInterceptor.AbandonCountdownEvent.Wait(TimeSpan.FromSeconds(5)));
        }
    }

    [Fact]
    public void ReceiveContext_Resiliency()
    {
        const int messageCount = 1000;
        const int throwCount = 50;
        const int poisonPillCount = 10;
        const int abandonCount = throwCount + poisonPillCount;
        const int completeCount = messageCount - abandonCount;
        const int reachedServiceCount = messageCount - poisonPillCount;

        IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
        using (host)
        {
            host.Start();
            var resolver = new DependencyResolverHelper(host);
            var queue = resolver.GetService<ConcurrentQueue<string>>();
            var receiveContextInterceptor = resolver.GetService<ReceiveContextInterceptor>();
            var testService = resolver.GetService<TestService>();
            receiveContextInterceptor.AbandonCountdownEvent.Reset(abandonCount);
            receiveContextInterceptor.CompleteCountdownEvent.Reset(completeCount);
            testService.CountdownEvent.Reset(reachedServiceCount);

            foreach (var message in GetMessages())
            {
                queue.Enqueue(message);
            }

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(30)));
            Assert.True(receiveContextInterceptor.CompleteCountdownEvent.Wait(TimeSpan.FromSeconds(30)));
            Assert.True(receiveContextInterceptor.AbandonCountdownEvent.Wait(TimeSpan.FromSeconds(30)));
        }

        IEnumerable<string> GetMessages()
        {
            Random random = new();
            int complete = 0;
            int @throw = 0;
            int poisonPill = 0;
            while ((complete + @throw + poisonPill) < messageCount)
            {
                int value = random.Next(0, 3);
                switch (value)
                {
                    case 0:
                        if (complete < completeCount)
                        {
                            complete++;
                            yield return string.Format(CreateMessageTemplate, value);
                        }
                        break;
                    case 1:
                        if (@throw < throwCount)
                        {
                            @throw++;
                            yield return string.Format(ThrowMessageTemplate, value);

                        }
                        break;
                    case 2:
                        if (poisonPill < poisonPillCount)
                        {
                            poisonPill++;
                            yield return "poison-pill";
                        }
                        break;
                    default:
                        throw new IndexOutOfRangeException();
                }
            }
        }
    }

    private class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<TestService>();
            services.AddServiceModelServices();
            services.AddQueueTransport();

            services.AddSingleton<ReceiveContextInterceptor>();
            services.AddSingleton<ConcurrentQueue<string>>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseServiceModel(services =>
            {
                services.AddService<TestService>();
                services.AddServiceEndpoint<TestService, ITestContract>(new InMemoryQueueBinding(), $"inmem://localhost:8080");
            });
        }
    }
}
