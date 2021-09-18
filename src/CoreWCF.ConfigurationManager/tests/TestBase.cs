// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoreWCF.ConfigurationManager.Tests
{
    public class TestBase
    {
        protected ServiceProvider CreateProvider(string path)
        {
            var services = new ServiceCollection();
            services.AddServiceModelServices();
            services.AddServiceModelConfigurationManagerFile(path);

            return services.BuildServiceProvider();
        }

        protected static IConfigurationHolder GetConfigurationHolder(ServiceProvider provider)
        {
            IConfigureOptions<ServiceModelOptions> options = provider.GetRequiredService<IConfigureOptions<ServiceModelOptions>>();
            options.Configure(new ServiceModelOptions());
            IConfigurationHolder settingHolder = provider.GetService<IConfigurationHolder>();
            return settingHolder;
        }

        //Limiting this logic for test purpose.
        protected static IXmlConfigEndpoint GetXmlConfigEndpoinByEndpointName(IConfigurationHolder configHolder, string name)
        {
           foreach(var serviceEndPoint in configHolder.Endpoints)
            {
                if (string.Compare(serviceEndPoint.Name, name, true) == 0)
                {
                    return configHolder.GetXmlConfigEndpoint(serviceEndPoint);
                }
            }
            throw new EndpointNotFoundException();
        }
    }
}
