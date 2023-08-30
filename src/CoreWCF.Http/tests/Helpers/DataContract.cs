// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml;

namespace Helpers
{
    internal class DataContract
    {
        private static Dictionary<Type, DataContract> cache = new Dictionary<Type, DataContract>();
        private Type underlyingType;
        private bool isValueType;
        private XmlQualifiedName stableName;

        internal static DataContract GetDataContract(Type type)
        {
            DataContract dataContract = null;

            if (!cache.TryGetValue(type, out dataContract))
            {
                lock (cache)
                {
                    if (!cache.TryGetValue(type, out dataContract))
                    {
                        dataContract = CreateDataContract(type);
                        cache.Add(type, dataContract);
                    }
                }
            }

            return dataContract;
        }

        private static DataContract CreateDataContract(Type type)
        {
            DataContract primitiveContract = PrimitiveDataContract.GetPrimitiveDataContract(type);

            if (primitiveContract != null)
                return primitiveContract;

            if (type.IsArray)
                return new ArrayDataContract(type);

            if (type.IsEnum)
                return new EnumDataContract(type);

            return new ClassDataContract(type);
        }

        internal static bool IsPrimitive(Type type)
        {
            return PrimitiveDataContract.GetPrimitiveDataContract(type) != null;
        }

        internal void GetStableName(DataContractAttribute dataContractAttribute, out string name, out string ns)
        {
            name = null;
            ns = null;
            if (dataContractAttribute == null)
                GetDefaultStableName(this.UnderlyingType, out name, out ns);
            else
            {
                if (dataContractAttribute.Name == null || dataContractAttribute.Name.Length == 0)
                    GetDefaultStableName(this.UnderlyingType, out name, out ns);
                else
                    name = dataContractAttribute.Name;

                if (dataContractAttribute.Namespace == null)
                {
                    //TODO,sowmys: Use DataContractNamespaceAttribute if one is provided
                    if (ns == null)
                    {
                        string defName;

                        GetDefaultStableName(this.UnderlyingType, out defName, out ns);
                    }
                }
                else
                    ns = dataContractAttribute.Namespace;
            }
        }

        internal static void GetDefaultStableName(Type type, out string name, out string ns)
        {
            DataContract primitiveContract = PrimitiveDataContract.GetPrimitiveDataContract(type);

            if (primitiveContract != null)
            {
                name = primitiveContract.StableName.Name;
                ns = primitiveContract.StableName.Namespace;
                return;
            }

            if (type.IsArray)
            {
                Type elementType = type;
                string arrayOfPrefix = "";

                while (elementType.IsArray)
                {
                    arrayOfPrefix += "ArrayOf";
                    elementType = elementType.GetElementType();
                }

                GetDefaultStableName(elementType, out name, out ns);
                name = arrayOfPrefix + name;
                return;
            }

            string clrNs = type.Namespace;

            if (clrNs == null) clrNs = String.Empty;

            if (type.DeclaringType == null)
                name = type.Name;
            else
            {
                int nsLen = clrNs.Length;

                if (nsLen > 0)
                    nsLen++; //include the . following namespace

                name = type.FullName.Substring(nsLen).Replace('+', '.');
            }

            ns = Globals.DefaultNamespace + clrNs.Replace('.', '/');
        }

        internal DataContract()
        {
        }

        internal DataContract(Type type)
        {
            underlyingType = type;
            isValueType = type.IsValueType;
        }

        internal Type UnderlyingType
        {
            get { return underlyingType; }
        }

        internal virtual string TopLevelElementName
        {
            get { return StableName.Name; }
        }

        internal virtual string TopLevelElementNamespace
        {
            get { return StableName.Namespace; }
        }

        internal bool IsValueType
        {
            get { return isValueType; }
            set { isValueType = value; }
        }

        internal XmlQualifiedName StableName
        {
            get { return stableName; }
            set { stableName = value; }
        }

        public override bool Equals(object other)
        {
            if ((object)this == other)
                return true;

            DataContract dataContract = other as DataContract;

            if (dataContract != null)
            {
                return (StableName.Name == dataContract.StableName.Name && StableName.Namespace == dataContract.StableName.Namespace && IsValueType == dataContract.IsValueType);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}