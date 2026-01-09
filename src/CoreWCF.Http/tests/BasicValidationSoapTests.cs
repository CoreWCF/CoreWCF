// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class BasicValidationSoapTests
    {
        private readonly ITestOutputHelper _output;
        public BasicValidationSoapTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BasicRequestReplySoap()
        {
            IHost host = ServiceHelper.CreateWebHostBuilder<BasicValidationSoapTestsStartup>(_output).Build();
            using (host)
            {
                host.Start();
                ClientContract.IRequestReplyService client = ClientHelper.GetProxy<ClientContract.IRequestReplyService>(host);
                _output.WriteLine("Invoking service operation DownloadData");
                _output.WriteLine("Response = {0}", client.DownloadData());

                _output.WriteLine("Invoking service operation UploadData");
                client.UploadData("ContentToReplace");

                _output.WriteLine("Invoking service operation DownloadStream");
                Stream downloadedStream = client.DownloadStream();
                _output.WriteLine("Response = ...");

                // Read from the stream...
                StreamReader reader = new StreamReader(downloadedStream);
                string content = reader.ReadToEnd();
                _output.WriteLine(content);

                _output.WriteLine("Invoking service operation UploadStream");
                byte[] buffer = new byte[1024];
                Random rand = new Random();
                rand.NextBytes(buffer);
                MemoryStream uploadStream = new MemoryStream(buffer);
                client.UploadStream(uploadStream);

                _output.WriteLine("Getting Log from service.  Result: ...");
                foreach (string logItem in client.GetLog())
                {
                    _output.WriteLine(logItem);
                }
            }
        }
    }

    internal class BasicValidationSoapTestsStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<Services.RequestReplyService>();
                builder.AddServiceEndpoint<Services.RequestReplyService, IRequestReplyService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
            });
        }
    }
}
