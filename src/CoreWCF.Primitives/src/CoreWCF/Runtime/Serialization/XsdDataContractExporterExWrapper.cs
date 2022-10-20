// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;
using System.Xml.Schema;

namespace CoreWCF.Runtime.Serialization
{
    internal class XsdDataContractExporterExWrapper : IXsdDataContractExporter
    {
        private readonly XsdDataContractExporterEx _xsdDataContractExporter;

        public XsdDataContractExporterExWrapper()
        {
            _xsdDataContractExporter = new XsdDataContractExporterEx();
        }

        public XsdDataContractExporterExWrapper(XmlSchemaSet xmlSchemaSet)
        {
            _xsdDataContractExporter = new XsdDataContractExporterEx(xmlSchemaSet);
        }

        public void Export(Type type) => _xsdDataContractExporter.Export(type);
        public XmlQualifiedName GetRootElementName(Type type) => _xsdDataContractExporter.GetRootElementName(type);
        public XmlSchemaType GetSchemaType(Type type) => _xsdDataContractExporter.GetSchemaType(type);
        public XmlQualifiedName GetSchemaTypeName(Type type) => _xsdDataContractExporter.GetSchemaTypeName(type);
    }
}
