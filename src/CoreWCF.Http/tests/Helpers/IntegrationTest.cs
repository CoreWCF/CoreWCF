// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace CoreWCF.Http.Tests.Helpers
{
    /// <summary>
    /// Enables in-memory integration testing for CoreWCF (outside-in testing via <see cref="HttpClient"/>).
    ///
    /// Use these tests to exercise the entire HTTP stack, rather than create in-process ServiceModel channels.
    /// 
    /// <see href="https://docs.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-3.1"/>
    /// <seealso href="https://docs.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-2.1"/>
    /// </summary>
    /// <typeparam name="TStartup"></typeparam>
    public class IntegrationTest<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
    {
        protected override TestServer CreateServer(IWebHostBuilder builder)
        {
            var addresses = new ServerAddressesFeature();
            addresses.Addresses.Add("http://localhost/");
            var features = new FeatureCollection();
            features.Set<IServerAddressesFeature>(addresses);

            var server = new TestServer(builder, features);
            return server;
        }

        protected override IWebHostBuilder CreateWebHostBuilder()
        {
            return ServiceHelper.CreateWebHostBuilder<TStartup>();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Use the output directory as content root. WebApplicationFactory's default
            // resolution can fail (e.g. UseSolutionRelativeContentRoot constructs a path
            // that doesn't exist). These tests don't need a real content root.
            builder.UseContentRoot(AppContext.BaseDirectory);
        }
    }
}
