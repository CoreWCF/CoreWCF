// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.WebHttp.Tests
{
    public class SerializationTests
    {
        private readonly ITestOutputHelper _output;

        public SerializationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task SerializeDeserializeJson()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();

                ServiceContract.SerializationData requestData = GetRequestData();
                (HttpStatusCode statusCode, string content) = await HttpHelpers.PostJsonAsync("api/json", requestData);
                ServiceContract.SerializationData responseData = SerializationHelpers.DeserializeJson<ServiceContract.SerializationData>(content);

                VerifyResponseData(statusCode, responseData);
            }
        }

        [Fact]
        public async Task SerializeDeserializeXml()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();

                ServiceContract.SerializationData requestData = GetRequestData();
                (HttpStatusCode statusCode, string content) = await HttpHelpers.PostXmlAsync("api/xml", requestData);
                ServiceContract.SerializationData responseData = SerializationHelpers.DeserializeXml<ServiceContract.SerializationData>(content);

                VerifyResponseData(statusCode, responseData);
            }
        }

        private ServiceContract.SerializationData GetRequestData() => new ServiceContract.SerializationData()
        {
            Items = new List<ServiceContract.SerializationDatum>()
            {
                new ServiceContract.SerializationDatum()
                {
                    NumericField = 1,
                    StringField = "test",
                    BooleanField = true,
                    DateTimeField = new DateTime(2022, 01, 01),
                    TimeSpanField = TimeSpan.FromMilliseconds(10),
                    DateTimeOffsetField = new DateTimeOffset(new DateTime(2022, 02, 02)),
                    GuidField = new Guid("166d37d2-e712-4233-96e7-8cd1d40e9da2"),
                    UriField = new Uri("http://microsoft.com")
                }
            }
        };

        private void VerifyResponseData(HttpStatusCode statusCode, ServiceContract.SerializationData responseData)
        {
            Assert.Equal(HttpStatusCode.OK, statusCode);
            ServiceContract.SerializationDatum responseDatum = Assert.Single(responseData.Items);
            Assert.Equal(1, responseDatum.NumericField);
            Assert.Equal("test", responseDatum.StringField);
            Assert.True(responseDatum.BooleanField);
            Assert.Equal(new DateTime(2022, 01, 01), responseDatum.DateTimeField);
            Assert.Equal(TimeSpan.FromMilliseconds(10), responseDatum.TimeSpanField);
            Assert.Equal(new DateTimeOffset(new DateTime(2022, 02, 02)), responseDatum.DateTimeOffsetField);
            Assert.Equal(new Guid("166d37d2-e712-4233-96e7-8cd1d40e9da2"), responseDatum.GuidField);
            Assert.Equal(new Uri("http://microsoft.com"), responseDatum.UriField);
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
                    builder.AddService<Services.SerializationService>();
                    builder.AddServiceWebEndpoint<Services.SerializationService, ServiceContract.ISerializationService>("api");
                });
            }
        }
    }
}
