using System;
using CoreWCF.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
    }
}
