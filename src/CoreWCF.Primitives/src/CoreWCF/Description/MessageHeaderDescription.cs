// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF.Description
{
    public class MessageHeaderDescription : MessagePartDescription
    {
        private bool _isUnknownHeader;

        public MessageHeaderDescription(string name, string ns)
            : base(name, ns)
        {
        }

        internal MessageHeaderDescription(MessageHeaderDescription other)
            : base(other)
        {
            MustUnderstand = other.MustUnderstand;
            Relay = other.Relay;
            Actor = other.Actor;
            TypedHeader = other.TypedHeader;
            IsUnknownHeaderCollection = other.IsUnknownHeaderCollection;
        }

        internal override MessagePartDescription Clone()
        {
            return new MessageHeaderDescription(this);
        }

        public string Actor { get; set; }

        public bool MustUnderstand { get; set; }

        public bool Relay { get; set; }

        public bool TypedHeader { get; set; }

        internal bool IsUnknownHeaderCollection
        {
            get
            {
                return _isUnknownHeader || Multiple && (Type == typeof(XmlElement));
            }
            set
            {
                _isUnknownHeader = value;
            }
        }
    }
}