// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml.Schema;

namespace CoreWCF.Runtime.Serialization
{
    internal static class XsdDataContractExporterFactory
    {
        public static IXsdDataContractExporter Create() => new XsdDataContractExporterWrapper();

        public static IXsdDataContractExporter Create(XmlSchemaSet xmlSchemaSet) => new XsdDataContractExporterWrapper(xmlSchemaSet);
    }
}
