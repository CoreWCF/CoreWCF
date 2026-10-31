// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using CoreWCF.Channels;

namespace CoreWCF.Extensions.Configuration
{
    /// <summary>
    /// Resolves every type named in a service model configuration section: bindings, binding elements, service
    /// implementations and contracts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A type is named by its
    /// <see href="https://learn.microsoft.com/dotnet/api/system.type.assemblyqualifiedname">assembly qualified
    /// name</see>: <c>"CoreWCF.NetTcpBinding, CoreWCF.NetTcp"</c>. Short names are not accepted, and neither are
    /// bare full names.
    /// </para>
    /// <para>
    /// The reason is in this repository rather than in theory. CoreWCF ships client and server halves of the queue
    /// transports side by side as deliberate homonyms: <c>CoreWCF.Channels.KafkaBinding</c> in
    /// <c>CoreWCF.Kafka</c> against <c>CoreWCF.ServiceModel.Channels.KafkaBinding</c> in
    /// <c>CoreWCF.Kafka.Client</c>, and the same for <c>RabbitMqBinding</c> and
    /// <c>RabbitMqTransportBindingElement</c>. Resolving <c>"KafkaBinding"</c> by short name picks whichever
    /// assembly was scanned last. Namespaces already disambiguate the two - client types live under
    /// <c>CoreWCF.ServiceModel.*</c>, mirroring <c>System.ServiceModel.*</c> - so the full name is the smallest
    /// unambiguous key.
    /// </para>
    /// <para>
    /// Naming the assembly as well is what makes resolution deterministic. An assembly that is not loaded yet
    /// cannot be searched, so a name resolved by scanning loaded assemblies answers according to whatever the
    /// application happened to touch first: right on the machine where the configuration was written, wrong
    /// elsewhere. Transports load lazily, and so does a class library holding service implementations. An assembly
    /// qualified name loads its assembly instead of waiting for something else to.
    /// </para>
    /// <para>
    /// The same rule covers services and contracts as well as bindings, so that a configuration file has one
    /// convention rather than one per kind of type. <see cref="Add(string, Type)"/> registers a shorter name of
    /// the host's choosing where the assembly qualified form would be repetitive.
    /// </para>
    /// </remarks>
    public sealed class ServiceModelTypeRegistry
    {
        private readonly Dictionary<string, Type> _registered = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Type> _resolved = new Dictionary<string, Type>(StringComparer.Ordinal);

        /// <summary>
        /// Registers <paramref name="type"/> under its full name, so configuration can name it without the
        /// assembly.
        /// </summary>
        public ServiceModelTypeRegistry Add(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            return Add(type.FullName, type);
        }

        /// <summary>
        /// Registers <paramref name="type"/> under <paramref name="name"/>.
        /// </summary>
        public ServiceModelTypeRegistry Add(string name, Type type)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("A name is required.", nameof(name));
            }

            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (_registered.TryGetValue(name, out Type existing) && existing != type)
            {
                throw new BindingConfigurationException(
                    $"'{name}' is already registered for '{existing.AssemblyQualifiedName}' and cannot be " +
                    $"re-registered for '{type.AssemblyQualifiedName}'.");
            }

            _registered[name] = type;
            return this;
        }

        /// <summary>
        /// Resolves <paramref name="name"/> to a type.
        /// </summary>
        public Type Resolve(string name, string configurationPath)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new BindingConfigurationException(
                    $"A type name is required (configuration path '{configurationPath}').");
            }

            if (_registered.TryGetValue(name, out Type type))
            {
                return type;
            }

            if (_resolved.TryGetValue(name, out type))
            {
                return type;
            }

            type = Type.GetType(name, throwOnError: false, ignoreCase: false);

            if (type == null)
            {
                throw new BindingConfigurationException(
                    $"'{name}' did not resolve to a type (configuration path '{configurationPath}'). Types are " +
                    $"named by assembly qualified name, for example " +
                    $"\"CoreWCF.NetTcpBinding, CoreWCF.NetTcp\"{RegisteredNames()}.");
            }

            _resolved[name] = type;
            return type;
        }

        /// <summary>
        /// Resolves <paramref name="name"/> to a type assignable to <paramref name="baseType"/>.
        /// </summary>
        public Type Resolve(Type baseType, string name, string configurationPath)
        {
            if (baseType == null)
            {
                throw new ArgumentNullException(nameof(baseType));
            }

            Type type = Resolve(name, configurationPath);

            if (!baseType.IsAssignableFrom(type))
            {
                throw new BindingConfigurationException(
                    $"'{name}' resolves to '{type.FullName}', which is not a {baseType.Name} " +
                    $"(configuration path '{configurationPath}').");
            }

            return type;
        }

        /// <summary>
        /// Resolves <paramref name="name"/> to a <see cref="Binding"/> type.
        /// </summary>
        public Type ResolveBinding(string name, string configurationPath) =>
            Resolve(typeof(Binding), name, configurationPath);

        /// <summary>
        /// Resolves <paramref name="name"/> to a <see cref="BindingElement"/> type.
        /// </summary>
        public Type ResolveBindingElement(string name, string configurationPath) =>
            Resolve(typeof(BindingElement), name, configurationPath);

        private string RegisteredNames()
        {
            if (_registered.Count == 0)
            {
                return string.Empty;
            }

            string[] names = _registered.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            return $", or one of the registered names: {string.Join(", ", names)}";
        }
    }
}
