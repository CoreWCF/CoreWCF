// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoreWCF.Configuration
{
    internal class ConfigurationManagerServiceModelOptions : IConfigureNamedOptions<ServiceModelOptions>
    {
        private readonly Lazy<ServiceModelSectionGroup> _section;

        private readonly IConfigurationHolder _holder;

        public ConfigurationManagerServiceModelOptions(IServiceProvider builder, string path)
        {
            _holder = builder.GetRequiredService<IConfigurationHolder>();

            _section = new Lazy<ServiceModelSectionGroup>(() =>
            {
                var assembly = Assembly.GetEntryAssembly();
                var basePath = string.IsNullOrEmpty(assembly?.Location) ? AppContext.BaseDirectory : Path.GetDirectoryName(assembly.Location);

                // hack - in .net core 2.1 on linux directory not correct(on unit tests),
                // for example:
                // /home/vsts/.nuget/packages/microsoft.testplatform.testhost/16.7.1/lib/netcoreapp2.1/
                var isNetCore21 = RuntimeInformation.FrameworkDescription.Contains(".NET Core 4.6");
                if (isNetCore21 && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    basePath = AppContext.BaseDirectory;
                }

                var configMap = new ExeConfigurationFileMap(Path.Combine(basePath, "CoreWCF.machine.config"))
                {
                    ExeConfigFilename = path
                };
                System.Configuration.Configuration configuration = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);
                var section = ServiceModelSectionGroup.GetSectionGroup(configuration);

                if (section is null)
                {
                    throw new ServiceModelConfigurationException("Section not found");
                }

                return section;
            }, true);
        }

        public void Configure(string name, ServiceModelOptions options)
        {
            Configure(options);
        }

        public void Configure(ServiceModelOptions options)
        {
            var configHolder = ParseConfig();
            foreach (var serviceEndPoint in configHolder.Endpoints)
            {
               IXmlConfigEndpoint configEndpoint = configHolder.GetXmlConfigEndpoint(serviceEndPoint);
                options.ConfigureService(configEndpoint.Service, serviceConfig =>
                {
                    serviceConfig.AddServiceEndpoint(configEndpoint.Contract, configEndpoint.Binding, configEndpoint.Address, null);
                });
            }
        }

        private void ReadConfigSection(ServiceModelSectionGroup group)
        {
            if (group is null)
            {
                return;
            }

            AddBinding(group.Bindings?.BasicHttpBinding.Bindings);
            AddBinding(group.Bindings?.NetTcpBinding.Bindings);
            AddBinding(group.Bindings?.NetHttpBinding.Bindings);
            AddBinding(group.Bindings?.WSHttpBinding.Bindings);
            AddEndpoint(group.Services?.Services);
        }

        private void AddEndpoint(IEnumerable endpoints)
        {
            foreach (ServiceElement bindingElement in endpoints.OfType<ServiceElement>())
            {
                string serviceName = bindingElement.Name;

                foreach (ServiceEndpointElement endpoint in bindingElement.Endpoints.OfType<ServiceEndpointElement>())
                {
                    _holder.AddServiceEndpoint(
                        endpoint.Name,
                        serviceName,
                        endpoint.Address,
                        endpoint.Contract,
                        endpoint.Binding,
                        endpoint.BindingConfiguration);
                }
            }
        }

        private void AddBinding(IEnumerable bindings)
        {
            foreach (StandardBindingElement bindingElement in bindings.OfType<StandardBindingElement>())
            {
                Channels.Binding binding = bindingElement.CreateBinding();
                _holder.AddBinding(binding);
            }
        }

        private IConfigurationHolder ParseConfig()
        {
            ReadConfigSection(_section.Value);
            return _holder;
        }


    }
}
