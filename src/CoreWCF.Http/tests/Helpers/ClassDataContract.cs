using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;

namespace Helpers
{
    internal class ClassDataContract : DataContract
    {
        private ClassDataContract baseContract;
        private List<DataMember> members;

        internal ClassDataContract()
        {
        }

        internal ClassDataContract(Type type) : base(type)
        {
            object[] dataContractAttributes;
            bool hasDataContract;
            string name = null, ns = null;
            if (type.IsSerializable)
            {
                if (type.IsDefined(Globals.TypeOfDataContractAttribute, false))
                    throw new Exception(String.Format("Type {0} has both [Serializable] and [DataContract]", type.FullName));
                hasDataContract = false;
                GetStableName(null, out name, out ns);
            }
            else
            {
                dataContractAttributes = type.GetCustomAttributes(Globals.TypeOfDataContractAttribute, false);
                if (dataContractAttributes != null && dataContractAttributes.Length > 0)
                {
                    if (dataContractAttributes.Length > 1)
                        throw new Exception(String.Format("Type {0} has more than one [DataContract] attribute", type.FullName));

                    hasDataContract = true;
                    DataContractAttribute dataContractAttribute = (DataContractAttribute)dataContractAttributes[0];
                    GetStableName(dataContractAttribute, out name, out ns);
                }
                else
                    throw new Exception("Type has neither Serializable nor DataContract");
            }

            this.StableName = new XmlQualifiedName(name, ns);

            if (type.BaseType != null && type.BaseType != Globals.TypeOfObject && type.BaseType != Globals.TypeOfValueType)
                this.BaseContract = (ClassDataContract)DataContract.GetDataContract(type.BaseType);
            else
                this.BaseContract = null;
            ImportDataMembers(type, hasDataContract);
        }

        private void ImportDataMembers(Type type, bool hasDataContract)
        {
            members = new List<DataMember>();
            MemberInfo[] memberInfos = type.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (int i = 0; i < memberInfos.Length; i++)
            {
                MemberInfo member = memberInfos[i];
                if (hasDataContract)
                {
                    object[] memberAttributes = member.GetCustomAttributes(typeof(DataMemberAttribute), false);
                    if (memberAttributes != null && memberAttributes.Length > 0)
                    {
                        if (memberAttributes.Length > 1)
                            throw new Exception(string.Format("member {1} in type {0} has too many DataMemberAttribute", member.DeclaringType.FullName, member.Name));
                        if (member.MemberType == MemberTypes.Property)
                        {
                            PropertyInfo property = (PropertyInfo)member;

                            MethodInfo getMethod = property.GetGetMethod(true);
                            if (getMethod != null && IsMethodOverriding(getMethod))
                                continue;
                            MethodInfo setMethod = property.GetSetMethod(true);
                            if (setMethod != null && IsMethodOverriding(setMethod))
                                continue;
                            if (getMethod == null)
                                throw new Exception(string.Format("member {1} in type {0} has no get method", property.DeclaringType, property.Name));

                            if (setMethod == null)
                                throw new Exception(string.Format("member {1} in type {0} has no set method", property.DeclaringType, property.Name));
                            if (getMethod.GetParameters().Length > 0)
                                throw new Exception("indexer is not supported");
                        }
                        else if (member.MemberType != MemberTypes.Field)
                            throw new Exception("only field or property is supported now");

                        DataMember memberContract = new DataMember(member);
                        DataMemberAttribute memberAttribute = (DataMemberAttribute)memberAttributes[0];
                        if (memberAttribute.Name == null)
                            memberContract.Name = member.Name;
                        else
                            memberContract.Name = memberAttribute.Name;
                        members.Add(memberContract);
                    }
                }
                else
                {
                    FieldInfo field = member as FieldInfo;
                    if (field != null && !field.IsNotSerialized)
                    {
                        DataMember memberContract = new DataMember(member);
                        memberContract.Name = member.Name;
                        memberContract.VersionAdded = Globals.DefaultVersion;
                        //TODO, sowmys: Do Optionally Serialized here.
                        members.Add(memberContract);
                    }
                }
            }
            //TODO: need to update when introduce versions
            members.Sort(DataMemberComparer.Singleton);
        }

        private static bool IsMethodOverriding(MethodInfo method)
        {
            return method.IsVirtual && ((method.Attributes & MethodAttributes.NewSlot) == 0);
        }

        internal ClassDataContract BaseContract
        {
            get { return baseContract; }
            set { baseContract = value; }
        }

        internal List<DataMember> Members
        {
            get { return members; }
            set { members = value; }
        }

        //TODO: need to update when introduce versions
        public override bool Equals(object other)
        {
            if ((object)this == other)
                return true;

            if (base.Equals(other))
            {
                ClassDataContract dataContract = other as ClassDataContract;
                if (dataContract != null)
                {
                    if (Members.Count != dataContract.Members.Count)
                        return false;

                    for (int i = 0; i < Members.Count; i++)
                    {
                        if (!Members[i].Equals(dataContract.Members[i]))
                            return false;
                    }

                    if (BaseContract == null)
                        return (dataContract.BaseContract == null);
                    else
                        return BaseContract.StableName.Equals(dataContract.BaseContract.StableName);
                }
            }

            return false;
        }

        /* for version added feature 
		private static DataMember[] SortByVersionAndName(DataMember[] dataMembers)
		{
			Array.Sort(dataMembers, DataMemberVersionComparer.Singleton);

			int vStart = 0;
			int vEnd = 0;

			while (dataMembers != null && vEnd < dataMembers.Length)
			{
				while (vEnd < dataMembers.Length && dataMembers[vEnd].Attribute.VersionAdded == dataMembers[vStart].Attribute.VersionAdded)
					vEnd++;

				Array.Sort(dataMembers, vStart, vEnd - vStart, DataMemberComparer.Singleton);
				vStart = vEnd;
			}

			return dataMembers;
		}
		//*/
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        internal class DataMemberComparer : IComparer<DataMember>
        {
            public int Compare(DataMember x, DataMember y)
            {
                //TODO,sowmys: Check why this does not do case sensitive compare
                return String.Compare(x.Name, y.Name, false);
            }
            public bool Equals(DataMember x, DataMember y)
            {
                return x == y;
            }

            public int GetHashCode(DataMember x)
            {
                return ((object)x).GetHashCode();
            }
            internal static DataMemberComparer Singleton = new DataMemberComparer();
        }

        internal class DataMemberVersionComparer : IComparer<DataMember>
        {
            public int Compare(DataMember x, DataMember y)
            {
                return x.VersionAdded - y.VersionAdded;
            }

            public bool Equals(DataMember x, DataMember y)
            {
                return x.VersionAdded == y.VersionAdded;
            }

            public int GetHashCode(DataMember x)
            {
                return ((object)x).GetHashCode();
            }
            internal static DataMemberVersionComparer Singleton = new DataMemberVersionComparer();
        }
    }
}