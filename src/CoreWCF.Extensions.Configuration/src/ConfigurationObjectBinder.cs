// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace CoreWCF.Extensions.Configuration
{
    /// <summary>
    /// Binds a configuration section onto an existing object graph.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hydration cannot be layered on <c>ConfigurationBinder</c>, for two reasons it has no extensibility point for.
    /// A binding and its elements are polymorphic, and <c>ConfigurationBinder</c> cannot pick a concrete type from a
    /// discriminator, so neither the binding itself nor the contents of <c>CustomBinding.Elements</c> can be created.
    /// And several types that appear in ordinary binding configuration have no <see cref="System.ComponentModel.TypeConverter"/>
    /// at all - <c>MessageVersion</c>, <c>EnvelopeVersion</c>, <c>SecurityAlgorithmSuite</c>, <c>MessageSecurityVersion</c> -
    /// so their values cannot be converted from a string. See <see cref="ConfigurationValueConverter"/>.
    /// </para>
    /// <para>
    /// Driving the traversal from the configuration keys rather than from the target's properties buys two further
    /// things. An unknown key becomes an error naming its configuration path, instead of a value that silently does
    /// nothing. And a property the configuration does not mention is genuinely untouched: <c>ConfigurationBinder</c>
    /// round-trips every settable property through its getter and setter whether or not it is configured, which
    /// matters here because binding accessors are not pure - <c>ReaderQuotas</c> copies the incoming value over the
    /// encoder's instance, and <c>Security</c> rejects null and replaces the whole sub-object.
    /// </para>
    /// </remarks>
    internal sealed class ConfigurationObjectBinder
    {
        private const BindingFlags PropertyFlags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase;

        private readonly ServiceModelTypeRegistry _registry;
        private readonly string _discriminatorKey;

        public ConfigurationObjectBinder(ServiceModelTypeRegistry registry, string discriminatorKey)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _discriminatorKey = discriminatorKey ?? throw new ArgumentNullException(nameof(discriminatorKey));
        }

        public void Bind(object instance, IConfiguration section)
        {
            foreach (IConfigurationSection child in section.GetChildren())
            {
                if (string.Equals(child.Key, _discriminatorKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                PropertyInfo property = FindProperty(instance.GetType(), child.Key);
                if (property == null)
                {
                    throw new BindingConfigurationException(
                        $"'{instance.GetType().Name}' has no property named '{child.Key}' " +
                        $"(configuration path '{child.Path}').");
                }

                BindProperty(instance, property, child);
            }
        }

        private void BindProperty(object instance, PropertyInfo property, IConfigurationSection section)
        {
            Type propertyType = property.PropertyType;

            if (section.Value != null)
            {
                RequireSetter(instance, property, section);
                property.SetValue(instance, ConfigurationValueConverter.Convert(section.Value, propertyType, section.Path));
                return;
            }

            if (TryGetCollectionItemType(propertyType, out Type itemType))
            {
                object collection = property.GetValue(instance);
                if (collection == null)
                {
                    throw new BindingConfigurationException(
                        $"'{instance.GetType().Name}.{property.Name}' is null, so its items cannot be populated " +
                        $"(configuration path '{section.Path}').");
                }

                BindCollection(collection, itemType, section);
                return;
            }

            string typeName = section[_discriminatorKey];
            if (typeName != null || propertyType.IsAbstract)
            {
                // The configuration chose the concrete type, so a fresh instance has to replace whatever is there.
                RequireSetter(instance, property, section);
                object replacement = CreateInstance(propertyType, typeName, section);
                Bind(replacement, section);
                property.SetValue(instance, replacement);
                return;
            }

            // Bind into the instance the binding already created. Bindings carry meaningful defaults on these
            // sub-objects (security, reader quotas), and replacing them would silently discard those defaults.
            object existing = property.GetValue(instance);
            if (existing != null)
            {
                Bind(existing, section);
                return;
            }

            RequireSetter(instance, property, section);
            object created = CreateInstance(propertyType, typeName: null, section: section);
            Bind(created, section);
            property.SetValue(instance, created);
        }

        private void BindCollection(object collection, Type itemType, IConfigurationSection section)
        {
            MethodInfo add = typeof(ICollection<>).MakeGenericType(itemType).GetMethod("Add");

            foreach (IConfigurationSection child in section.GetChildren())
            {
                object item = CreateInstance(itemType, child[_discriminatorKey], child);
                Bind(item, child);
                add.Invoke(collection, new[] { item });
            }
        }

        private object CreateInstance(Type declaredType, string typeName, IConfigurationSection section)
        {
            Type concreteType = declaredType;

            if (typeName != null)
            {
                concreteType = _registry.Resolve(declaredType, typeName, section.Path);
            }
            else if (declaredType.IsAbstract)
            {
                throw new BindingConfigurationException(
                    $"A '{_discriminatorKey}' value is required to choose a concrete {declaredType.Name} " +
                    $"(configuration path '{section.Path}').");
            }

            if (concreteType.IsAbstract)
            {
                throw new BindingConfigurationException(
                    $"'{concreteType.FullName}' is abstract and cannot be created from configuration " +
                    $"(configuration path '{section.Path}').");
            }

            if (concreteType.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new BindingConfigurationException(
                    $"'{concreteType.FullName}' has no parameterless constructor and cannot be created from " +
                    $"configuration (configuration path '{section.Path}').");
            }

            return Activator.CreateInstance(concreteType);
        }

        private static void RequireSetter(object instance, PropertyInfo property, IConfigurationSection section)
        {
            if (property.SetMethod == null || !property.SetMethod.IsPublic)
            {
                throw new BindingConfigurationException(
                    $"'{instance.GetType().Name}.{property.Name}' is read-only and cannot be set from configuration " +
                    $"(configuration path '{section.Path}').");
            }
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            try
            {
                return type.GetProperty(name, PropertyFlags);
            }
            catch (AmbiguousMatchException)
            {
                // A property re-declared with 'new' in a derived type; the most derived one wins.
                for (Type current = type; current != null; current = current.BaseType)
                {
                    PropertyInfo property = current.GetProperty(name, PropertyFlags | BindingFlags.DeclaredOnly);
                    if (property != null)
                    {
                        return property;
                    }
                }

                return null;
            }
        }

        private static bool TryGetCollectionItemType(Type type, out Type itemType)
        {
            itemType = null;

            if (type == typeof(string))
            {
                return false;
            }

            foreach (Type contract in type.GetInterfaces())
            {
                if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICollection<>))
                {
                    itemType = contract.GetGenericArguments()[0];
                    return true;
                }
            }

            return false;
        }
    }
}
