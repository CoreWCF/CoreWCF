// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF.Description
{
    internal class DataContractSerializerMessageContractImporter
    {
        internal const string GenericMessageSchemaTypeName = "MessageBody";
        internal const string GenericMessageSchemaTypeNamespace = "http://schemas.microsoft.com/Message";
        private const string StreamBodySchemaTypeName = "StreamBody";
        private const string StreamBodySchemaTypeNamespace = GenericMessageSchemaTypeNamespace;

        internal static XmlQualifiedName GenericMessageTypeName = new XmlQualifiedName(GenericMessageSchemaTypeName, GenericMessageSchemaTypeNamespace);
        internal static XmlQualifiedName StreamBodyTypeName = new XmlQualifiedName(StreamBodySchemaTypeName, StreamBodySchemaTypeNamespace);

    }
}
