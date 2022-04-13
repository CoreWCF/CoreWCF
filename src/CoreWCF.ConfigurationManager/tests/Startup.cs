// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.ConfigurationManager.Tests
{
    public class Startup
    {
        public const string EndpointAddress = "/netTcp.svc/Buffered";

        public void ConfigureServices(IServiceCollection services)
        {
            string pathToXml = GetXmlConfigFilePath();
            services.AddServiceModelServices();
            services.AddServiceModelConfigurationManagerFile(pathToXml);
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel();
        }

        private string GetXmlConfigFilePath()
        {
            string xml = $@"
<configuration> 
    <system.serviceModel>
        <bindings>
            <netTcpBinding>
                <binding name=""testBinding""/>
            </netTcpBinding>
        </bindings>
        <services>
            <service name=""{typeof(SomeService).FullName}"">
                <endpoint address=""net.tcp://localhost:6687/netTcp.svc/Buffered""
                          name=""SomeEndpoint""
                          binding=""netTcpBinding""
                          bindingConfiguration=""testBinding""
                          contract=""{typeof(ISomeService).FullName}"" />
            </service>
        </services>
    </system.serviceModel>
</configuration>";

            var fs = TemporaryFileStream.Create(xml);

            return fs.Name;
        }
    }
}
