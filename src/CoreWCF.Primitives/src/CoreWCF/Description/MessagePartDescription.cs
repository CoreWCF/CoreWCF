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
        private ProtectionLevel _protectionLevel;
        private bool _hasProtectionLevel;

        private ICustomAttributeProvider _additionalAttributesProvider;

        public MessagePartDescription(string name, string ns)
        {
            if (name == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name), SR.SFxParameterNameCannotBeNull);
            }

            XmlName = new XmlName(name, true /*isEncoded*/);

            if (!string.IsNullOrEmpty(ns))
            {
                NamingHelper.CheckUriParameter(ns, nameof(ns));
            }

            Namespace = ns;
        }

        internal MessagePartDescription(MessagePartDescription other)
        {
            XmlName = other.XmlName;
            Namespace = other.Namespace;
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

        internal MessagePartDescription(MessagePartDescription other, string name, string ns) : this(other)
        {
            XmlName = new XmlName(name, true /*isEncoded*/);
            Namespace = ns;
        }

        public virtual MessagePartDescription Clone()
        {
            return new MessagePartDescription(this);
        }

        internal XmlName XmlName { get; }

        public string Name
        {
            get { return XmlName.EncodedName; }
        }

        public string Namespace { get; }

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

        internal ICustomAttributeProvider AdditionalAttributesProvider
        {
            get { return _additionalAttributesProvider ?? MemberInfo; }
            set { _additionalAttributesProvider = value; }
        }

        internal string UniquePartName { get; set; }

        internal int SerializationPosition { get; set; }
    }
}
