// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;

namespace CoreWCF.Dispatcher
{
    internal class XmlSerializerObjectSerializer : XmlObjectSerializer
    {
        private XmlSerializer serializer;
        private Type rootType;
        private string rootName;
        private string rootNamespace;
        private bool isSerializerSetExplicit = false;

        internal XmlSerializerObjectSerializer(Type type)
        {
            Initialize(type, null /*rootName*/, null /*rootNamespace*/, null /*xmlSerializer*/);
        }

        internal XmlSerializerObjectSerializer(Type type, XmlQualifiedName qualifiedName, XmlSerializer xmlSerializer)
        {
            if (qualifiedName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(qualifiedName));
            }
            Initialize(type, qualifiedName.Name, qualifiedName.Namespace, xmlSerializer);
        }

        private void Initialize(Type type, string rootName, string rootNamespace, XmlSerializer xmlSerializer)
        {
            if (type == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(type));
            }
            rootType = type;
            this.rootName = rootName;
            this.rootNamespace = rootNamespace == null ? string.Empty : rootNamespace;
            serializer = xmlSerializer;

            if (serializer == null)
            {
                if (this.rootName == null)
                    serializer = new XmlSerializer(type);
                else
                {
                    XmlRootAttribute xmlRoot = new XmlRootAttribute();
                    xmlRoot.ElementName = this.rootName;
                    xmlRoot.Namespace = this.rootNamespace;
                    serializer = new XmlSerializer(type, xmlRoot);
                }
            }
            else
                isSerializerSetExplicit = true;

            //try to get rootName and rootNamespace from type since root name not set explicitly
            if (this.rootName == null)
            {
                XmlTypeMapping mapping = new XmlReflectionImporter().ImportTypeMapping(rootType);
                this.rootName = mapping.ElementName;
                this.rootNamespace = mapping.Namespace;
            }
        }

        public override void WriteObject(XmlDictionaryWriter writer, object graph)
        {
            if (isSerializerSetExplicit)
                serializer.Serialize(writer, new object[] { graph });
            else
                serializer.Serialize(writer, graph);
        }

        public override void WriteStartObject(XmlDictionaryWriter writer, object graph)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }

        public override void WriteObjectContent(XmlDictionaryWriter writer, object graph)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }

        public override void WriteEndObject(XmlDictionaryWriter writer)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }

        public override object ReadObject(XmlDictionaryReader reader, bool verifyObjectName)
        {
            if (isSerializerSetExplicit)
            {
                object[] deserializedObjects = (object[])serializer.Deserialize(reader);
                if (deserializedObjects != null && deserializedObjects.Length > 0)
                    return deserializedObjects[0];
                else
                    return null;
            }
            else
                return serializer.Deserialize(reader);
        }

        public override bool IsStartObject(XmlDictionaryReader reader)
        {
            if (reader == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));

            reader.MoveToElement();

            if (rootName != null)
            {
                return reader.IsStartElement(rootName, rootNamespace);
            }
            else
            {
                return reader.IsStartElement();
            }
        }
    }

}