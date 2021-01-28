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
        private XmlSerializer _serializer;
        private Type _rootType;
        private string _rootName;
        private string _rootNamespace;
        private bool _isSerializerSetExplicit = false;

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
            _rootType = type;
            _rootName = rootName;
            _rootNamespace = rootNamespace == null ? string.Empty : rootNamespace;
            _serializer = xmlSerializer;

            if (_serializer == null)
            {
                if (_rootName == null)
                {
                    _serializer = new XmlSerializer(type);
                }
                else
                {
                    XmlRootAttribute xmlRoot = new XmlRootAttribute();
                    xmlRoot.ElementName = _rootName;
                    xmlRoot.Namespace = _rootNamespace;
                    _serializer = new XmlSerializer(type, xmlRoot);
                }
            }
            else
            {
                _isSerializerSetExplicit = true;
            }

            //try to get rootName and rootNamespace from type since root name not set explicitly
            if (_rootName == null)
            {
                XmlTypeMapping mapping = new XmlReflectionImporter().ImportTypeMapping(_rootType);
                _rootName = mapping.ElementName;
                _rootNamespace = mapping.Namespace;
            }
        }

        public override void WriteObject(XmlDictionaryWriter writer, object graph)
        {
            if (_isSerializerSetExplicit)
            {
                _serializer.Serialize(writer, new object[] { graph });
            }
            else
            {
                _serializer.Serialize(writer, graph);
            }
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
            if (_isSerializerSetExplicit)
            {
                object[] deserializedObjects = (object[])_serializer.Deserialize(reader);
                if (deserializedObjects != null && deserializedObjects.Length > 0)
                {
                    return deserializedObjects[0];
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return _serializer.Deserialize(reader);
            }
        }

        public override bool IsStartObject(XmlDictionaryReader reader)
        {
            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }

            reader.MoveToElement();

            if (_rootName != null)
            {
                return reader.IsStartElement(_rootName, _rootNamespace);
            }
            else
            {
                return reader.IsStartElement();
            }
        }
    }
}