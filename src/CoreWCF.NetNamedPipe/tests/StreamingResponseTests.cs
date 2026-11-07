// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Contract;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Services;
using Xunit;

#if !NET472
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
public class StreamingResponseTests
{
    private readonly ITestOutputHelper _output;

    public StreamingResponseTests(ITestOutputHelper output) => _output = output;

    [WindowsOnlyTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(65535)]
    [InlineData(65536)]
    [InlineData(65537)]
    [InlineData(131071)]
    [InlineData(131072)]
    [InlineData(131073)]
    [InlineData(262145)]
    [InlineData(8388625)]
    public async Task StreamedResponseDeliversFinalSegment(int length)
    {
        using (IWebHost host = CreateHost(nameof(StreamedResponseDeliversFinalSegment)))
        {
            await host.StartAsync();
            using (System.ServiceModel.ChannelFactory<IStreamingService> factory = CreateFactory(nameof(StreamedResponseDeliversFinalSegment)))
            {
                IStreamingService channel = factory.CreateChannel();
                try
                {
                    for (int request = 0; request < 2; request++)
                    {
                        using (Stream stream = channel.Download(length, 0))
                        {
                            await VerifyAsync(stream, length);
                        }
                    }
                    ((System.ServiceModel.IClientChannel)channel).Close();
                }
                finally
                {
                    ((System.ServiceModel.IClientChannel)channel).Abort();
                }
            }
        }
    }

    [WindowsOnlyFact]
    public async Task SecuredStreamedResponseDeliversFinalSegment()
    {
        using (IWebHost host = CreateHost(nameof(SecuredStreamedResponseDeliversFinalSegment)))
        {
            await host.StartAsync();
            using (System.ServiceModel.ChannelFactory<IStreamingService> factory = CreateFactory(nameof(SecuredStreamedResponseDeliversFinalSegment), secure: true))
            {
                IStreamingService channel = factory.CreateChannel();
                try
                {
                    using (Stream stream = channel.Download(262145, 0))
                    {
                        await VerifyAsync(stream, 262145);
                    }
                    ((System.ServiceModel.IClientChannel)channel).Close();
                }
                finally
                {
                    ((System.ServiceModel.IClientChannel)channel).Abort();
                }
            }
        }
    }

    [WindowsOnlyTheory]
    [InlineData(1024)]
    [InlineData(4096)]
    [InlineData(65536)]
    public async Task StreamedResponseFlushesBeforeProducerFinishes(int prefixLength)
    {
        using (IWebHost host = CreateHost(nameof(StreamedResponseFlushesBeforeProducerFinishes)))
        {
            await host.StartAsync();
            StreamingState state = host.Services.GetRequiredService<StreamingState>();
            using (System.ServiceModel.ChannelFactory<IStreamingService> factory = CreateFactory(nameof(StreamedResponseFlushesBeforeProducerFinishes)))
            {
                IStreamingService channel = factory.CreateChannel();
                try
                {
                    Task readPrefix = Task.Run(async () =>
                    {
                        using (Stream stream = channel.Download(131073, prefixLength))
                        {
                            byte[] buffer = new byte[1024];
                            Assert.True(await stream.ReadAsync(buffer, 0, buffer.Length) > 0);
                            // Do not drain the stream here: the producer is intentionally paused.
                            state.Release.Set();
                            await stream.CopyToAsync(Stream.Null);
                        }
                    });
                    Assert.True(state.Paused.Wait(TimeSpan.FromSeconds(10)), "The producer did not reach its pause point.");
                    Task completed = await Task.WhenAny(readPrefix, Task.Delay(TimeSpan.FromSeconds(10)));
                    Assert.Same(readPrefix, completed);
                    await readPrefix;
                    ((System.ServiceModel.IClientChannel)channel).Close();
                }
                finally
                {
                    state.Release.Set();
                    ((System.ServiceModel.IClientChannel)channel).Abort();
                }
            }
        }
    }

