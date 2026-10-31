// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using CoreWCF.Configuration;
using CoreWCF.Extensions.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CoreWCF.Extensions.Configuration.Tests
{
    public class ServiceModelConfigurationExtensionsTests
    {
        private const string ServiceTypeName = "CoreWCF.Extensions.Configuration.Tests.EchoService, CoreWCF.Extensions.Configuration.Tests";
        private const string EchoContractName = "CoreWCF.Extensions.Configuration.Tests.IEchoService, CoreWCF.Extensions.Configuration.Tests";

        private static IConfiguration Configure(Dictionary<string, string> data) =>
            new ConfigurationBuilder().AddInMemoryCollection(data).Build().GetSection("ServiceModel");

        private static Dictionary<string, string> ValidConfiguration() => new Dictionary<string, string>
        {
            ["ServiceModel:Bindings:internal:Type"] = "CoreWCF.NetTcpBinding, CoreWCF.NetTcp",
            [$"ServiceModel:Services:{ServiceTypeName}:Endpoints:0:Contract"] = EchoContractName,
            [$"ServiceModel:Services:{ServiceTypeName}:Endpoints:0:Binding"] = "internal",
            [$"ServiceModel:Services:{ServiceTypeName}:Endpoints:0:Address"] = "net.tcp://localhost:8089/echo",
        };

        [Fact]
        public void AddServiceModelConfiguration_ConfiguresServiceModelOptions()
        {
            var services = new ServiceCollection();
            services.AddServiceModelConfiguration(Configure(ValidConfiguration()));

            ServiceProvider provider = services.BuildServiceProvider();

            // Materialising the options runs the reader through the whole DI pipeline.
            ServiceModelOptions options = provider.GetRequiredService<IOptions<ServiceModelOptions>>().Value;

            Assert.NotNull(options);
        }

        [Fact]
        public void AddServiceModelConfiguration_SurfacesConfigurationErrorsWhenOptionsAreBuilt()
        {
            Dictionary<string, string> data = ValidConfiguration();
            data[$"ServiceModel:Services:{ServiceTypeName}:Endpoints:0:Binding"] = "not-declared";

            var services = new ServiceCollection();
            services.AddServiceModelConfiguration(Configure(data));

            ServiceProvider provider = services.BuildServiceProvider();

            BindingConfigurationException exception = Assert.Throws<BindingConfigurationException>(
                () => provider.GetRequiredService<IOptions<ServiceModelOptions>>().Value);

            Assert.Contains("not-declared", exception.Message);
        }

        [Fact]
        public void RegisteredNames_ServeBindingsServicesAndContractsAlike()
        {
            // One registry resolves every kind of type name, so a host that registers short names gets them
            // everywhere rather than only where the binding discriminator happens to look.
            var registry = new ServiceModelTypeRegistry()
                .Add("netTcp", typeof(NetTcpBinding))
                .Add("echoService", typeof(EchoService))
                .Add("echoContract", typeof(IEchoService));

            var services = new ServiceCollection();
            services.AddSingleton(registry);
            services.AddServiceModelConfiguration(Configure(new Dictionary<string, string>
            {
                ["ServiceModel:Bindings:internal:Type"] = "netTcp",
                ["ServiceModel:Services:echoService:Endpoints:0:Contract"] = "echoContract",
                ["ServiceModel:Services:echoService:Endpoints:0:Binding"] = "internal",
                ["ServiceModel:Services:echoService:Endpoints:0:Address"] = "net.tcp://localhost:8089/echo",
            }));

            ServiceProvider provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetRequiredService<IOptions<ServiceModelOptions>>().Value);

            // The same registry backs the reader, so the endpoint really was built from the registered names.
            ServiceEndpointDefinition endpoint = provider
                .GetRequiredService<ServiceModelConfigurationReader>()
                .ReadEndpoints(Configure(new Dictionary<string, string>
                {
                    ["ServiceModel:Bindings:internal:Type"] = "netTcp",
                    ["ServiceModel:Services:echoService:Endpoints:0:Contract"] = "echoContract",
                    ["ServiceModel:Services:echoService:Endpoints:0:Binding"] = "internal",
                    ["ServiceModel:Services:echoService:Endpoints:0:Address"] = "net.tcp://localhost:8089/echo",
                }))
                .Single();

            Assert.Equal(typeof(EchoService), endpoint.ServiceType);
            Assert.Equal(typeof(IEchoService), endpoint.Contract);
            Assert.IsType<NetTcpBinding>(endpoint.Binding);
        }

        [Fact]
        public void AddServiceModelConfiguration_LetsTheHostReplaceTheRegisteredServices()
        {
            var services = new ServiceCollection();
            var registry = new ServiceModelTypeRegistry().Add(typeof(NetTcpBinding));

            // Registered before the extension, so its TryAdd calls leave this in place.
            services.AddSingleton(new BindingHydrator(new BindingHydratorOptions { Registry = registry }));
            services.AddServiceModelConfiguration(Configure(ValidConfiguration()));

            ServiceProvider provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetRequiredService<IOptions<ServiceModelOptions>>().Value);
        }
    }
}
