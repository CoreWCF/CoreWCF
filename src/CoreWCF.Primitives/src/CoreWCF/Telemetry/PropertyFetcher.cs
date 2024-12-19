// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Reflection;

namespace CoreWCF.Telemetry;

/// <summary>
/// PropertyFetcher fetches a property from an object.
/// </summary>
/// <typeparam name="T">The type of the property being fetched.</typeparam>
#pragma warning disable CA1812
internal sealed class PropertyFetcher<T>
#pragma warning restore CA1812
{
    private readonly string propertyName;
    private PropertyFetch innerFetcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyFetcher{T}"/> class.
    /// </summary>
    /// <param name="propertyName">Property name to fetch.</param>
    public PropertyFetcher(string propertyName)
    {
        this.propertyName = propertyName;
    }

    /// <summary>
    /// Fetch the property from the object.
    /// </summary>
    /// <param name="obj">Object to be fetched.</param>
    /// <returns>Property fetched.</returns>
    public T Fetch(object obj)
    {
        return !this.TryFetch(obj, out var value)
            ? throw new ArgumentException("Supplied object was null or did not match the expected type.", nameof(obj))
            : value;
    }

    /// <summary>
    /// Try to fetch the property from the object.
    /// </summary>
    /// <param name="obj">Object to be fetched.</param>
    /// <param name="value">Fetched value.</param>
    /// <returns><see langword="true"/> if the property was fetched.</returns>
    public bool TryFetch(object obj, out T value)
    {
        if (obj == null)
        {
            value = default;
            return false;
        }

        if (this.innerFetcher == null)
        {
            var type = obj.GetType().GetTypeInfo();
            var property = type.DeclaredProperties.FirstOrDefault(p => string.Equals(p.Name, this.propertyName, StringComparison.OrdinalIgnoreCase)) ?? type.GetProperty(this.propertyName);
            this.innerFetcher = PropertyFetch.FetcherForProperty(property);
        }

        return this.innerFetcher.TryFetch(obj, out value);
    }

    // see https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/DiagnosticSourceEventSource.cs
    private class PropertyFetch
    {
        /// <summary>
        /// Create a property fetcher from a .NET Reflection PropertyInfo class that
        /// represents a property of a particular type.
        /// </summary>
        public static PropertyFetch FetcherForProperty(PropertyInfo propertyInfo)
        {
            if (propertyInfo == null || !typeof(T).IsAssignableFrom(propertyInfo.PropertyType))
            {
                // returns null on any fetch.
                return new PropertyFetch();
            }

            var typedPropertyFetcher = typeof(TypedPropertyFetch<,>);
            var instantiatedTypedPropertyFetcher = typedPropertyFetcher.MakeGenericType(
                typeof(T), propertyInfo.DeclaringType, propertyInfo.PropertyType);
            return (PropertyFetch)Activator.CreateInstance(instantiatedTypedPropertyFetcher, propertyInfo);
        }

        public virtual bool TryFetch(object obj, out T value)
        {
            value = default;
            return false;
        }

#pragma warning disable CA1812
        private sealed class TypedPropertyFetch<TDeclaredObject, TDeclaredProperty> : PropertyFetch
#pragma warning restore CA1812
            where TDeclaredProperty : T
        {
            private readonly Func<TDeclaredObject, TDeclaredProperty> propertyFetch;

            public TypedPropertyFetch(PropertyInfo property)
            {
#if NET
                this.propertyFetch = property.GetMethod.CreateDelegate<Func<TDeclaredObject, TDeclaredProperty>>();
#else
                this.propertyFetch = (Func<TDeclaredObject, TDeclaredProperty>)property.GetMethod.CreateDelegate(typeof(Func<TDeclaredObject, TDeclaredProperty>));
#endif
            }

            public override bool TryFetch(object obj, out T value)
            {
                if (obj is TDeclaredObject o)
                {
                    value = this.propertyFetch(o);
                    return true;
                }

                value = default;
                return false;
            }
        }
    }
}

