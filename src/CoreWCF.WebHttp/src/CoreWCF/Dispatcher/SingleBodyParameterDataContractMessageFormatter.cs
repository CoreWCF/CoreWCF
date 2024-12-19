// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using CoreWCF.Channels;
using CoreWCF.Description;

namespace CoreWCF.Dispatcher
{
    internal class SingleBodyParameterDataContractMessageFormatter : SingleBodyParameterMessageFormatter
    {
        private static readonly Type s_typeOfNullable = typeof(Nullable<>);
        private static readonly Type[] s_collectionDataContractInterfaces = new Type[] { typeof(IEnumerable), typeof(IList), typeof(ICollection), typeof(IDictionary) };
        private static readonly Type[] s_genericCollectionDataContractInterfaces = new Type[] { typeof(IEnumerable<>), typeof(IList<>), typeof(ICollection<>), typeof(IDictionary<,>) };
        private XmlObjectSerializer _cachedOutputSerializer;
        private Type _cachedOutputSerializerType;
        private readonly bool _ignoreExtensionData;
        private XmlObjectSerializer[] _inputSerializers;
        private readonly IList<Type> _knownTypes;
        private readonly int _maxItemsInObjectGraph;
        private readonly Type _parameterDataContractType;
        private readonly object _thisLock;
        private readonly bool _useJsonFormat;
        private readonly bool _isParameterCollectionInterfaceDataContract;
        private readonly bool _isQueryable;

        public SingleBodyParameterDataContractMessageFormatter(OperationDescription operation, Type parameterType, bool isRequestFormatter, bool useJsonFormat, DataContractSerializerOperationBehavior dcsob)
            : base(operation, isRequestFormatter, "DataContractSerializer")
        {
            if (operation == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(operation));
            }

