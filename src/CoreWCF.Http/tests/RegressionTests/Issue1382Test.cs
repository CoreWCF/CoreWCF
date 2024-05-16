// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Http.Tests.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.RegressionTests;

public class Issue1382Test : IClassFixture<IntegrationTest<Issue1382Test.Startup>>
{
    private readonly ITestOutputHelper _output;
    private readonly IntegrationTest<Startup> _factory;

    public Issue1382Test(ITestOutputHelper output, IntegrationTest<Startup> factory)
    {
        _output = output;
        _factory = factory;
    }

    [Fact]
    public async Task DefaultsNonPassedInParametersWhenTypeIsValueType()
    {
        //
        // FIXME:
        // - Pre-read buffer breaks message parsing when Transfer-Encoding is set to chunked (removes first byte)
        // - When a message is invalid, but no error is thrown, message reply crashes because a null message is passed to the Close channel
        //

        var client = _factory.CreateClient();
        const string action = "http://tempuri.org/IEchoService/Echo";

        var request = new HttpRequestMessage(HttpMethod.Post, new Uri("http://localhost:8080/BasicWcfService/basichttp.svc", UriKind.Absolute));
        request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{action}\"");

        const string requestBody = @"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tem=""http://tempuri.org/"">
   <s:Header/>
   <s:Body>
      <tem:Echo>
      </tem:Echo>
   </s:Body>
</s:Envelope>";

        request.Content = new StringContent(requestBody, Encoding.UTF8, "text/xml");

        // FIXME: Commenting out this line will induce a chunked response, which will break the pre-read message parser
        request.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(requestBody);

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        _output.WriteLine(responseBody);

        const string expected = "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                                "<s:Body>" +
                                "<EchoResponse xmlns=\"http://tempuri.org/\">" +
                                "<EchoResult>0</EchoResult>" +
                                "</EchoResponse>" +
                                "</s:Body>" +
                                "</s:Envelope>";

        Assert.Equal(expected, responseBody);
    }

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<EchoService>();
                builder.AddServiceEndpoint<EchoService, IEchoService>(new BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
            });
        }
    }

    [ServiceContract]
    public interface IEchoService
    {
        [OperationContract]
        int Echo(int value);
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class EchoService : IEchoService
    {
        public int Echo(int value) => value;
    }
}
