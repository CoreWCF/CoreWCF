// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWCF.WebHttp.Tests
{
    public class WebOperationContextTests
    {
        private readonly ITestOutputHelper _output;

        public WebOperationContextTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task StatusCodeSet()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();

                (HttpStatusCode statusCode, string _) = await HttpHelpers.GetAsync(host.GetHttpBaseAddressUri(), "api/statuscode");

                Assert.Equal(HttpStatusCode.Accepted, statusCode);
            }
        }

        [Fact]
        public async Task ResponseHeaderAdded()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();

                using HttpClient httpClient = new HttpClient();

                HttpResponseMessage response = await httpClient.GetAsync($"http://localhost:{host.GetHttpPort()}/api/responseheader");

                Assert.True(response.Headers.Contains("TestHeader"));
                Assert.Equal("test", string.Join("", response.Headers.GetValues("TestHeader")));
            }
        }

        [Fact]
        public async Task ContentTypeSet()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();

                using HttpClient httpClient = new HttpClient();

                HttpResponseMessage response = await httpClient.GetAsync($"http://localhost:{host.GetHttpPort()}/api/contenttype");

                Assert.Equal("text/plain", response.Content.Headers.ContentType.ToString());
            }
        }

        [Fact]
        public async Task CanInspectRouteMatch()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();

                (HttpStatusCode _, string content) = await HttpHelpers.GetAsync(host.GetHttpBaseAddressUri(), "api/match");

                Assert.Equal($"\"http:\\/\\/127.0.0.1:{host.GetHttpPort()}\\/api\\/match\"", content);
            }
        }

        // RFC 7231 section 7.1.1.1 defines three accepted HTTP-date formats:
        // IMF-fixdate (RFC1123), RFC 850 (obsolete), and ANSI C asctime.
        public static TheoryData<string, string> HttpDateFormats => new TheoryData<string, string>
        {
            // expectedUtc (ISO 8601), header value
            { "1994-11-06T08:49:37.0000000Z", "Sun, 06 Nov 1994 08:49:37 GMT" },        // RFC 1123 / IMF-fixdate
            { "1994-11-06T08:49:37.0000000Z", "Sunday, 06-Nov-94 08:49:37 GMT" },       // RFC 850
            { "1994-11-06T08:49:37.0000000Z", "Sun Nov  6 08:49:37 1994" },             // ANSI C asctime
        };

        [Theory]
        [MemberData(nameof(HttpDateFormats))]
        public async Task IfModifiedSinceParsedFromAllHttpDateFormats(string expectedUtcIso, string headerValue)
        {
            string actual = await SendWithHeaderAsync("api/ifmodifiedsince", "If-Modified-Since", headerValue);
            DateTime expected = DateTime.Parse(expectedUtcIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            DateTime parsed = DateTime.Parse(actual, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            Assert.Equal(expected.ToUniversalTime(), parsed.ToUniversalTime());
        }

        [Theory]
        [MemberData(nameof(HttpDateFormats))]
        public async Task IfUnmodifiedSinceParsedFromAllHttpDateFormats(string expectedUtcIso, string headerValue)
        {
            string actual = await SendWithHeaderAsync("api/ifunmodifiedsince", "If-Unmodified-Since", headerValue);
            DateTime expected = DateTime.Parse(expectedUtcIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            DateTime parsed = DateTime.Parse(actual, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            Assert.Equal(expected.ToUniversalTime(), parsed.ToUniversalTime());
        }

        [Theory]
        [InlineData("api/ifmodifiedsince", "If-Modified-Since")]
        [InlineData("api/ifunmodifiedsince", "If-Unmodified-Since")]
        public async Task ConditionalDateHeaderReturnsNullWhenAbsent(string url, string _)
        {
            string actual = await SendWithHeaderAsync(url, headerName: null, headerValue: null);
            Assert.Equal("null", actual);
        }

        [Theory]
        [InlineData("api/ifmodifiedsince", "If-Modified-Since")]
        [InlineData("api/ifunmodifiedsince", "If-Unmodified-Since")]
        public async Task ConditionalDateHeaderReturnsNullWhenInvalid(string url, string headerName)
        {
            string actual = await SendWithHeaderAsync(url, headerName, "not-a-real-date");
            Assert.Equal("null", actual);
        }

        private async Task<string> SendWithHeaderAsync(string relativeUrl, string headerName, string headerValue)
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();

                using HttpClient httpClient = new HttpClient();
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get,
                    $"http://localhost:{host.GetHttpPort()}/{relativeUrl}");
                if (!string.IsNullOrEmpty(headerName))
                {
                    request.Headers.TryAddWithoutValidation(headerName, headerValue);
                }

                using HttpResponseMessage response = await httpClient.SendAsync(request);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                string body = await response.Content.ReadAsStringAsync();
                // Default WebHttp formatting wraps a string return value as a JSON string ("value").
                if (body.Length >= 2 && body[0] == '"' && body[body.Length - 1] == '"')
                {
                    body = body.Substring(1, body.Length - 2);
                }
                return body;
            }
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelWebServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.WebOperationContextService>();
                    builder.AddServiceWebEndpoint<Services.WebOperationContextService, ServiceContract.IWebOperationContextService>("api");
                });
            }
        }
    }
}
