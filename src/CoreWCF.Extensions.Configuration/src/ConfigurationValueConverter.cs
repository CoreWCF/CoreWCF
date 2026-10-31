// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace CoreWCF.Extensions.Configuration
{
    /// <summary>
    /// Converts a configuration string into the CLR type a binding property expects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three strategies are tried in order. Most values (numbers, enums, <see cref="TimeSpan"/>, <see cref="Uri"/>)
    /// are handled by their <see cref="TypeConverter"/>, which is what <c>ConfigurationBinder</c> would use.
    /// </para>
    /// <para>
    /// The interesting case is the set of types that have no <see cref="TypeConverter"/> at all:
    /// <c>MessageVersion</c>, <c>EnvelopeVersion</c>, <c>SecurityAlgorithmSuite</c> and <c>MessageSecurityVersion</c>.
    /// <c>CoreWCF.ConfigurationManager</c> carries one hand written converter per type for these. They share a trait
    /// that makes the hand written converters unnecessary: their well known values are exposed as public static
    /// members on the type itself (<c>MessageVersion.Soap12WSAddressing10</c>, <c>SecurityAlgorithmSuite.Basic256</c>).
    /// A single static-member lookup therefore replaces the whole set, and keeps working for types added later.
    /// </para>
    /// </remarks>
    internal static class ConfigurationValueConverter
    {
        private const BindingFlags StaticMemberFlags =
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase;

        public static object Convert(string value, Type targetType, string configurationPath)
        {
            if (targetType == typeof(string))
            {
                return value;
            }

            Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (underlyingType != targetType && string.IsNullOrEmpty(value))
            {
                return null;
            }

            if (underlyingType == typeof(Encoding) && TryConvertEncoding(value, out object encoding))
            {
                return encoding;
            }

            TypeConverter converter = TypeDescriptor.GetConverter(underlyingType);
            if (converter != null && converter.CanConvertFrom(typeof(string)))
            {
                try
                {
                    return converter.ConvertFromString(null, CultureInfo.InvariantCulture, value);
                }
                catch (Exception ex)
                {
                    throw new BindingConfigurationException(
                        $"Failed to convert '{value}' to {underlyingType.Name} (configuration path '{configurationPath}').",
                        ex);
                }
            }

            if (TryResolveWellKnownValue(underlyingType, value, out object wellKnownValue))
            {
                return wellKnownValue;
            }

            throw new BindingConfigurationException(
                $"No conversion from '{value}' to {underlyingType.Name} is available, and {underlyingType.Name} " +
                $"declares no public static member with that name (configuration path '{configurationPath}').");
        }

        private static bool TryConvertEncoding(string value, out object encoding)
        {
            try
            {
                encoding = Encoding.GetEncoding(value);
                return true;
            }
            catch (ArgumentException)
            {
                // Not a code page name; a static member lookup may still resolve it (for example "UTF8").
                encoding = null;
                return false;
            }
        }

        /// <summary>
        /// Resolves a public static property or field of <paramref name="type"/> named <paramref name="name"/>
        /// whose value is assignable to <paramref name="type"/>.
        /// </summary>
        private static bool TryResolveWellKnownValue(Type type, string name, out object value)
        {
            value = null;

            PropertyInfo property = type.GetProperty(name, StaticMemberFlags);
            if (property != null && property.CanRead && type.IsAssignableFrom(property.PropertyType))
            {
                value = property.GetValue(null);
                return value != null;
            }

            FieldInfo field = type.GetField(name, StaticMemberFlags);
            if (field != null && type.IsAssignableFrom(field.FieldType))
            {
                value = field.GetValue(null);
                return value != null;
            }

            return false;
        }
    }
}
