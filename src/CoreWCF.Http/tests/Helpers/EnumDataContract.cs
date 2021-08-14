using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;

namespace Helpers
{
    internal class EnumDataContract : DataContract
    {
        PrimitiveDataContract baseContract;
        List<DataMember> members;
        List<long> values;
        bool isULong;
        bool isFlags;

        internal EnumDataContract()
        {
        }

        internal EnumDataContract(Type type) : base(type)
        {
            string name = null, ns = null;
            object[] dataContractAttributes = type.GetCustomAttributes(Globals.TypeOfDataContractAttribute, false);
            bool hasDataContract;
            if (dataContractAttributes != null && dataContractAttributes.Length > 0)
            {
                if (dataContractAttributes.Length > 1)
                    throw new Exception(string.Format("{0} has TooManyDataContracts", type.FullName));

                hasDataContract = true;
                DataContractAttribute dataContractAttribute = (DataContractAttribute)dataContractAttributes[0];
                GetStableName(dataContractAttribute, out name, out ns);
            }
            else
            {
                hasDataContract = false;
                GetStableName(null, out name, out ns);
            }
            StableName = new XmlQualifiedName(name, ns);

            Type baseType = Enum.GetUnderlyingType(type);
            baseContract = PrimitiveDataContract.GetPrimitiveDataContract(baseType);
            isULong = (baseType == Globals.TypeOfULong);

            FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.Public);
            members = new List<DataMember>(fields.Length);
            values = new List<long>(fields.Length);

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                bool dataMemberValid = false;
                object[] memberAttributes = field.GetCustomAttributes(Globals.TypeOfDataMemberAttribute, false);
                if (hasDataContract)
                {
                    if (memberAttributes != null && memberAttributes.Length > 0)
                    {
                        if (memberAttributes.Length > 1)
                            throw new Exception(string.Format("{0} {1} has TooManyDataMembers", field.DeclaringType.FullName, field.Name));
                        DataMemberAttribute memberAttribute = (DataMemberAttribute)memberAttributes[0];
                        DataMember memberContract = new DataMember(field);
                        if (memberAttribute.Name == null || memberAttribute.Name.Length == 0)
                            memberContract.Name = field.Name;
                        else
                            memberContract.Name = memberAttribute.Name;
                        members.Add(memberContract);
                        dataMemberValid = true;
                    }
                }
                else
                {
                    if (!field.IsNotSerialized)
                    {
                        DataMember memberContract = new DataMember(field);
                        memberContract.Name = field.Name;
                        members.Add(memberContract);
                        dataMemberValid = true;
                    }
                }

                if (dataMemberValid)
                {
                    object enumValue = field.GetValue(null);
                    if (isULong)
                        values.Add((long)((IConvertible)enumValue).ToUInt64(null));
                    else
                        values.Add(((IConvertible)enumValue).ToInt64(null));
                }
            }

            IsFlags = type.IsDefined(Globals.TypeOfFlagsAttribute, false);
        }

        internal PrimitiveDataContract BaseContract
        {
            get
            {
                return baseContract;
            }
            set
            {
                baseContract = value;
                isULong = (baseContract.UnderlyingType == Globals.TypeOfULong);
            }
        }

        internal List<DataMember> Members
        {
            get { return members; }
            set { members = value; }
        }

        internal List<long> Values
        {
            get { return values; }
            set { values = value; }
        }

        internal bool IsFlags
        {
            get { return isFlags; }
            set { isFlags = value; }
        }

        internal bool IsULong
        {
            get { return isULong; }
        }
    }
}