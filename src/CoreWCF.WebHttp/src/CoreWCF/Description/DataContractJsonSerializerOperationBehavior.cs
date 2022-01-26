// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Xml;

namespace CoreWCF.Description
{
    internal class DataContractJsonSerializerOperationBehavior : DataContractSerializerOperationBehavior
    {
        private readonly EmitTypeInformation _emitTypeInformation;

        public DataContractJsonSerializerOperationBehavior(OperationDescription description, int maxItemsInObjectGraph, bool ignoreExtensionDataObject, EmitTypeInformation emitTypeInformation)
            : base(description)
        {
            MaxItemsInObjectGraph = maxItemsInObjectGraph;
            IgnoreExtensionDataObject = ignoreExtensionDataObject;

            _emitTypeInformation = emitTypeInformation;
        }

        public override XmlObjectSerializer CreateSerializer(Type type, string name, string ns, IList<Type> knownTypes)
        {
            return new DataContractJsonSerializer(type, new DataContractJsonSerializerSettings
            {
                RootName = name,
                KnownTypes = knownTypes,
                MaxItemsInObjectGraph = MaxItemsInObjectGraph,
                IgnoreExtensionDataObject = IgnoreExtensionDataObject,
                EmitTypeInformation = _emitTypeInformation
            });
        }

        public override XmlObjectSerializer CreateSerializer(Type type, XmlDictionaryString name, XmlDictionaryString ns, IList<Type> knownTypes)
        {
            return new DataContractJsonSerializer(type, new DataContractJsonSerializerSettings
            {
                RootName = name.Value,
                KnownTypes = knownTypes,
                MaxItemsInObjectGraph = MaxItemsInObjectGraph,
                IgnoreExtensionDataObject = IgnoreExtensionDataObject,
                EmitTypeInformation = _emitTypeInformation
            });
        }
    }
}
