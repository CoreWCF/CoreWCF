// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;
using System.Xml.Schema;

namespace CoreWCF.Runtime.Serialization
{
    internal interface IXsdDataContractExporter
    {
        void Export(Type type);
        XmlQualifiedName GetRootElementName(Type type);
        XmlSchemaType GetSchemaType(Type type);
        XmlQualifiedName GetSchemaTypeName(Type type);
    }
}
