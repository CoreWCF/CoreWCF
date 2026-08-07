// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Channels;
using CoreWCF.Extensions.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CoreWCF.Extensions.Configuration.Tests.Server
{
    /// <summary>Stands in for <c>CoreWCF.Channels.KafkaBinding</c> in <c>CoreWCF.Kafka</c>.</summary>
    public sealed class EchoBinding : Binding
    {
        public override string Scheme => "server";

        public override BindingElementCollection CreateBindingElements() => new BindingElementCollection();
    }
}

namespace CoreWCF.Extensions.Configuration.Tests.Client
{
    /// <summary>Stands in for <c>CoreWCF.ServiceModel.Channels.KafkaBinding</c> in <c>CoreWCF.Kafka.Client</c>.</summary>
    public sealed class EchoBinding : Binding
    {
        public override string Scheme => "client";

        public override BindingElementCollection CreateBindingElements() => new BindingElementCollection();
    }
}

namespace CoreWCF.Extensions.Configuration.Tests
{
    /// <summary>
    /// CoreWCF ships homonym client and server bindings today: <c>CoreWCF.Channels.KafkaBinding</c> against
    /// <c>CoreWCF.ServiceModel.Channels.KafkaBinding</c>, and the same for <c>RabbitMqBinding</c> and
    /// <c>RabbitMqTransportBindingElement</c>. Resolving a short name would pick one of them arbitrarily, so these
    /// pin that a short name cannot resolve at all and that the two are distinguishable.
    /// </summary>
    public class HomonymBindingTests
    {
        private const string Assembly = "CoreWCF.Extensions.Configuration.Tests";

        private static IConfigurationSection Section(string typeName) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { ["Binding:Type"] = typeName })
                .Build()
                .GetSection("Binding");

        [Fact]
        public void SharedShortName_DoesNotResolve()
        {
            Assert.Throws<BindingConfigurationException>(
                () => new BindingHydrator().CreateBinding(Section("EchoBinding")));
        }

        [Fact]
        public void AssemblyQualifiedNames_TellTheHomonymsApart()
        {
            Binding server = new BindingHydrator()
                .CreateBinding(Section($"{typeof(Server.EchoBinding).FullName}, {Assembly}"));
            Binding client = new BindingHydrator()
                .CreateBinding(Section($"{typeof(Client.EchoBinding).FullName}, {Assembly}"));

            Assert.IsType<Server.EchoBinding>(server);
            Assert.IsType<Client.EchoBinding>(client);
            Assert.Equal("server", server.Scheme);
            Assert.Equal("client", client.Scheme);
        }

        [Fact]
        public void RegisteringBothUnderOneName_IsRejectedRatherThanLastWriterWins()
        {
            var registry = new ServiceModelTypeRegistry().Add("echo", typeof(Server.EchoBinding));

            BindingConfigurationException exception = Assert.Throws<BindingConfigurationException>(
                () => registry.Add("echo", typeof(Client.EchoBinding)));

            Assert.Contains("already registered", exception.Message);
        }

        [Fact]
        public void RegisteringBothUnderTheirFullNames_Coexists()
        {
            var registry = new ServiceModelTypeRegistry()
                .Add(typeof(Server.EchoBinding))
                .Add(typeof(Client.EchoBinding));

            var options = new BindingHydratorOptions { Registry = registry };
            var hydrator = new BindingHydrator(options);

            Assert.IsType<Server.EchoBinding>(hydrator.CreateBinding(Section(typeof(Server.EchoBinding).FullName)));
            Assert.IsType<Client.EchoBinding>(hydrator.CreateBinding(Section(typeof(Client.EchoBinding).FullName)));
        }
    }
}
