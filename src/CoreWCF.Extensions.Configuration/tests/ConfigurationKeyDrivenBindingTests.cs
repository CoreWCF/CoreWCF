// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using CoreWCF.Channels;
using CoreWCF.Extensions.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CoreWCF.Extensions.Configuration.Tests
{
    /// <summary>
    /// A binding that records reads and writes of a settable property, standing in for the binding properties
    /// whose accessors are not pure: <c>ReaderQuotas</c> copies the incoming value over the encoder's own
    /// instance, and <c>Security</c> rejects null and replaces the whole sub-object.
    /// </summary>
    public sealed class AccessorSpyBinding : Binding
    {
        private int _roundTripped;

        public int Reads { get; private set; }

        public int Writes { get; private set; }

        public int Threshold { get; set; }

        public int RoundTripped
        {
            get
            {
                Reads++;
                return _roundTripped;
            }

            set
            {
                Writes++;
                _roundTripped = value;
            }
        }

        public override string Scheme => "spy";

        public override BindingElementCollection CreateBindingElements() => new BindingElementCollection();
    }

    /// <summary>
    /// Pins the behaviour that makes hydration walk the configuration keys rather than hand the binding to
    /// ConfigurationBinder.
    /// </summary>
    public class ConfigurationKeyDrivenBindingTests
    {
        private static IConfigurationSection SpySection() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Binding:Type"] = typeof(AccessorSpyBinding).FullName,
                    ["Binding:Threshold"] = "7",
                })
                .Build()
                .GetSection("Binding");

        [Fact]
        public void Hydration_LeavesUnconfiguredPropertiesUntouched()
        {
            // The registry is how a host avoids repeating an assembly qualified name in configuration.
            var options = new BindingHydratorOptions
            {
                Registry = new ServiceModelTypeRegistry().Add(typeof(AccessorSpyBinding)),
            };

            var binding = (AccessorSpyBinding)new BindingHydrator(options).CreateBinding(SpySection());

            Assert.Equal(7, binding.Threshold);
            Assert.Equal(0, binding.Reads);
            Assert.Equal(0, binding.Writes);
        }

        [Fact]
        public void ConfigurationBinder_RoundTripsEverySettablePropertyThroughItsAccessors()
        {
            // ConfigurationBinder.BindProperty evaluates the property's BindingPoint and, for anything with a
            // public setter, calls SetValue with whatever the getter returned - whether or not the configuration
            // mentions the property. Read-only computed properties are safe because the BindingPoint is lazy, but
            // settable ones are get-then-set on every bind. WCF binding setters are not pure, so hydration drives
            // the traversal from the configuration keys instead.
            var spy = new AccessorSpyBinding();

            SpySection().Bind(spy);

            Assert.Equal(7, spy.Threshold);
            Assert.True(spy.Reads > 0, "Expected ConfigurationBinder to read the unconfigured property.");
            Assert.True(spy.Writes > 0, "Expected ConfigurationBinder to write the unconfigured property back.");
        }
    }
}
