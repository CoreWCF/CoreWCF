using CoreWCF.Configuration;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CoreWCF.Http.Tests
{
    public class IntegrationTests
    {
        [Fact]
        public async Task NetHttpListenPortZeroWorks()
        {
            var host =
                WebHost.CreateDefaultBuilder()
                .UseKestrel(o => o.ListenAnyIP(0)) // <-- 0 tells Kestrel to pick any random free port. This is impractical for production, but really great for unit tests!
                .ConfigureServices((WebHostBuilderContext ctx, IServiceCollection services) =>
                {
                    services.AddServiceModelServices();
                })
                .Configure(app => {

                    app.UseServiceModel(builder =>
                    {
                        builder.AddService<SimpleService>();
                        builder.AddServiceEndpoint<SimpleService, ISimpleService>(new NetHttpBinding(), "/wcf");
                    });
                })
                .Build();

            try
            {
                host.Start();

                // check which port kestrel actually listens on
                var port = new Uri(host.ServerFeatures.Get<IServerAddressesFeature>().Addresses.First()).Port;

                var factory = new ChannelFactory<ISimpleService>(new System.ServiceModel.NetHttpBinding(), new System.ServiceModel.EndpointAddress($"http://localhost:{port}/wcf"));
                factory.Open();
                var channel = factory.CreateChannel();
                ((System.ServiceModel.IClientChannel)channel).Open();

                var response = channel.Echo("Hello");
                Assert.Equal("Hello", response);

                ((System.ServiceModel.IClientChannel)channel).Close();
                factory.Close();
            }
            finally
            {
                await host.StopAsync();
            }
        }
    }
}
