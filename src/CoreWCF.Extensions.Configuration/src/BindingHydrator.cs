// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Channels;
using Microsoft.Extensions.Configuration;

namespace CoreWCF.Extensions.Configuration
{
    /// <summary>
    /// Creates <see cref="Binding"/> instances from an <see cref="IConfiguration"/> source.
    /// </summary>
    /// <remarks>
    /// A binding section names its concrete type with a discriminator key (<c>Type</c> by default); every other key
    /// is a property on that binding.
    /// <code>
    /// "Bindings": {
    ///   "internal": {
    ///     "Type": "NetTcpBinding",
    ///     "MaxReceivedMessageSize": 2097152,
    ///     "Security": { "Mode": "Transport" }
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public sealed class BindingHydrator
    {
        private readonly ServiceModelTypeRegistry _registry;
        private readonly string _discriminatorKey;
        private readonly ConfigurationObjectBinder _binder;

        public BindingHydrator()
            : this(new BindingHydratorOptions())
        {
        }

        public BindingHydrator(BindingHydratorOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _registry = options.Registry ?? throw new ArgumentException(
                $"{nameof(BindingHydratorOptions)}.{nameof(BindingHydratorOptions.Registry)} is required.",
                nameof(options));

            if (string.IsNullOrEmpty(options.DiscriminatorKey))
            {
                throw new ArgumentException(
                    $"{nameof(BindingHydratorOptions)}.{nameof(BindingHydratorOptions.DiscriminatorKey)} is required.",
                    nameof(options));
            }

            _discriminatorKey = options.DiscriminatorKey;
            _binder = new ConfigurationObjectBinder(_registry, _discriminatorKey);
        }

        /// <summary>
        /// Creates the single binding described by <paramref name="section"/>.
        /// </summary>
        public Binding CreateBinding(IConfigurationSection section)
        {
            if (section == null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            string typeName = section[_discriminatorKey];
            if (typeName == null)
            {
                throw new BindingConfigurationException(
                    $"A '{_discriminatorKey}' value naming the binding type is required " +
                    $"(configuration path '{section.Path}').");
            }

            Type bindingType = _registry.ResolveBinding(typeName, section.Path);
            var binding = (Binding)Activator.CreateInstance(bindingType);
            _binder.Bind(binding, section);
            return binding;
        }

        /// <summary>
        /// Creates every binding declared under <paramref name="section"/>, keyed by its configuration key.
        /// A binding that does not set <see cref="Binding.Name"/> takes its key as its name.
        /// </summary>
        public IDictionary<string, Binding> CreateBindings(IConfiguration section)
        {
            if (section == null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            var bindings = new Dictionary<string, Binding>(StringComparer.OrdinalIgnoreCase);

            foreach (IConfigurationSection child in section.GetChildren())
            {
                Binding binding = CreateBinding(child);

                // Binding.Name falls back to the type name rather than staying null, so ask configuration
                // whether a name was given rather than inspecting the binding.
                if (child["Name"] == null)
                {
                    binding.Name = child.Key;
                }

                bindings[child.Key] = binding;
            }

            return bindings;
        }
    }
}
