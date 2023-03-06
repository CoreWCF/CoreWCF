using System;
using System.IO;
using System.Net.Http;
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
        private readonly int _httpPort = TcpPortHelper.GetFreeTcpPort();

        public int GetHttpPort() => _httpPort;

        protected override TestServer CreateServer(IWebHostBuilder builder)
        {
            var addresses = new ServerAddressesFeature();
            addresses.Addresses.Add($"http://localhost:{_httpPort}/");
            var features = new FeatureCollection();
            features.Set<IServerAddressesFeature>(addresses);

            var server = new TestServer(builder, features);
            return server;
        }

        protected override IWebHostBuilder CreateWebHostBuilder()
        {
            SetSelfHostedContentRoot();

            return ServiceHelper.CreateWebHostBuilder<TStartup>();
        }

        private static void SetSelfHostedContentRoot()
        {
            var contentRoot = Directory.GetCurrentDirectory();
            var assemblyName = typeof(IntegrationTest<TStartup>).Assembly.GetName().Name;
            var settingSuffix = assemblyName.ToUpperInvariant().Replace(".", "_");
            var settingName = $"ASPNETCORE_TEST_CONTENTROOT_{settingSuffix}";
            Environment.SetEnvironmentVariable(settingName, contentRoot);
        }
    }
}
