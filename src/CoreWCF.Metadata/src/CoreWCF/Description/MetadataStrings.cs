// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Description
{
    internal static class MetadataStrings
    {
        public static class MetadataExchangeStrings
        {
            /*
             * This file has a counterpart XmlStrings.cs in the svcutil codebase. 
             * When making chnages here, please consider whether they should be made there as well
             */
            public const string Prefix = "wsx";
            public const string Name = "WS-MetadataExchange";
            public const string Namespace = "http://schemas.xmlsoap.org/ws/2004/09/mex";
            public const string HttpBindingName = "MetadataExchangeHttpBinding";
            public const string HttpsBindingName = "MetadataExchangeHttpsBinding";
            public const string TcpBindingName = "MetadataExchangeTcpBinding";
            public const string NamedPipeBindingName = "MetadataExchangeNamedPipeBinding";
            public const string BindingNamespace = "http://schemas.microsoft.com/ws/2005/02/mex/bindings";

            public const string Metadata = "Metadata";
            public const string MetadataSection = "MetadataSection";
            public const string Dialect = "Dialect";
            public const string Identifier = "Identifier";
            public const string MetadataReference = "MetadataReference";
            public const string Location = "Location";
        }

        public static class WSTransfer
        {
            public const string Prefix = "wxf";
            public const string Name = "WS-Transfer";
            public const string Namespace = "http://schemas.xmlsoap.org/ws/2004/09/transfer";

            public const string GetAction = Namespace + "/Get";
            public const string GetResponseAction = Namespace + "/GetResponse";
        }

        public static class ServiceDescription
        {
            public const string Definitions = "definitions";
            public const string ArrayType = "arrayType";
        }

        public static class XmlSchema
        {
            public const string Schema = "schema";
        }

        public static class Xml
        {
            public const string Prefix = "xml";
            public const string NamespaceUri = "http://www.w3.org/XML/1998/namespace";

            public static class Attributes
            {
                public const string Id = "id";
            }

        }

        public static class Wsu
        {
            public const string Prefix = "wsu";
            public const string NamespaceUri = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
            public static class Attributes
            {
                public const string Id = "Id";
            }
        }

        public static class WSPolicy
        {
            public const string Prefix = "wsp";
            public const string NamespaceUri = "http://schemas.xmlsoap.org/ws/2004/09/policy";
            public const string NamespaceUri15 = "http://www.w3.org/ns/ws-policy";

            public static class Attributes
            {
                public const string Optional = "Optional";
                public const string PolicyURIs = "PolicyURIs";
                public const string URI = "URI";
                public const string TargetNamespace = "TargetNamespace";
            }
            public static class Elements
            {
                public const string PolicyReference = "PolicyReference";
                public const string All = "All";
                public const string ExactlyOne = "ExactlyOne";
                public const string Policy = "Policy";
            }
        }

    }
}
