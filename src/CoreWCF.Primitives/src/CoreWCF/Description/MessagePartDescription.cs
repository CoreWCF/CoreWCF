// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Security;
using System.Reflection;
using CoreWCF.Security;

namespace CoreWCF.Description
{
    public class MessagePartDescription
    {
        private readonly XmlName name;
        private readonly string ns;
        private int index;
        private Type type;
        private int serializationPosition;
        private ProtectionLevel protectionLevel;
        private bool hasProtectionLevel;
        private MemberInfo memberInfo;

        // TODO: Was ICustomAttributeProvider
        private CustomAttributeProvider additionalAttributesProvider;
        private bool multiple;

        //string baseType;
        private string uniquePartName;

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
            hasProtectionLevel = other.hasProtectionLevel;
            protectionLevel = other.protectionLevel;
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
            get { return protectionLevel; }
            set
            {
                if (!ProtectionLevelHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                protectionLevel = value;
                hasProtectionLevel = true;
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