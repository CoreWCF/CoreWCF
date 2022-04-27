// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    internal sealed class XmlElementBackedAddressHeader : AddressHeader
    {
        private readonly XmlElement _header;

        public XmlElementBackedAddressHeader(XmlDictionaryReader reader)
        {
            Name = reader.LocalName;
            Namespace = reader.NamespaceURI;
            using (XmlReader subTreeReader = reader.ReadSubtree())
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(subTreeReader);
                _header = doc.DocumentElement;
            }
        }

        public override string Name { get; }
        public override string Namespace { get; }

        protected override void OnWriteAddressHeaderContents(XmlDictionaryWriter writer)
        {
            _header.WriteTo(writer);
        }
    }
}
