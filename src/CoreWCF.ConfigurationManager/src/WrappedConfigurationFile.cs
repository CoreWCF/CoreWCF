// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace CoreWCF.Configuration
{
    internal class WrappedConfigurationFile
    {
        private static readonly (string name, Type type)[] KnownSections = new[]
{
           // ("services", typeof(ServicesSection)),
            ("bindings", typeof(BindingsSection)),
           // ("extensions", typeof(ExtensionsSection)),
           // ("client", typeof(ClientSection)), needed ??
        };

        private readonly Lazy<string> _config;
        public string ConfigPath => _config.Value;

        public WrappedConfigurationFile(string path)
        {
            _config = new Lazy<string>(() => WrapFile(XDocument.Load(path)), true);
        }

        private static string WrapFile(XDocument original)
        {
            //var configPath = Path.GetTempFileName();
            var configPath = @"d:\tmp.config";
            var serviceModel = original.Descendants("system.serviceModel");

            var sections = KnownSections.Select(info => new XElement("section", new XAttribute("name", info.name), new XAttribute("type", info.type.AssemblyQualifiedName)));

            var doc = new XDocument(
                new XElement("configuration",
                    new XElement("configSections",
                        new XElement("sectionGroup", new XAttribute("name", "system.serviceModel"), new XAttribute("type", typeof(ServiceModelSectionGroup).AssemblyQualifiedName),
                            sections)),
                    serviceModel));

            using (var writer = XmlWriter.Create(configPath, new XmlWriterSettings { Indent = true }))
            {
                doc.WriteTo(writer);
            }

            return configPath;
        }

        internal void Dispose()
        {
            if (_config.IsValueCreated)
            {
                File.Delete(_config.Value);
            }
        }
    }
}
