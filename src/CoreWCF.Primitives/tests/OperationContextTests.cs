// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public class OperationContextTests
    {
        private static readonly Uri _serviceAddress = new Uri($"http://localhost:8080/Test", UriKind.Absolute);

        [ServiceContract]
        private interface IContract
        {
            [OperationContract]
            void Operation();
        }

        // IncludeExceptionDetailInFaults is not needed to reproduce the original bug,
        // it's here purely for better Exception visibility in case of a failure.
        [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
        private class Service : IContract
        {
            public ManualResetEventSlim FirstRequestStoredOperationContextEvent { get; } = new ManualResetEventSlim(false);

            public ManualResetEventSlim SecondRequestInMidOperationEvent { get; } = new ManualResetEventSlim(false);

            public ManualResetEventSlim OperationContextAssertedEvent { get; } = new ManualResetEventSlim(false);


            public void Operation()
            {
                // This operation is expected to be requested exactly twice in parallel.
                // The goal of the events here is to perfectly synchronize the first and second requests in a way
                // that they will run in parallel and assert the OperationContext.Current before and while-running second request,
                // which is the original bug.

                // If this is the first request
                if (!FirstRequestStoredOperationContextEvent.IsSet)
                {
                    var context = OperationContext.Current;

                    // Signal that the first request is running and stored OperationContext.Current .
                    FirstRequestStoredOperationContextEvent.Set();

                    try
                    {
                        // Wait until a second request is running.
                        if (!SecondRequestInMidOperationEvent.Wait(TimeSpan.FromSeconds(30)))
                        {
                            throw new FaultException("An expected event from the second request did not set in time.");
                        }

                        // Assert
                        if (context != OperationContext.Current)
                        {
                            throw new FaultException("OperationContext.Current was replaced in mid-operation.");
                        }
                    }
                    finally
                    {
                        // Release the second request
                        OperationContextAssertedEvent.Set();
                    }
                }
                // If this is the second request
                else if (!SecondRequestInMidOperationEvent.IsSet)
                {
                    // Signal that the second request is running.
                    SecondRequestInMidOperationEvent.Set();

                    // Wait until OperationContext assertion happens in the first request
                    if (!OperationContextAssertedEvent.Wait(TimeSpan.FromSeconds(30)))
                    {
                        throw new FaultException("An expected assertion event from the first request did not set in time.");
                    }
                }
                else
                {
                    throw new FaultException("The operation was designed for 2 calls only.");
                }
            }
        }

        private class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
                services.AddSingleton<Service>();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Service>();
                    var binding = new BasicHttpBinding(Channels.BasicHttpSecurityMode.None);
                    builder.AddServiceEndpoint<Service, IContract>(binding, _serviceAddress);
                });
            }
        }

        [Fact]
        public async Task AccessingCurrentOperationContextBeforeStartingTheHostSouldNotCauseExceptionsLater()
        {
            // This single line is what actually caused the original bug to occur.
            // It must be placed before the host started.
            var context = OperationContext.Current;

            using (var host = TestHelper.CreateHost<Startup>(webHostBuilder =>
            {
                webHostBuilder.UseKestrel(o =>
                {
                    o.ListenAnyIP(_serviceAddress.Port);
                });
            }))
            {
                host.Start();

                var cancellationSource = new CancellationTokenSource();
                var cancellationToken = cancellationSource.Token;
                var service = host.Services.GetRequiredService<Service>();

                try
                {
                    Task firstRequest = RequestAndAssert(cancellationSource);

                    // Wait until the first request stored initial OperationContext.Current .
                    if (!service.FirstRequestStoredOperationContextEvent.Wait(TimeSpan.FromSeconds(30), cancellationToken))
                    {
                        // In case we don't get the event on time, we want first and foremost to see any exception thrown
                        // from the request.
                        await firstRequest;

                        // This is a fallback in case there is no exception from the request.
                        Assert.Fail("An expected context event from the first request did not set in time.");
                    }

                    // The first request is not done yet and waiting for a signal from a second request
                    // that needs to run in parallel to the first request.
                    // which is been triggered here, the second request will run in parallel to the first.
                    Task secondRequest = RequestAndAssert(cancellationSource);

                    // The first request is the one that actually asserts for the changed OperationContext.
                    // If there is any assertion failure, this is the one that will throw it.
                    await Task.WhenAll(firstRequest, secondRequest);
                }
                finally
                {
                    // Unblock any potentially blocked operations in case of any failure or unexpected bugs
                    service.FirstRequestStoredOperationContextEvent.Set();
                    service.SecondRequestInMidOperationEvent.Set();
                    service.OperationContextAssertedEvent.Set();
                }
            }
        }

        private static async Task RequestAndAssert(CancellationTokenSource cancellationSource)
        {
            var cancellationToken = cancellationSource.Token;

            using (var client = new HttpClient())
            {
                try
                {
                    //
                    // FIXME:
                    // - Pre-read buffer breaks message parsing when Transfer-Encoding is set to chunked (removes first byte)
                    // - When a message is invalid, but no error is thrown, message reply crashes because a null message is passed to the Close channel
                    //

                    var request = new HttpRequestMessage(HttpMethod.Post, _serviceAddress);

                    const string action = "http://tempuri.org/IContract/Operation";
                    request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{action}\"");

                    const string requestBody = @"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
   <s:Header/>
   <s:Body>
      <Operation xmlns=""http://tempuri.org/"" />
   </s:Body>
</s:Envelope>";

                    request.Content = new StringContent(requestBody, Encoding.UTF8, "text/xml");

                    // FIXME: Commenting out this line will induce a chunked response, which will break the pre-read message parser
                    request.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(requestBody);

                    var response = await client.SendAsync(request, cancellationToken);

#if NET5_0_OR_GREATER
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
#else
                    var responseBody = await response.Content.ReadAsStringAsync();
#endif

                    const string expected = "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                                            "<s:Body>" +
                                            "<OperationResponse xmlns=\"http://tempuri.org/\"/>" +
                                            "</s:Body>" +
                                            "</s:Envelope>";

                    // The <object> is a workaround for xUnit to print the whole string in case of a failure
                    Assert.Equal<object>(expected, responseBody);
                    Assert.True(response.IsSuccessStatusCode);
                }
                catch
                {
                    cancellationSource.Cancel();
                    throw;
                }
            }
        }
    }
}