            if (parameterType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameterType));
            }

            if (dcsob == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(dcsob));
            }

            _parameterDataContractType = DataContractHelpers.GetSubstituteDataContractType(parameterType, out _isQueryable);
            _isParameterCollectionInterfaceDataContract = IsTypeCollectionInterface(_parameterDataContractType);
            List<Type> tmp = new List<Type>();
            if (operation.KnownTypes != null)
            {
                foreach (Type knownType in operation.KnownTypes)
                {
                    tmp.Add(knownType);
                }
            }

            Type nullableType = UnwrapNullableType(_parameterDataContractType);
            if (nullableType != _parameterDataContractType)
            {
                tmp.Add(nullableType);
            }

            _ignoreExtensionData = dcsob.IgnoreExtensionDataObject;
            _maxItemsInObjectGraph = dcsob.MaxItemsInObjectGraph;
            _knownTypes = tmp.AsReadOnly();
            ValidateType(_parameterDataContractType, _knownTypes);

            _useJsonFormat = useJsonFormat;
            CreateInputSerializers(_parameterDataContractType);

            _thisLock = new object();
        }

        internal static Type UnwrapNullableType(Type type)
        {
            while (type.IsGenericType && type.GetGenericTypeDefinition() == s_typeOfNullable)
            {
                type = type.GetGenericArguments()[0];
            }

            return type;
        }

        // The logic of this method should be kept the same as 
        // System.ServiceModel.Dispatcher.DataContractSerializerOperationFormatter.PartInfo.ReadObject
        protected override object ReadObject(Message message)
        {
            object val = base.ReadObject(message);
            if (_isQueryable && val != null)
            {
                return Queryable.AsQueryable((IEnumerable)val);
            }

            return val;
        }

        protected override void AttachMessageProperties(Message message, bool isRequest)
        {
            if (_useJsonFormat)
            {
                message.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.JsonProperty);
            }
        }

        protected override XmlObjectSerializer[] GetInputSerializers()
        {
            return _inputSerializers;
        }

        protected override XmlObjectSerializer GetOutputSerializer(Type type)
        {
            lock (_thisLock)
            {
                // if we already have a serializer for this type reuse it
                if (_cachedOutputSerializerType != type)
                {
                    Type typeForSerializer;
                    if (_isParameterCollectionInterfaceDataContract)
                    {
                        // if the parameterType is a collection interface, ensure the type implements it
                        if (!_parameterDataContractType.IsAssignableFrom(type))
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SerializationException(SR.Format(SR.TypeIsNotParameterTypeAndIsNotPresentInKnownTypes, type, OperationName, ContractName, _parameterDataContractType)));
                        }
                        typeForSerializer = _parameterDataContractType;
                    }
                    else
                    {
                        typeForSerializer = GetTypeForSerializer(type, _parameterDataContractType, _knownTypes);
                    }
                    _cachedOutputSerializer = CreateSerializer(typeForSerializer);
                    _cachedOutputSerializerType = type;
                }

                return _cachedOutputSerializer;
            }
        }

        private static bool IsTypeCollectionInterface(Type parameterType)
        {
            if (parameterType.IsGenericType && parameterType.IsInterface)
            {
                Type genericTypeDef = parameterType.GetGenericTypeDefinition();
                foreach (Type type in s_genericCollectionDataContractInterfaces)
                {
                    if (genericTypeDef == type)
                    {
                        return true;
                    }
                }
            }

            foreach (Type type in s_collectionDataContractInterfaces)
            {
                if (parameterType == type)
                {
                    return true;
                }
            }

            return false;
        }

        protected override void ValidateMessageFormatProperty(Message message)
        {
            if (_useJsonFormat)
            {
                // useJsonFormat is always false in the green bits
                message.Properties.TryGetValue(WebBodyFormatMessageProperty.Name, out object prop);
                WebBodyFormatMessageProperty formatProperty = (prop as WebBodyFormatMessageProperty);
                if (formatProperty == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.MessageFormatPropertyNotFound, OperationName, ContractName, ContractNs)));
                }
                if (formatProperty.Format != WebContentFormat.Json)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.InvalidHttpMessageFormat, OperationName, ContractName, ContractNs, formatProperty.Format, WebContentFormat.Json)));
                }
            }
            else
            {
                base.ValidateMessageFormatProperty(message);
            }
        }

        private static void ValidateType(Type parameterType, IEnumerable<Type> knownTypes)
        {
            XsdDataContractExporter dataContractExporter = new XsdDataContractExporter();
            if (knownTypes != null)
            {
                ExportOptions options = new ExportOptions();

                foreach (Type knownType in knownTypes)
                {
                    options.KnownTypes.Add(knownType);
                }

                dataContractExporter.Options = options;
            }

            dataContractExporter.GetSchemaTypeName(parameterType); // throws if parameterType is not a valid data contract
        }

        private void CreateInputSerializers(Type type)
        {
            List<XmlObjectSerializer> tmp = new List<XmlObjectSerializer>();
            tmp.Add(CreateSerializer(type));
            foreach (Type knownType in _knownTypes)
            {
                tmp.Add(CreateSerializer(knownType));
            }

            _inputSerializers = tmp.ToArray();
        }

        private XmlObjectSerializer CreateSerializer(Type type)
        {
            if (_useJsonFormat)
            {
                return new DataContractJsonSerializer(type, new DataContractJsonSerializerSettings
                {
                    KnownTypes = _knownTypes,
                    MaxItemsInObjectGraph = _maxItemsInObjectGraph,
                    IgnoreExtensionDataObject = _ignoreExtensionData,
                    EmitTypeInformation = EmitTypeInformation.AsNeeded
                });
            }
            else
            {
                return new DataContractSerializer(type, new DataContractSerializerSettings
                {
                    KnownTypes = _knownTypes,
                    MaxItemsInObjectGraph = _maxItemsInObjectGraph,
                    IgnoreExtensionDataObject = _ignoreExtensionData,
                    PreserveObjectReferences = false
                });
            }
        }
    }
}
