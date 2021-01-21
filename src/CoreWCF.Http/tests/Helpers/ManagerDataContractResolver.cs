// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;
using System.Xml;

namespace Helpers
{
    public class ManagerDataContractResolver<T> : DataContractResolver
    {
        private string Namespace
        {
            get { return typeof(T).Namespace ?? "global"; }
        }

        private string Name
        {
            get { return typeof(T).Name; }
        }

        public override Type ResolveName(string typeName, string typeNamespace, Type declaredType, DataContractResolver knownTypeResolver)
        {
            if (typeName == this.Name && typeNamespace == this.Namespace)
            {
                return typeof(T);
            }
            else
            {
                return knownTypeResolver.ResolveName(typeName, typeNamespace, declaredType, null);
            }
        }

        public override bool TryResolveType(Type type, Type declaredType, DataContractResolver knownTypeResolver, out XmlDictionaryString typeName, out XmlDictionaryString typeNamespace)
        {
            if (type == typeof(T))
            {
                XmlDictionary dic = new XmlDictionary();
                typeName = dic.Add(this.Name);
                typeNamespace = dic.Add(this.Namespace);
                return true;
            }
            else
            {
                return knownTypeResolver.TryResolveType(type, declaredType, null, out typeName, out typeNamespace);
            }
        }
    }
}
