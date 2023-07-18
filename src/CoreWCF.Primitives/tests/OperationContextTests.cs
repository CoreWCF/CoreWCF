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
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public partial class OperationContextTests
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
            public void Operation()
            {
                var context = OperationContext.Current;

                // This sleep is not needed to reproduce the original bug in a real environment,
                // but is essential to reproduce it in a test environment.
                Thread.Sleep(100);

                if (context != OperationContext.Current)
                {
                    throw new FaultException("OperationContext.Current was replaced in mid-operation.");
                }
            }
        }

        private class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
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
            // This single line is what actually caused the original bug to occur
            var context = OperationContext.Current;

            var builder = WebHost.CreateDefaultBuilder<Startup>(null);
            builder.UseKestrel(o =>
            {
                o.ListenAnyIP(_serviceAddress.Port);
            });
            var host = builder.Build();
            using (host)
            {
                host.Start();

                CancellationTokenSource cancellationSource = new CancellationTokenSource();
                CancellationToken cancellationToken = cancellationSource.Token;

                IEnumerable<Task> clientTasks = Enumerable.Range(0, 2).Select(async i =>
                {
                    using (var client = new HttpClient())
                    {
                        for (int j = 0; j < 10 && !cancellationToken.IsCancellationRequested; j++)
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

                                var response = await client.SendAsync(request);

                                var responseBody = await response.Content.ReadAsStringAsync();

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
                });

                await Task.WhenAll(clientTasks);
            }
        }
    }
}
