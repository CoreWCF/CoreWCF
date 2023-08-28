// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;

namespace Helpers
{
    internal class ArrayDataContract : DataContract
    {
        private DataContract itemContract;
        private int rank;

        internal ArrayDataContract() : base()
        {
        }

        internal ArrayDataContract(Type type) : base(type)
        {
            rank = type.GetArrayRank();
            Type elementType = type;
            string arrayOfPrefix = String.Empty;
            while (elementType.IsArray)
            {
                arrayOfPrefix += "ArrayOf";
                elementType = elementType.GetElementType();
            }

            DataContract elementDataContract = DataContract.GetDataContract(elementType);
            string name = arrayOfPrefix + elementDataContract.StableName.Name;
            string ns = elementDataContract is PrimitiveDataContract ? Globals.SerializationNamespace : elementDataContract.StableName.Namespace;
            this.StableName = new XmlQualifiedName(name, ns);
        }

        internal DataContract ItemContract
        {
            get
            {
                if (itemContract == null)
                {
                    if (UnderlyingType != null)
                    {
                        lock (this)
                        {
                            if (itemContract == null)
                            {
                                itemContract = DataContract.GetDataContract(UnderlyingType.GetElementType());
                            }
                        }
                    }
                }

                return itemContract;
            }
            set
            {
                itemContract = value;
            }
        }

        internal int Rank
        {
            get { return rank; }
            set { rank = value; }
        }

        public override bool Equals(object other)
        {
            if ((object)this == other)
                return true;

            if (base.Equals(other))
            {
                ArrayDataContract dataContract = other as ArrayDataContract;
                if (dataContract != null)
                {
                    return ItemContract.Equals(dataContract.itemContract);
                }
            }

            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}