// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Channels;
using Microsoft.Extensions.Configuration;

namespace CoreWCF.Extensions.Configuration
{
    /// <summary>
    /// Reads a service model section into the endpoint definitions it describes.
    /// </summary>
    /// <remarks>
    /// The shape mirrors the <c>&lt;system.serviceModel&gt;</c> section that <c>CoreWCF.ConfigurationManager</c>
    /// reads from XML, so a wcf.config file has a direct equivalent here:
    /// <code>
    /// "ServiceModel": {
    ///   "Bindings": {
    ///     "internal": {
    ///       "Type": "CoreWCF.NetTcpBinding, CoreWCF.NetTcp",
    ///       "Security": { "Mode": "None" }
    ///     }
    ///   },
    ///   "Services": {
    ///     "Contoso.EchoService, Contoso.Services": {
    ///       "Endpoints": [
    ///         {
    ///           "Contract": "Contoso.IEchoService, Contoso.Contracts",
    ///           "Binding": "internal",
    ///           "Address": "net.tcp://localhost:8089/echo"
    ///         }
    ///       ]
    ///     }
    ///   }
    /// }
    /// </code>
    /// An endpoint's <c>Binding</c> is either the name of an entry under <c>Bindings</c> or, when the binding is used
    /// once, an inline binding object.
    /// </remarks>
    public class ServiceModelConfigurationReader
    {
        private readonly BindingHydrator _hydrator;
        private readonly ServiceModelTypeRegistry _registry;

        public ServiceModelConfigurationReader()
            : this(new BindingHydrator(), new ServiceModelTypeRegistry())
        {
        }

        public ServiceModelConfigurationReader(BindingHydrator hydrator, ServiceModelTypeRegistry registry)
        {
            _hydrator = hydrator ?? throw new ArgumentNullException(nameof(hydrator));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// Reads every endpoint declared under <paramref name="serviceModelSection"/>.
        /// </summary>
        public IReadOnlyList<ServiceEndpointDefinition> ReadEndpoints(IConfiguration serviceModelSection)
        {
            if (serviceModelSection == null)
            {
                throw new ArgumentNullException(nameof(serviceModelSection));
            }

            IDictionary<string, Binding> namedBindings =
                _hydrator.CreateBindings(serviceModelSection.GetSection("Bindings"));

            var definitions = new List<ServiceEndpointDefinition>();

            foreach (IConfigurationSection service in serviceModelSection.GetSection("Services").GetChildren())
            {
                Type serviceType = _registry.Resolve(service.Key, service.Path);

                IConfigurationSection endpoints = service.GetSection("Endpoints");
                if (!endpoints.Exists())
                {
                    throw new BindingConfigurationException(
                        $"Service '{service.Key}' declares no endpoints (configuration path '{service.Path}').");
                }

                foreach (IConfigurationSection endpoint in endpoints.GetChildren())
                {
                    definitions.Add(ReadEndpoint(serviceType, endpoint, namedBindings));
                }
            }

            return definitions;
        }

        private ServiceEndpointDefinition ReadEndpoint(
            Type serviceType,
            IConfigurationSection endpoint,
            IDictionary<string, Binding> namedBindings)
        {
            IConfigurationSection contractSection = endpoint.GetSection("Contract");
            Type contract = _registry.Resolve(contractSection.Value, contractSection.Path);

            Binding binding = ResolveBinding(endpoint.GetSection("Binding"), namedBindings);
            Uri address = ReadUri(endpoint.GetSection("Address"), required: true);
            Uri listenUri = ReadUri(endpoint.GetSection("ListenUri"), required: false);

            return new ServiceEndpointDefinition(serviceType, contract, binding, address, listenUri);
        }

        private Binding ResolveBinding(IConfigurationSection binding, IDictionary<string, Binding> namedBindings)
        {
            if (!binding.Exists())
            {
                throw new BindingConfigurationException(
                    $"A binding is required (configuration path '{binding.Path}').");
            }

            // A string names an entry under Bindings; an object declares the binding inline.
            if (binding.Value == null)
            {
                return _hydrator.CreateBinding(binding);
            }

            if (namedBindings.TryGetValue(binding.Value, out Binding named))
            {
                return named;
            }

            throw new BindingConfigurationException(
                $"'{binding.Value}' does not name a binding declared under 'Bindings' " +
                $"(configuration path '{binding.Path}').");
        }

        private static Uri ReadUri(IConfigurationSection section, bool required)
        {
            if (string.IsNullOrEmpty(section.Value))
            {
                if (required)
                {
                    throw new BindingConfigurationException(
                        $"An address is required (configuration path '{section.Path}').");
                }

                return null;
            }

            if (!Uri.TryCreate(section.Value, UriKind.RelativeOrAbsolute, out Uri uri))
            {
                throw new BindingConfigurationException(
                    $"'{section.Value}' is not a valid URI (configuration path '{section.Path}').");
            }

            return uri;
        }
    }
}
