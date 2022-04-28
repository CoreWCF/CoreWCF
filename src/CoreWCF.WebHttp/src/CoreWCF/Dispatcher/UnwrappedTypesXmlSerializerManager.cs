// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    internal class UnwrappedTypesXmlSerializerManager
    {
        private readonly Dictionary<Type, XmlTypeMapping> _allTypes;
        private readonly XmlReflectionImporter _importer;
        private readonly Dictionary<object, IList<Type>> _operationTypes;
        private bool _serializersCreated;
        private readonly Dictionary<Type, XmlSerializer> _serializersMap;
        private readonly object _thisLock;

        public UnwrappedTypesXmlSerializerManager()
        {
            _allTypes = new Dictionary<Type, XmlTypeMapping>();
            _serializersMap = new Dictionary<Type, XmlSerializer>();
            _operationTypes = new Dictionary<object, IList<Type>>();
            _importer = new XmlReflectionImporter();
            _thisLock = new object();
        }

        public TypeSerializerPair[] GetOperationSerializers(object key)
        {
            if (key == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(key));
            }

            lock (_thisLock)
            {
                if (!_serializersCreated)
                {
                    BuildSerializers();
                    _serializersCreated = true;
                }

                List<TypeSerializerPair> serializers = new List<TypeSerializerPair>();
                IList<Type> operationTypes = _operationTypes[key];
                for (int i = 0; i < operationTypes.Count; ++i)
                {
                    TypeSerializerPair pair = new TypeSerializerPair
                    {
                        Type = operationTypes[i],
                        Serializer = new XmlSerializerXmlObjectSerializer(_serializersMap[operationTypes[i]])
                    };
                    serializers.Add(pair);
                }

                return serializers.ToArray();
            }
        }

        public void RegisterType(object key, IList<Type> types)
        {
            if (key == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(key));
            }

            if (types == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(types));
            }

            lock (_thisLock)
            {
                if (_serializersCreated)
                {
                    Fx.Assert("An xml serializer type was added after the serializers were created");
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.XmlSerializersCreatedBeforeRegistration)));
                }

                for (int i = 0; i < types.Count; ++i)
                {
                    if (!_allTypes.ContainsKey(types[i]))
                    {
                        _allTypes.Add(types[i], _importer.ImportTypeMapping(types[i]));
                    }
                }

                _operationTypes.Add(key, types);
            }
        }

        private void BuildSerializers()
        {
            List<Type> types = new List<Type>();
            List<XmlMapping> mappings = new List<XmlMapping>();
            foreach (Type type in _allTypes.Keys)
            {
                XmlTypeMapping mapping = _allTypes[type];
                types.Add(type);
                mappings.Add(mapping);
            }

            XmlSerializer[] serializers = XmlSerializer.FromMappings(mappings.ToArray());
            for (int i = 0; i < types.Count; ++i)
            {
                _serializersMap.Add(types[i], serializers[i]);
            }
        }

        public struct TypeSerializerPair
        {
            public XmlObjectSerializer Serializer;

            public Type Type;
        }

        internal class XmlSerializerXmlObjectSerializer : XmlObjectSerializer
        {
            private readonly XmlSerializer _serializer;

            public XmlSerializerXmlObjectSerializer(XmlSerializer serializer)
            {
                _serializer = serializer ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serializer));
            }

            public override bool IsStartObject(XmlDictionaryReader reader) => _serializer.CanDeserialize(reader);

            public override object ReadObject(XmlDictionaryReader reader, bool verifyObjectName) => _serializer.Deserialize(reader);

            public override void WriteEndObject(XmlDictionaryWriter writer)
            {
                Fx.Assert("This method should never get hit");

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
            }

            public override void WriteObject(XmlDictionaryWriter writer, object graph)
            {
                _serializer.Serialize(writer, graph);
            }

            public override void WriteObjectContent(XmlDictionaryWriter writer, object graph)
            {
                Fx.Assert("This method should never get hit");

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
            }

            public override void WriteStartObject(XmlDictionaryWriter writer, object graph)
            {
                Fx.Assert("This method should never get hit");

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
            }
        }
    }
}
