// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using CoreWCF.Description;

namespace CoreWCF.Dispatcher
{
    internal class SingleBodyParameterXmlSerializerMessageFormatter : SingleBodyParameterMessageFormatter
    {
        private XmlObjectSerializer _cachedOutputSerializer;
        private Type _cachedOutputSerializerType;
        private readonly List<Type> _knownTypes;
        private readonly Type _parameterType;
        private readonly UnwrappedTypesXmlSerializerManager _serializerManager;
        private XmlObjectSerializer[] _serializers;
        private readonly object _thisLock;
        private UnwrappedTypesXmlSerializerManager.TypeSerializerPair[] _typeSerializerPairs;

        public SingleBodyParameterXmlSerializerMessageFormatter(OperationDescription operation, Type parameterType, bool isRequestFormatter, XmlSerializerOperationBehavior xsob, UnwrappedTypesXmlSerializerManager serializerManager)
            : base(operation, isRequestFormatter, "XmlSerializer")
        {
            if (operation == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(operation));
            }

            if (xsob == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(xsob));
            }

            _serializerManager = serializerManager ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serializerManager));
            _parameterType = parameterType ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameterType));
            List<Type> operationTypes = new List<Type>();
            operationTypes.Add(parameterType);
            _knownTypes = new List<Type>();
            if (operation.KnownTypes != null)
            {
                foreach (Type knownType in operation.KnownTypes)
                {
                    _knownTypes.Add(knownType);
                    operationTypes.Add(knownType);
                }
            }

            Type nullableType = SingleBodyParameterDataContractMessageFormatter.UnwrapNullableType(_parameterType);
            if (nullableType != _parameterType)
            {
                _knownTypes.Add(nullableType);
                operationTypes.Add(nullableType);
            }

            _serializerManager.RegisterType(this, operationTypes);
            _thisLock = new object();
        }

        protected override XmlObjectSerializer[] GetInputSerializers()
        {
            lock (_thisLock)
            {
                EnsureSerializers();
                return _serializers;
            }
        }

        protected override XmlObjectSerializer GetOutputSerializer(Type type)
        {
            lock (_thisLock)
            {
                if (_cachedOutputSerializerType != type)
                {
                    Type typeForSerializer = GetTypeForSerializer(type, _parameterType, _knownTypes);
                    EnsureSerializers();
                    bool foundSerializer = false;
                    if (_typeSerializerPairs != null)
                    {
                        for (int i = 0; i < _typeSerializerPairs.Length; ++i)
                        {
                            if (typeForSerializer == _typeSerializerPairs[i].Type)
                            {
                                _cachedOutputSerializer = _typeSerializerPairs[i].Serializer;
                                _cachedOutputSerializerType = type;
                                foundSerializer = true;
                                break;
                            }
                        }
                    }

                    if (!foundSerializer)
                    {
                        return null;
                    }
                }

                return _cachedOutputSerializer;
            }
        }

        //  must be called under a lock
        private void EnsureSerializers()
        {
            if (_typeSerializerPairs == null)
            {
                _typeSerializerPairs = _serializerManager.GetOperationSerializers(this);
                _serializers = new XmlObjectSerializer[_typeSerializerPairs.Length];
                for (int i = 0; i < _typeSerializerPairs.Length; ++i)
                {
                    _serializers[i] = _typeSerializerPairs[i].Serializer;
                }
            }
        }
    }
}
