// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF.Description
{
    public class MessageHeaderDescription : MessagePartDescription
    {
        private bool mustUnderstand;
        private bool relay;
        private string actor;
        private bool typedHeader;
        private bool isUnknownHeader;

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

        public string Actor
        {
            get { return actor; }
            set { actor = value; }
        }

        public bool MustUnderstand
        {
            get { return mustUnderstand; }
            set { mustUnderstand = value; }
        }

        public bool Relay
        {
            get { return relay; }
            set { relay = value; }
        }

        public bool TypedHeader
        {
            get { return typedHeader; }
            set { typedHeader = value; }
        }

        internal bool IsUnknownHeaderCollection
        {
            get
            {
                return isUnknownHeader || Multiple && (Type == typeof(XmlElement));
            }
            set
            {
                isUnknownHeader = value;
            }
        }
    }
}