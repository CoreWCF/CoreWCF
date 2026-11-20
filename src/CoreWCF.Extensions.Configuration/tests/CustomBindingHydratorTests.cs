// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text;
using CoreWCF.Channels;
using CoreWCF.Extensions.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CoreWCF.Extensions.Configuration.Tests
{
    /// <summary>
    /// CustomBinding is the case ConfigurationBinder cannot express on its own: an ordered collection of
    /// polymorphic BindingElements, each needing its own concrete type chosen at bind time.
    /// </summary>
    public class CustomBindingHydratorTests
    {
        private static IConfigurationSection Section(Dictionary<string, string> data) =>
            new ConfigurationBuilder().AddInMemoryCollection(data).Build().GetSection("Binding");

        [Fact]
        public void CustomBinding_HydratesPolymorphicElementsInOrder()
        {
            IConfigurationSection section = Section(new Dictionary<string, string>
            {
                ["Binding:Type"] = "CoreWCF.Channels.CustomBinding, CoreWCF.Primitives",
                ["Binding:Elements:0:Type"] = "CoreWCF.Channels.TextMessageEncodingBindingElement, CoreWCF.Primitives",
                ["Binding:Elements:0:MessageVersion"] = "Soap12WSAddressing10",
                ["Binding:Elements:0:WriteEncoding"] = "utf-8",
                ["Binding:Elements:0:MaxReadPoolSize"] = "128",
                ["Binding:Elements:1:Type"] = "CoreWCF.Channels.HttpTransportBindingElement, CoreWCF.Http",
                ["Binding:Elements:1:MaxReceivedMessageSize"] = "1048576",
            });

            var binding = Assert.IsType<CustomBinding>(new BindingHydrator().CreateBinding(section));

            Assert.Equal(2, binding.Elements.Count);

            var encoding = Assert.IsType<TextMessageEncodingBindingElement>(binding.Elements[0]);
            Assert.Same(MessageVersion.Soap12WSAddressing10, encoding.MessageVersion);
            Assert.Equal(Encoding.UTF8.WebName, encoding.WriteEncoding.WebName);
            Assert.Equal(128, encoding.MaxReadPoolSize);

            var transport = Assert.IsType<HttpTransportBindingElement>(binding.Elements[1]);
            Assert.Equal(1048576, transport.MaxReceivedMessageSize);

            // Order is what makes a channel stack work, so it has to survive the round trip.
            Assert.Equal("http", binding.Scheme);
        }

        [Fact]
        public void ElementWithoutDiscriminator_IsReported()
        {
            IConfigurationSection section = Section(new Dictionary<string, string>
            {
                ["Binding:Type"] = "CoreWCF.Channels.CustomBinding, CoreWCF.Primitives",
                ["Binding:Elements:0:MaxReadPoolSize"] = "128",
            });

            BindingConfigurationException exception = Assert.Throws<BindingConfigurationException>(
                () => new BindingHydrator().CreateBinding(section));

            Assert.Contains("'Type'", exception.Message);
            Assert.Contains("BindingElement", exception.Message);
        }

        [Fact]
        public void ElementResolvingToTheWrongBaseType_IsReported()
        {
            IConfigurationSection section = Section(new Dictionary<string, string>
            {
                ["Binding:Type"] = "CoreWCF.Channels.CustomBinding, CoreWCF.Primitives",
                ["Binding:Elements:0:Type"] = "CoreWCF.NetTcpBinding, CoreWCF.NetTcp",
            });

            BindingConfigurationException exception = Assert.Throws<BindingConfigurationException>(
                () => new BindingHydrator().CreateBinding(section));

            Assert.Contains("not a BindingElement", exception.Message);
        }
    }
}
