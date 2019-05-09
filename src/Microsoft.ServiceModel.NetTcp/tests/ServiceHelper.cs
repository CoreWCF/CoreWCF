using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

public static class ServiceHelper
{
    public static IWebHost CreateWebHost<TStartup>(params string[] urls) where TStartup : class
    {
        var config = new ConfigurationBuilder().Build();
        var builder = new WebHostBuilder()
            .UseConfiguration(config)
            .UseStartup<TStartup>()
            .UseKestrel()
            .UseUrls(urls);

        return builder.Build();
    }
}
