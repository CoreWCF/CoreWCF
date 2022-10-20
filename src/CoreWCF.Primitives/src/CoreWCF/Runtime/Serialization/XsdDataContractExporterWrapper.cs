// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Schema;

namespace CoreWCF.Runtime.Serialization
{
    internal class XsdDataContractExporterWrapper : IXsdDataContractExporter
    {
        private readonly XsdDataContractExporter _xsdDataContractExporter;

        public XsdDataContractExporterWrapper()
        {
            _xsdDataContractExporter = new XsdDataContractExporter();
        }

        public XsdDataContractExporterWrapper(XmlSchemaSet xmlSchemaSet)
        {
            _xsdDataContractExporter = new XsdDataContractExporter(xmlSchemaSet);
        }

        public void Export(Type type) => _xsdDataContractExporter.Export(type);

        public XmlQualifiedName GetRootElementName(Type type) => _xsdDataContractExporter.GetRootElementName(type);

        public XmlSchemaType GetSchemaType(Type type) => _xsdDataContractExporter.GetSchemaType(type);

        public XmlQualifiedName GetSchemaTypeName(Type type) => _xsdDataContractExporter.GetSchemaTypeName(type);
    }
}
