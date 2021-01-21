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
        private readonly string ns;
        private ProtectionLevel protectionLevel;
        private bool hasProtectionLevel;

        // TODO: Was ICustomAttributeProvider
        private CustomAttributeProvider additionalAttributesProvider;

        public MessagePartDescription(string name, string ns)
        {
            if (name == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("name", SR.SFxParameterNameCannotBeNull);
            }

            XmlName = new XmlName(name, true /*isEncoded*/);

            if (!string.IsNullOrEmpty(ns))
            {
                NamingHelper.CheckUriParameter(ns, "ns");
            }

            this.ns = ns;
        }

        internal MessagePartDescription(MessagePartDescription other)
        {
            XmlName = other.XmlName;
            ns = other.ns;
            Index = other.Index;
            Type = other.Type;
            SerializationPosition = other.SerializationPosition;
            hasProtectionLevel = other.hasProtectionLevel;
            protectionLevel = other.protectionLevel;
            MemberInfo = other.MemberInfo;
            Multiple = other.Multiple;
            additionalAttributesProvider = other.additionalAttributesProvider;
            //this.baseType = other.baseType;
            //this.uniquePartName = other.uniquePartName;
        }

        internal virtual MessagePartDescription Clone()
        {
            return new MessagePartDescription(this);
        }

        internal XmlName XmlName { get; }

        public string Name
        {
            get { return XmlName.EncodedName; }
        }

        public string Namespace
        {
            get { return ns; }
        }

        public Type Type { get; set; }

        public int Index { get; set; }

        public bool Multiple { get; set; }

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
        public MemberInfo MemberInfo { get; set; }

        internal bool HasProtectionLevel => false;

        internal CustomAttributeProvider AdditionalAttributesProvider
        {
            get { return additionalAttributesProvider ?? MemberInfo; }
            set { additionalAttributesProvider = value; }
        }

        internal string UniquePartName { get; set; }

        internal int SerializationPosition { get; set; }
    }
}