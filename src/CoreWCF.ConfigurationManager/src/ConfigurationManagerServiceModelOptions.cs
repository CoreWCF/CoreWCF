// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace CoreWCF.Configuration
{
    internal class ConfigurationManagerServiceModelOptions : IConfigureNamedOptions<ServiceModelOptions>, IDisposable
    {
        private readonly IContractResolver _mapper;
        private readonly Lazy<ServiceModelSectionGroup> _section;
        private readonly WrappedConfigurationFile _file;
        private readonly IServiceProvider _serviceBuilder;
        private readonly IConfigurationHolder _holder;

        public ConfigurationManagerServiceModelOptions(IServiceProvider builder, string path, bool isOptional)
        {
            _mapper = builder.GetRequiredService<IContractResolver>();
            _holder = builder.GetRequiredService<IConfigurationHolder>();
            _file = new WrappedConfigurationFile(path);

            _section = new Lazy<ServiceModelSectionGroup>(() =>
            {
                var configuration = ConfigurationManager.OpenMappedMachineConfiguration(new ConfigurationFileMap(_file.ConfigPath));
                var section = ServiceModelSectionGroup.GetSectionGroup(configuration);

                if (section is null && !isOptional)
                {
                    throw new ServiceModelConfigurationException("Section not found");
                }

                return section;
            }, true);
        }

        public void Dispose() => _file.Dispose();

        public void Configure(ServiceModelOptions options) => Configure(ServiceModelDefaults.DefaultName, options);

        public void Configure(string name, ServiceModelOptions options)
        {
            Configure(name, options, _section.Value);
        }

        private void Configure(string name, ServiceModelOptions options, ServiceModelSectionGroup group)
        {
            if (group is null)
            {
                return;
            }

            // todo implement
            if (string.Equals(ServiceModelDefaults.DefaultName, name, StringComparison.Ordinal))
            {
                //Add(options, group.Client?.Endpoints);
                Add(options, group.Bindings.BasicHttpBinding.Bindings);
                Add(options, group.Bindings.NetTcpBinding.Bindings);
            }
            else
            {
                //var service = group.Services.Services.Cast<ServiceElement>().FirstOrDefault(e => e.Name == name);

                //if (service != null)
                //{
                //    Add(options, service.Endpoints);
                //}
            }
        }

        private void Add(ServiceModelOptions options, IEnumerable endpoints)
        {
            if (endpoints is null)
            {
                return;
            }

            foreach (var endpoint in endpoints.OfType<StandardBindingElement>())
            {
                var binding = endpoint.CreateBinding();
                _holder.AddBinding(binding);
            }


            // todo implement
            //foreach (var endpoint in endpoints.OfType<IEndpoint>())
            //{
            //    options.Services.Add(_mapper.ResolveContract(endpoint.Contract), o =>
            //    {
            //        o.Endpoint = new EndpointAddress(endpoint.Address);

            //        if (!string.IsNullOrEmpty(endpoint.Binding) || !string.IsNullOrEmpty(endpoint.BindingConfiguration))
            //        {
            //            o.Binding = ConfigLoader.LookupBinding(endpoint.Binding, endpoint.BindingConfiguration, ConfigurationHelpers.GetEvaluationContext(endpoint));
            //        }
            //    });
            //}
        }
    }
}