    [WindowsOnlyTheory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task SlowReaderAppliesBoundedBackpressureAndCanResume(int clientCount)
    {
        using (IWebHost host = CreateHost(nameof(SlowReaderAppliesBoundedBackpressureAndCanResume)))
        {
            await host.StartAsync();
            StreamingState state = host.Services.GetRequiredService<StreamingState>();
            using (System.ServiceModel.ChannelFactory<IStreamingService> factory = CreateFactory(nameof(SlowReaderAppliesBoundedBackpressureAndCanResume)))
            {
                IStreamingService[] channels = new IStreamingService[clientCount];
                Stream[] streams = new Stream[clientCount];
                try
                {
                    for (int i = 0; i < clientCount; i++)
                    {
                        channels[i] = factory.CreateChannel();
                        streams[i] = channels[i].Download(8 * 1024 * 1024 + 17, 0);
                    }
                    await Task.Delay(250);
                    long first = Interlocked.Read(ref state.BytesRead);
                    await Task.Delay(500);
                    long second = Interlocked.Read(ref state.BytesRead);
                    _output.WriteLine($"{clientCount} unread responses: producer read {first} then {second} bytes.");
                    // Includes one encoder read beyond the pause threshold and native/client buffers.
                    Assert.InRange(second, 1, clientCount * 512 * 1024);
                    Assert.Equal(first, second);
                    Task[] readers = new Task[clientCount];
                    for (int i = 0; i < clientCount; i++)
                    {
                        readers[i] = VerifyAsync(streams[i], 8 * 1024 * 1024 + 17);
                    }
                    await Task.WhenAll(readers);
                    for (int i = 0; i < clientCount; i++)
                    {
                        streams[i].Dispose();
                        using (Stream tail = channels[i].Download(17, 0))
                        {
                            await VerifyAsync(tail, 17);
                        }
                        ((System.ServiceModel.IClientChannel)channels[i]).Close();
                    }
                }
                finally
                {
                    for (int i = 0; i < clientCount; i++)
                    {
                        ((System.ServiceModel.IClientChannel)channels[i])?.Abort();
                        streams[i]?.Dispose();
                    }
                }
            }
        }
    }

    [WindowsOnlyFact]
    public async Task AbortedSlowResponseDoesNotPreventSubsequentRequests()
    {
        using (IWebHost host = CreateHost(nameof(AbortedSlowResponseDoesNotPreventSubsequentRequests)))
        {
            await host.StartAsync();
            using (System.ServiceModel.ChannelFactory<IStreamingService> factory = CreateFactory(nameof(AbortedSlowResponseDoesNotPreventSubsequentRequests)))
            {
                IStreamingService channel = factory.CreateChannel();
                Stream stream = null;
                try
                {
                    stream = channel.Download(8 * 1024 * 1024 + 17, 0);
                    await Task.Delay(250);
                }
                finally
                {
                    ((System.ServiceModel.IClientChannel)channel).Abort();
                    stream?.Dispose();
                }

                IStreamingService next = factory.CreateChannel();
                try
                {
                    using (Stream tail = next.Download(17, 0))
                    {
                        await VerifyAsync(tail, 17);
                    }
                    ((System.ServiceModel.IClientChannel)next).Close();
                }
                finally
                {
                    ((System.ServiceModel.IClientChannel)next).Abort();
                }
            }
        }
    }

    private IWebHost CreateHost(string path) => ServiceHelper.CreateWebHostBuilder<Startup>(_output, path)
        .UseUrls("http://127.0.0.1:0").Build();

    private static System.ServiceModel.ChannelFactory<IStreamingService> CreateFactory(string path, bool secure = false)
    {
        System.ServiceModel.NetNamedPipeBinding binding = new System.ServiceModel.NetNamedPipeBinding(
            secure ? System.ServiceModel.NetNamedPipeSecurityMode.Transport : System.ServiceModel.NetNamedPipeSecurityMode.None)
        {
            TransferMode = System.ServiceModel.TransferMode.StreamedResponse,
            MaxReceivedMessageSize = long.MaxValue,
            MaxBufferSize = 65536,
            ReceiveTimeout = TimeSpan.FromSeconds(15),
            SendTimeout = TimeSpan.FromSeconds(15)
        };
        string endpoint = secure ? "secure-stream.svc" : "stream.svc";
        return new System.ServiceModel.ChannelFactory<IStreamingService>(binding,
            new System.ServiceModel.EndpointAddress($"net.pipe://localhost/{path}/{endpoint}"));
    }

    private static async Task VerifyAsync(Stream stream, int length)
    {
        byte[] buffer = new byte[65536];
        int position = 0;
        int count;
        while ((count = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
        {
            Assert.True(position + count <= length, "Response exceeded the expected length.");
            for (int i = 0; i < count; i++)
            {
                if (buffer[i] != (byte)((position + i) % 251))
                {
                    Assert.Fail($"Unexpected byte at position {position + i}.");
                }
            }
            position += count;
        }
        Assert.Equal(length, position);
    }

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
            services.AddSingleton<StreamingState>();
            services.AddSingleton<StreamingService>();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<StreamingService>();
                builder.AddServiceEndpoint<StreamingService, IStreamingService>(
                    new CoreWCF.NetNamedPipeBinding(CoreWCF.NetNamedPipeSecurityMode.None)
                    {
                        TransferMode = CoreWCF.TransferMode.StreamedResponse
                    }, "stream.svc");
                builder.AddServiceEndpoint<StreamingService, IStreamingService>(
                    new CoreWCF.NetNamedPipeBinding
                    {
                        TransferMode = CoreWCF.TransferMode.StreamedResponse
                    }, "secure-stream.svc");
            });
        }
    }
}
