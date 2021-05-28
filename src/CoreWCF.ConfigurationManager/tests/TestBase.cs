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
            services.AddConfigurationManagerFile(path);

            return services.BuildServiceProvider();
        }

        protected static IConfigurationHolder GetConfigurationHolder(ServiceProvider provider)
        {
            IConfigureOptions<ServiceModelOptions> options = provider.GetRequiredService<IConfigureOptions<ServiceModelOptions>>();
            options.Configure(new ServiceModelOptions());
            IConfigurationHolder settingHolder = provider.GetService<IConfigurationHolder>();
            return settingHolder;
        }
    }
}
