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
        private readonly string _ns;
        private ProtectionLevel _protectionLevel;
        private bool _hasProtectionLevel;

        // TODO: Was ICustomAttributeProvider
        private CustomAttributeProvider _additionalAttributesProvider;

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

            _ns = ns;
        }

        internal MessagePartDescription(MessagePartDescription other)
        {
            XmlName = other.XmlName;
            _ns = other._ns;
            Index = other.Index;
            Type = other.Type;
            SerializationPosition = other.SerializationPosition;
            _hasProtectionLevel = other._hasProtectionLevel;
            _protectionLevel = other._protectionLevel;
            MemberInfo = other.MemberInfo;
            Multiple = other.Multiple;
            _additionalAttributesProvider = other._additionalAttributesProvider;
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
            get { return _ns; }
        }

        public Type Type { get; set; }

        public int Index { get; set; }

        public bool Multiple { get; set; }

        public ProtectionLevel ProtectionLevel
        {
            get { return _protectionLevel; }
            set
            {
                if (!ProtectionLevelHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                _protectionLevel = value;
                _hasProtectionLevel = true;
            }
        }
        public MemberInfo MemberInfo { get; set; }

        internal bool HasProtectionLevel => false;

        internal CustomAttributeProvider AdditionalAttributesProvider
        {
            get { return _additionalAttributesProvider ?? MemberInfo; }
            set { _additionalAttributesProvider = value; }
        }

        internal string UniquePartName { get; set; }

        internal int SerializationPosition { get; set; }
    }
}