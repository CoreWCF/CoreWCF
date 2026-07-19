// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

#if !NET472
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
public class NamedPipeListenerStartupTests
{
    private readonly ITestOutputHelper _output;

    public NamedPipeListenerStartupTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // The named pipe listener publishes its randomly chosen pipe name through a shared
    // memory section so that clients on the same machine can locate the endpoint. The
    // accept pump that actually creates the pipe instance runs after the name is
    // published, so any other process that can read the shared memory could create the
    // pipe with that name first. The listener must therefore make its own first
    // CreateNamedPipe call refuse to attach to a pre-existing instance, so that an
    // already-claimed name results in a hard startup failure rather than a silent
    // attach to a foreign pipe namespace.
    [WindowsOnlyFact]
    public async Task NetPipeStartupFailsWhenPublishedPipeNameAlreadyExists()
    {
        string basePath = nameof(NetPipeStartupFailsWhenPublishedPipeNameAlreadyExists);
        var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output, basePath).Build();
        using (host)
        {
            object listener = CreateListener(host.Services);

            await InvokeBindAsync(listener);
            try
            {
                string pipeName = GetPublishedPipeName(listener);
                Assert.StartsWith(@"\\.\pipe\", pipeName);
                string localPipeName = pipeName.Substring(@"\\.\pipe\".Length);

                using (var squatter = new NamedPipeServerStream(
                    localPipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous))
                {
                    InvokeStartAccepting(listener);

                    Task[] pumpTasks = GetAcceptPumpTasks(listener);
                    Assert.NotEmpty(pumpTasks);

                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    Task firstFault = await Task.WhenAny(
                        Task.WhenAny(pumpTasks),
                        Task.Delay(Timeout.Infinite, cts.Token));

                    Assert.False(cts.IsCancellationRequested,
                        "The accept pump should fault immediately when its pipe name has already been claimed by another process.");

                    Task faulted = pumpTasks.First(t => t.IsFaulted || t.IsCompleted);
                    Assert.True(faulted.IsFaulted, "Expected the accept pump to surface the name collision as a fault.");
                }
            }
            finally
            {
                await InvokeStopAsync(listener);
            }
        }
    }

    private static object CreateListener(IServiceProvider hostServices)
    {
        Assembly assembly = typeof(CoreWCF.NetNamedPipeBinding).Assembly;

        // Resolving the options triggers the configuration setup that wires
        // ApplicationServices, which the listener uses to resolve its dependencies.
        Type optionsType = assembly.GetType("CoreWCF.Channels.NetNamedPipeOptions", throwOnError: true);
        Type iOptionsType = typeof(IOptions<>).MakeGenericType(optionsType);
        object iOptions = hostServices.GetRequiredService(iOptionsType);
        object netNamedPipeOptions = iOptionsType.GetProperty("Value").GetValue(iOptions);

        var codeBackedListenOptions = (IList)optionsType
            .GetProperty("CodeBackedListenOptions", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .GetValue(netNamedPipeOptions);

        Assert.NotEmpty(codeBackedListenOptions);
        var listenOptions = (NamedPipeListenOptions)codeBackedListenOptions[0];

        Type listenerType = assembly.GetType("CoreWCF.Channels.NamedPipeListener", throwOnError: true);
        return Activator.CreateInstance(
            listenerType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { listenOptions },
            culture: null);
    }

    private static Task InvokeBindAsync(object listener)
    {
        MethodInfo bindAsync = listener.GetType().GetMethod(
            "BindAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return (Task)bindAsync.Invoke(listener, new object[] { CancellationToken.None });
    }

    private static void InvokeStartAccepting(object listener)
    {
        MethodInfo startAccepting = listener.GetType().GetMethod(
            "StartAccepting",
            BindingFlags.Instance | BindingFlags.NonPublic);
        startAccepting.Invoke(listener, Array.Empty<object>());
    }

    private static Task InvokeStopAsync(object listener)
    {
        MethodInfo stopAsync = listener.GetType().GetMethod(
            "StopAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return (Task)stopAsync.Invoke(listener, new object[] { CancellationToken.None });
    }

    private static string GetPublishedPipeName(object listener)
    {
        FieldInfo sharedMemoryField = listener.GetType().GetField(
            "_sharedMemory",
            BindingFlags.Instance | BindingFlags.NonPublic);
        object sharedMemory = sharedMemoryField.GetValue(listener);
        Assert.NotNull(sharedMemory);

        PropertyInfo pipeNameProperty = sharedMemory.GetType().GetProperty(
            "PipeName",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var pipeName = (string)pipeNameProperty.GetValue(sharedMemory);
        Assert.NotNull(pipeName);
        return pipeName;
    }

    private static Task[] GetAcceptPumpTasks(object listener)
    {
        FieldInfo tasksField = listener.GetType().GetField(
            "_tasks",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return (Task[])tasksField.GetValue(listener);
    }

    internal class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<Services.EchoService>();
                builder.AddServiceEndpoint<Services.EchoService, Contract.IEchoService>(
                    new CoreWCF.NetNamedPipeBinding(), "netpipe.svc");
            });
        }
    }
}
