using System;
using System.IO;
using Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace CoreWCF.Http.Tests.IntegrationTests
{
    public class IntegrationTest<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
    {
        protected override TestServer CreateServer(IWebHostBuilder builder)
        {       
            var addresses = new ServerAddressesFeature();
            var features = new FeatureCollection();
            features.Set<IServerAddressesFeature>(addresses); 

            var server = new TestServer(builder, features);
#if NETCOREAPP3_1
            server.AllowSynchronousIO = true;
#endif
            return server;
        }

        protected override IWebHostBuilder CreateWebHostBuilder()
        {
            var contentRoot = Directory.GetCurrentDirectory();

            var assemblyName = typeof(IntegrationTest<TStartup>).Assembly.GetName().Name;
            var settingSuffix = assemblyName.ToUpperInvariant().Replace(".", "_");
            var settingName = $"ASPNETCORE_TEST_CONTENTROOT_{settingSuffix}";
            Environment.SetEnvironmentVariable(settingName, contentRoot);

            return ServiceHelper.CreateWebHostBuilder<TStartup>();
        }
    }
}