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
    public class BindingHydratorTests
    {
        private static IConfigurationSection Section(params (string Key, string Value)[] values)
        {
            var data = new Dictionary<string, string>();
            foreach ((string key, string value) in values)
            {
                data["Binding:" + key] = value;
            }

            return new ConfigurationBuilder().AddInMemoryCollection(data).Build().GetSection("Binding");
        }

        [Fact]
        public void NetTcpBinding_HydratesScalarsAndNestedSecurity()
        {
            IConfigurationSection section = Section(
                ("Type", "CoreWCF.NetTcpBinding, CoreWCF.NetTcp"),
                ("MaxReceivedMessageSize", "2097152"),
                ("MaxBufferSize", "65536"),
                ("ReceiveTimeout", "00:05:00"),
                ("Security:Mode", "Transport"),
                ("Security:Transport:ClientCredentialType", "Certificate"));

            var binding = Assert.IsType<NetTcpBinding>(new BindingHydrator().CreateBinding(section));

            Assert.Equal(2097152, binding.MaxReceivedMessageSize);
            Assert.Equal(65536, binding.MaxBufferSize);
            Assert.Equal(System.TimeSpan.FromMinutes(5), binding.ReceiveTimeout);
            Assert.Equal(SecurityMode.Transport, binding.Security.Mode);
            Assert.Equal(TcpClientCredentialType.Certificate, binding.Security.Transport.ClientCredentialType);
        }

        [Fact]
        public void BasicHttpBinding_HydratesEncodingAndReaderQuotas()
        {
            IConfigurationSection section = Section(
                ("Type", "CoreWCF.BasicHttpBinding, CoreWCF.Http"),
                ("TextEncoding", "utf-8"),
                ("TransferMode", "Streamed"),
                ("ReaderQuotas:MaxStringContentLength", "32768"),
                ("ReaderQuotas:MaxArrayLength", "65536"));

            var binding = Assert.IsType<BasicHttpBinding>(new BindingHydrator().CreateBinding(section));

            Assert.Equal(Encoding.UTF8.WebName, binding.TextEncoding.WebName);
            Assert.Equal(TransferMode.Streamed, binding.TransferMode);
            Assert.Equal(32768, binding.ReaderQuotas.MaxStringContentLength);
            Assert.Equal(65536, binding.ReaderQuotas.MaxArrayLength);
        }

        [Fact]
        public void BindingDefaults_SurviveAPartiallyConfiguredSubObject()
        {
            // Only Mode is configured; the rest of NetTcpSecurity must keep the defaults NetTcpBinding gave it.
            var expected = new NetTcpBinding();

            IConfigurationSection section = Section(
                ("Type", "CoreWCF.NetTcpBinding, CoreWCF.NetTcp"),
                ("Security:Mode", "None"));

            var binding = (NetTcpBinding)new BindingHydrator().CreateBinding(section);

            Assert.Equal(SecurityMode.None, binding.Security.Mode);
            Assert.Equal(expected.Security.Transport.ProtectionLevel, binding.Security.Transport.ProtectionLevel);
            Assert.Equal(expected.Security.Transport.ClientCredentialType, binding.Security.Transport.ClientCredentialType);
        }

        [Fact]
        public void CreateBindings_NamesEachBindingFromItsKey()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Bindings:internal:Type"] = "CoreWCF.NetTcpBinding, CoreWCF.NetTcp",
                    ["Bindings:public:Type"] = "CoreWCF.BasicHttpBinding, CoreWCF.Http",
                    ["Bindings:public:Name"] = "explicitly-named",
                })
                .Build();

            IDictionary<string, Binding> bindings =
                new BindingHydrator().CreateBindings(configuration.GetSection("Bindings"));

            Assert.Equal(2, bindings.Count);
            Assert.Equal("internal", bindings["internal"].Name);
            Assert.Equal("explicitly-named", bindings["public"].Name);
        }

        [Fact]
        public void UnknownKey_ReportsTheConfigurationPath()
        {
            IConfigurationSection section = Section(
                ("Type", "CoreWCF.NetTcpBinding, CoreWCF.NetTcp"),
                ("MaxRecievedMessageSize", "1024"));

            BindingConfigurationException exception = Assert.Throws<BindingConfigurationException>(
                () => new BindingHydrator().CreateBinding(section));

            Assert.Contains("MaxRecievedMessageSize", exception.Message);
            Assert.Contains("Binding:MaxRecievedMessageSize", exception.Message);
        }

        [Fact]
        public void MissingDiscriminator_IsReported()
        {
            IConfigurationSection section = Section(("MaxReceivedMessageSize", "1024"));

            BindingConfigurationException exception = Assert.Throws<BindingConfigurationException>(
                () => new BindingHydrator().CreateBinding(section));

            Assert.Contains("'Type'", exception.Message);
        }

        [Fact]
        public void UnresolvableTypeName_PointsAtTheAssemblyQualifiedForm()
        {
            IConfigurationSection section = Section(("Type", "CoreWCF.NetTcpBindingg, CoreWCF.NetTcp"));

            BindingConfigurationException exception = Assert.Throws<BindingConfigurationException>(
                () => new BindingHydrator().CreateBinding(section));

            Assert.Contains("assembly qualified", exception.Message);
        }

        [Fact]
        public void ShortName_DoesNotResolve()
        {
            // Short names are rejected outright: CoreWCF ships homonym client and server bindings, so a short
            // name cannot identify a type. See ServiceModelTypeRegistry.
            IConfigurationSection section = Section(("Type", "NetTcpBinding"));

            Assert.Throws<BindingConfigurationException>(() => new BindingHydrator().CreateBinding(section));
        }
    }
}
