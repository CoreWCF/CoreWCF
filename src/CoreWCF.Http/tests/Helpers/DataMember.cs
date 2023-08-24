using System;
using System.Reflection;

namespace Helpers
{
    internal class DataMember
    {
        private DataContract memberTypeContract;
        private string name;
        private int versionAdded;
        private MemberInfo memberInfo;

        internal DataMember()
        {
        }

        internal DataMember(MemberInfo memberInfo)
        {
            this.memberInfo = memberInfo;
        }

        internal MemberInfo MemberInfo
        {
            get { return memberInfo; }
        }

        internal string Name
        {
            get { return name; }
            set { name = value; }
        }

        internal int VersionAdded
        {
            get { return versionAdded; }
            set { versionAdded = value; }
        }

        internal object GetMemberValue(object obj)
        {
            FieldInfo field = MemberInfo as FieldInfo;

            if (field != null)
                return ((FieldInfo)MemberInfo).GetValue(obj);

            return ((PropertyInfo)MemberInfo).GetValue(obj, null);
        }

        internal Type MemberType
        {
            get
            {
                FieldInfo field = MemberInfo as FieldInfo;
                if (field != null)
                    return field.FieldType;
                return ((PropertyInfo)MemberInfo).PropertyType;
            }
        }

        internal DataContract MemberTypeContract
        {
            get
            {
                if (memberTypeContract == null)
                {
                    if (MemberInfo != null)
                    {
                        lock (this)
                        {
                            if (memberTypeContract == null)
                            {
                                memberTypeContract = DataContract.GetDataContract(MemberType);
                            }
                        }
                    }
                }
                return memberTypeContract;
            }
            set
            {
                memberTypeContract = value;
            }
        }

        public override bool Equals(object other)
        {
            if ((object)this == other)
                return true;

            DataMember dataMember = other as DataMember;
            if (dataMember != null)
            {
                return (Name == dataMember.Name && MemberTypeContract.StableName.Equals(dataMember.MemberTypeContract.StableName));
            }
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}