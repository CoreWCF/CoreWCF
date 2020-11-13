using System;
using System.Net.Security;
using System.Reflection;
using CoreWCF.Security;

namespace CoreWCF.Description
{
    public class MessagePartDescription
    {
        XmlName name;
        string ns;
        int index;
        Type type;
        int serializationPosition;
        ProtectionLevel protectionLevel;
        bool hasProtectionLevel;
        MemberInfo memberInfo;
        // TODO: Was ICustomAttributeProvider
        CustomAttributeProvider additionalAttributesProvider;

        bool multiple;
        //string baseType;
        string uniquePartName;

        public MessagePartDescription(string name, string ns)
        {
            if (name == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("name", SR.SFxParameterNameCannotBeNull);
            }

            this.name = new XmlName(name, true /*isEncoded*/);

            if (!string.IsNullOrEmpty(ns))
            {
                NamingHelper.CheckUriParameter(ns, "ns");
            }

            this.ns = ns;
        }

        internal MessagePartDescription(MessagePartDescription other)
        {
            name = other.name;
            ns = other.ns;
            index = other.index;
            type = other.type;
            serializationPosition = other.serializationPosition;
            this.hasProtectionLevel = other.hasProtectionLevel;
            this.protectionLevel = other.protectionLevel;
            memberInfo = other.memberInfo;
            multiple = other.multiple;
            additionalAttributesProvider = other.additionalAttributesProvider;
            //this.baseType = other.baseType;
            //this.uniquePartName = other.uniquePartName;
        }

        internal virtual MessagePartDescription Clone()
        {
            return new MessagePartDescription(this);
        }

        internal XmlName XmlName
        {
            get { return name; }
        }

        public string Name
        {
            get { return name.EncodedName; }
        }

        public string Namespace
        {
            get { return ns; }
        }

        public Type Type
        {
            get { return type; }
            set { type = value; }
        }

        public int Index
        {
            get { return index; }
            set { index = value; }
        }

        public bool Multiple
        {
            get { return multiple; }
            set { multiple = value; }
        }

        public ProtectionLevel ProtectionLevel
        {
            get { return this.protectionLevel; }
            set
            {
                if (!ProtectionLevelHelper.IsDefined(value))
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                this.protectionLevel = value;
                this.hasProtectionLevel = true;
            }
        }
        public MemberInfo MemberInfo
        {
            get { return memberInfo; }
            set { memberInfo = value; }
        }

        internal bool HasProtectionLevel => false;

        internal CustomAttributeProvider AdditionalAttributesProvider
        {
            get { return additionalAttributesProvider ?? memberInfo; }
            set { additionalAttributesProvider = value; }
        }

        internal string UniquePartName
        {
            get { return uniquePartName; }
            set { uniquePartName = value; }
        }

        internal int SerializationPosition
        {
            get { return serializationPosition; }
            set { serializationPosition = value; }
        }
    }
}