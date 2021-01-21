// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    // TODO: This needed to be made public for NetTcp, investigate making it internal again
    public abstract class AddressingHeader : DictionaryHeader, IMessageHeaderWithSharedNamespace
    {
        private readonly AddressingVersion version;

        protected AddressingHeader(AddressingVersion version)
        {
            this.version = version;
        }

        internal AddressingVersion Version
        {
            get { return version; }
        }

        XmlDictionaryString IMessageHeaderWithSharedNamespace.SharedPrefix
        {
            get { return XD.AddressingDictionary.Prefix; }
        }

        XmlDictionaryString IMessageHeaderWithSharedNamespace.SharedNamespace
        {
            get { return version.DictionaryNamespace; }
        }

        public override XmlDictionaryString DictionaryNamespace
        {
            get { return version.DictionaryNamespace; }
        }
    }

    internal class ActionHeader : AddressingHeader
    {
        private readonly string action;
        private const bool mustUnderstandValue = true;

        private ActionHeader(string action, AddressingVersion version)
            : base(version)
        {
            this.action = action;
        }

        public string Action
        {
            get { return action; }
        }

        public override bool MustUnderstand
        {
            get { return mustUnderstandValue; }
        }

        public override XmlDictionaryString DictionaryName
        {
            get { return XD.AddressingDictionary.Action; }
        }

        public static ActionHeader Create(string action, AddressingVersion addressingVersion)
        {
            if (action == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(action));
            }

            if (addressingVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(addressingVersion));
            }

            return new ActionHeader(action, addressingVersion);
        }

        public static ActionHeader Create(XmlDictionaryString dictionaryAction, AddressingVersion addressingVersion)
        {
            if (dictionaryAction == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(action));
            }

            if (addressingVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(addressingVersion));
            }

            return new DictionaryActionHeader(dictionaryAction, addressingVersion);
        }

        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            writer.WriteString(action);
        }

        public static string ReadHeaderValue(XmlDictionaryReader reader, AddressingVersion addressingVersion)
        {
            Fx.Assert(reader.IsStartElement(XD.AddressingDictionary.Action, addressingVersion.DictionaryNamespace), "");
            string act = reader.ReadElementContentAsString();

            if (act.Length > 0 && (act[0] <= 32 || act[act.Length - 1] <= 32))
            {
                act = XmlUtil.Trim(act);
            }

            return act;
        }

        public static ActionHeader ReadHeader(XmlDictionaryReader reader, AddressingVersion version,
            string actor, bool mustUnderstand, bool relay)
        {
            string action = ReadHeaderValue(reader, version);

            if (actor.Length == 0 && mustUnderstand == mustUnderstandValue && !relay)
            {
                return new ActionHeader(action, version);
            }
            else
            {
                return new FullActionHeader(action, actor, mustUnderstand, relay, version);
            }
        }

        private class DictionaryActionHeader : ActionHeader
        {
            private readonly XmlDictionaryString dictionaryAction;

            public DictionaryActionHeader(XmlDictionaryString dictionaryAction, AddressingVersion version)
                : base(dictionaryAction.Value, version)
            {
                this.dictionaryAction = dictionaryAction;
            }

            protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                writer.WriteString(dictionaryAction);
            }
        }

        private class FullActionHeader : ActionHeader
        {
            private readonly string actor;
            private readonly bool mustUnderstand;
            private readonly bool relay;

            public FullActionHeader(string action, string actor, bool mustUnderstand, bool relay, AddressingVersion version)
                : base(action, version)
            {
                this.actor = actor;
                this.mustUnderstand = mustUnderstand;
                this.relay = relay;
            }

            public override string Actor
            {
                get { return actor; }
            }

            public override bool MustUnderstand
            {
                get { return mustUnderstand; }
            }

            public override bool Relay
            {
                get { return relay; }
            }
        }
    }

    internal class FromHeader : AddressingHeader
    {
        private readonly EndpointAddress from;
        private const bool mustUnderstandValue = false;

        private FromHeader(EndpointAddress from, AddressingVersion version)
            : base(version)
        {
            this.from = from;
        }

        public EndpointAddress From
        {
            get { return from; }
        }

        public override XmlDictionaryString DictionaryName
        {
            get { return XD.AddressingDictionary.From; }
        }

        public override bool MustUnderstand
        {
            get { return mustUnderstandValue; }
        }

        public static FromHeader Create(EndpointAddress from, AddressingVersion addressingVersion)
        {
            if (from == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(from));
            }

            if (addressingVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(addressingVersion));
            }

            return new FromHeader(from, addressingVersion);
        }

        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            from.WriteContentsTo(Version, writer);
        }

        public static FromHeader ReadHeader(XmlDictionaryReader reader, AddressingVersion version,
            string actor, bool mustUnderstand, bool relay)
        {
            EndpointAddress from = ReadHeaderValue(reader, version);

            if (actor.Length == 0 && mustUnderstand == mustUnderstandValue && !relay)
            {
                return new FromHeader(from, version);
            }
            else
            {
                return new FullFromHeader(from, actor, mustUnderstand, relay, version);
            }
        }

        public static EndpointAddress ReadHeaderValue(XmlDictionaryReader reader, AddressingVersion addressingVersion)
        {
            Fx.Assert(reader.IsStartElement(XD.AddressingDictionary.From, addressingVersion.DictionaryNamespace), "");
            return EndpointAddress.ReadFrom(addressingVersion, reader);
        }

        private class FullFromHeader : FromHeader
        {
            private readonly string actor;
            private readonly bool mustUnderstand;
            private readonly bool relay;

            public FullFromHeader(EndpointAddress from, string actor, bool mustUnderstand, bool relay, AddressingVersion version)
                : base(from, version)
            {
                this.actor = actor;
                this.mustUnderstand = mustUnderstand;
                this.relay = relay;
            }

            public override string Actor
            {
                get { return actor; }
            }

            public override bool MustUnderstand
            {
                get { return mustUnderstand; }
            }

            public override bool Relay
            {
                get { return relay; }
            }
        }
    }

    internal class FaultToHeader : AddressingHeader
    {
        private readonly EndpointAddress faultTo;
        private const bool mustUnderstandValue = false;

        private FaultToHeader(EndpointAddress faultTo, AddressingVersion version)
            : base(version)
        {
            this.faultTo = faultTo;
        }

        public EndpointAddress FaultTo
        {
            get { return faultTo; }
        }

        public override XmlDictionaryString DictionaryName
        {
            get { return XD.AddressingDictionary.FaultTo; }
        }

        public override bool MustUnderstand
        {
            get { return mustUnderstandValue; }
        }

        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            faultTo.WriteContentsTo(Version, writer);
        }

        public static FaultToHeader Create(EndpointAddress faultTo, AddressingVersion addressingVersion)
        {
            if (faultTo == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(faultTo));
            }

            if (addressingVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(addressingVersion));
            }

            return new FaultToHeader(faultTo, addressingVersion);
        }

        public static FaultToHeader ReadHeader(XmlDictionaryReader reader, AddressingVersion version,
            string actor, bool mustUnderstand, bool relay)
        {
            EndpointAddress faultTo = ReadHeaderValue(reader, version);

            if (actor.Length == 0 && mustUnderstand == mustUnderstandValue && !relay)
            {
                return new FaultToHeader(faultTo, version);
            }
            else
            {
                return new FullFaultToHeader(faultTo, actor, mustUnderstand, relay, version);
            }
        }

        public static EndpointAddress ReadHeaderValue(XmlDictionaryReader reader, AddressingVersion version)
        {
            Fx.Assert(reader.IsStartElement(XD.AddressingDictionary.FaultTo, version.DictionaryNamespace), "");
            return EndpointAddress.ReadFrom(version, reader);
        }

        private class FullFaultToHeader : FaultToHeader
        {
            private readonly string actor;
            private readonly bool mustUnderstand;
            private readonly bool relay;

            public FullFaultToHeader(EndpointAddress faultTo, string actor, bool mustUnderstand, bool relay, AddressingVersion version)
                : base(faultTo, version)
            {
                this.actor = actor;
                this.mustUnderstand = mustUnderstand;
                this.relay = relay;
            }

            public override string Actor
            {
                get { return actor; }
            }

            public override bool MustUnderstand
            {
                get { return mustUnderstand; }
            }

            public override bool Relay
            {
                get { return relay; }
            }
        }
    }

    // TODO: This needed to be made public for NetTcp, investigate making it internal again
    public class ToHeader : AddressingHeader
    {
        private readonly Uri to;
        private const bool mustUnderstandValue = true;
        private static ToHeader anonymousToHeader10;
        //static ToHeader anonymousToHeader200408;

        protected ToHeader(Uri to, AddressingVersion version)
            : base(version)
        {
            this.to = to;
        }

        private static ToHeader AnonymousTo10
        {
            get
            {
                if (anonymousToHeader10 == null)
                {
                    anonymousToHeader10 = new AnonymousToHeader(AddressingVersion.WSAddressing10);
                }

                return anonymousToHeader10;
            }
        }

        //static ToHeader AnonymousTo200408
        //{
        //    get
        //    {
        //        if (anonymousToHeader200408 == null)
        //            anonymousToHeader200408 = new AnonymousToHeader(AddressingVersion.WSAddressingAugust2004);
        //        return anonymousToHeader200408;
        //    }
        //}

        public override XmlDictionaryString DictionaryName
        {
            get { return XD.AddressingDictionary.To; }
        }

        public override bool MustUnderstand
        {
            get { return mustUnderstandValue; }
        }

        public Uri To
        {
            get { return to; }
        }

        public static ToHeader Create(Uri toUri, XmlDictionaryString dictionaryTo, AddressingVersion addressingVersion)
        {
            if (addressingVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(addressingVersion));
            }

            if (((object)toUri == (object)addressingVersion.AnonymousUri))
            {
                if (addressingVersion == AddressingVersion.WSAddressing10)
                {
                    return AnonymousTo10;
                }
                else
                {
                    //return AnonymousTo200408;
                    throw new PlatformNotSupportedException($"Unsupported addressing version {addressingVersion.ToString()}");
                }
            }
            else
            {
                return new DictionaryToHeader(toUri, dictionaryTo, addressingVersion);
            }
        }

        public static ToHeader Create(Uri to, AddressingVersion addressingVersion)
        {
            if ((object)to == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(to));
            }
            else if ((object)to == (object)addressingVersion.AnonymousUri)
            {
                if (addressingVersion == AddressingVersion.WSAddressing10)
                {
                    return AnonymousTo10;
                }
                else
                {
                    throw new PlatformNotSupportedException($"Unsupported addressing version {addressingVersion.ToString()}");
                }
                //return AnonymousTo200408;
            }
            else
            {
                return new ToHeader(to, addressingVersion);
            }
        }

        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            writer.WriteString(to.AbsoluteUri);
        }

        public static Uri ReadHeaderValue(XmlDictionaryReader reader, AddressingVersion version)
        {
            return ReadHeaderValue(reader, version, null);
        }

        internal static Uri ReadHeaderValue(XmlDictionaryReader reader, AddressingVersion version, UriCache uriCache)
        {
            Fx.Assert(reader.IsStartElement(XD.AddressingDictionary.To, version.DictionaryNamespace), "");

            string toString = reader.ReadElementContentAsString();

            if ((object)toString == (object)version.Anonymous)
            {
                return version.AnonymousUri;
            }

            if (uriCache == null)
            {
                return new Uri(toString);
            }

            return uriCache.CreateUri(toString);
        }

        internal static ToHeader ReadHeader(XmlDictionaryReader reader, AddressingVersion version, UriCache uriCache,
            string actor, bool mustUnderstand, bool relay)
        {
            Uri to = ReadHeaderValue(reader, version, uriCache);

            if (actor.Length == 0 && mustUnderstand == mustUnderstandValue && !relay)
            {
                if ((object)to == (object)version.AnonymousUri)
                {
                    if (version == AddressingVersion.WSAddressing10)
                    {
                        return AnonymousTo10;
                    }
                    else
                    {
                        throw new PlatformNotSupportedException($"Unsupported addressing version {version}");
                    }
                    //return AnonymousTo200408;
                }
                else
                {
                    return new ToHeader(to, version);
                }
            }
            else
            {
                return new FullToHeader(to, actor, mustUnderstand, relay, version);
            }
        }

        private class AnonymousToHeader : ToHeader
        {
            public AnonymousToHeader(AddressingVersion version)
                : base(version.AnonymousUri, version)
            {
            }

            protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                writer.WriteString(Version.DictionaryAnonymous);
            }
        }

        private class DictionaryToHeader : ToHeader
        {
            private readonly XmlDictionaryString dictionaryTo;

            public DictionaryToHeader(Uri to, XmlDictionaryString dictionaryTo, AddressingVersion version)
                : base(to, version)
            {
                this.dictionaryTo = dictionaryTo;
            }

            protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                writer.WriteString(dictionaryTo);
            }
        }

        private class FullToHeader : ToHeader
        {
            private readonly string actor;
            private readonly bool mustUnderstand;
            private readonly bool relay;

            public FullToHeader(Uri to, string actor, bool mustUnderstand, bool relay, AddressingVersion version)
                : base(to, version)
            {
                this.actor = actor;
                this.mustUnderstand = mustUnderstand;
                this.relay = relay;
            }

            public override string Actor
            {
                get { return actor; }
            }

            public override bool MustUnderstand
            {
                get { return mustUnderstand; }
            }

            public override bool Relay
            {
                get { return relay; }
            }
        }
    }

    internal class ReplyToHeader : AddressingHeader
    {
        private readonly EndpointAddress replyTo;
        private const bool mustUnderstandValue = false;
        private static ReplyToHeader anonymousReplyToHeader10;

        private ReplyToHeader(EndpointAddress replyTo, AddressingVersion version)
            : base(version)
        {
            this.replyTo = replyTo;
        }

        public EndpointAddress ReplyTo
        {
            get { return replyTo; }
        }

        public override XmlDictionaryString DictionaryName
        {
            get { return XD.AddressingDictionary.ReplyTo; }
        }

        public override bool MustUnderstand
        {
            get { return mustUnderstandValue; }
        }

        public static ReplyToHeader AnonymousReplyTo10
        {
            get
            {
                if (anonymousReplyToHeader10 == null)
                {
                    anonymousReplyToHeader10 = new ReplyToHeader(EndpointAddress.AnonymousAddress, AddressingVersion.WSAddressing10);
                }

                return anonymousReplyToHeader10;
            }
        }

        //public static ReplyToHeader AnonymousReplyTo200408
        //{
        //    get
        //    {
        //        if (anonymousReplyToHeader200408 == null)
        //            anonymousReplyToHeader200408 = new ReplyToHeader(EndpointAddress.AnonymousAddress, AddressingVersion.WSAddressingAugust2004);
        //        return anonymousReplyToHeader200408;
        //    }
        //}

        public static ReplyToHeader Create(EndpointAddress replyTo, AddressingVersion addressingVersion)
        {
            if (replyTo == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(replyTo));
            }

            if (addressingVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(addressingVersion));
            }

            return new ReplyToHeader(replyTo, addressingVersion);
        }

        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            replyTo.WriteContentsTo(Version, writer);
        }

        public static ReplyToHeader ReadHeader(XmlDictionaryReader reader, AddressingVersion version,
            string actor, bool mustUnderstand, bool relay)
        {
            EndpointAddress replyTo = ReadHeaderValue(reader, version);

            if (actor.Length == 0 && mustUnderstand == mustUnderstandValue && !relay)
            {
                if ((object)replyTo == (object)EndpointAddress.AnonymousAddress)
                {
                    if (version == AddressingVersion.WSAddressing10)
                    {
                        return AnonymousReplyTo10;
                    }
                    else
                    {
                        //return AnonymousReplyTo200408;
                        throw new PlatformNotSupportedException($"Addressing version {version.ToString()} not supported");
                    }
                }
                return new ReplyToHeader(replyTo, version);
            }
            else
            {
                return new FullReplyToHeader(replyTo, actor, mustUnderstand, relay, version);
            }
        }

        public static EndpointAddress ReadHeaderValue(XmlDictionaryReader reader, AddressingVersion version)
        {
            Fx.Assert(reader.IsStartElement(XD.AddressingDictionary.ReplyTo, version.DictionaryNamespace), "");
            return EndpointAddress.ReadFrom(version, reader);
        }

        private class FullReplyToHeader : ReplyToHeader
        {
            private readonly string actor;
            private readonly bool mustUnderstand;
            private readonly bool relay;

            public FullReplyToHeader(EndpointAddress replyTo, string actor, bool mustUnderstand, bool relay, AddressingVersion version)
                : base(replyTo, version)
            {
                this.actor = actor;
                this.mustUnderstand = mustUnderstand;
                this.relay = relay;
            }

            public override string Actor
            {
                get { return actor; }
            }

            public override bool MustUnderstand
            {
                get { return mustUnderstand; }
            }

            public override bool Relay
            {
                get { return relay; }
            }
        }
    }

    internal class MessageIDHeader : AddressingHeader
    {
        private readonly UniqueId messageId;
        private const bool mustUnderstandValue = false;

        private MessageIDHeader(UniqueId messageId, AddressingVersion version)
            : base(version)
        {
            this.messageId = messageId;
        }

        public override XmlDictionaryString DictionaryName
        {
            get { return XD.AddressingDictionary.MessageId; }
        }

        public UniqueId MessageId
        {
            get { return messageId; }
        }

        public override bool MustUnderstand
        {
            get { return mustUnderstandValue; }
        }

        public static MessageIDHeader Create(UniqueId messageId, AddressingVersion addressingVersion)
        {
            if (object.ReferenceEquals(messageId, null))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageId));
            }

            if (addressingVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(addressingVersion));
            }

            return new MessageIDHeader(messageId, addressingVersion);
        }

        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            writer.WriteValue(messageId);
        }

        public static UniqueId ReadHeaderValue(XmlDictionaryReader reader, AddressingVersion version)
        {
            Fx.Assert(reader.IsStartElement(XD.AddressingDictionary.MessageId, version.DictionaryNamespace), "");
            return reader.ReadElementContentAsUniqueId();
        }

        public static MessageIDHeader ReadHeader(XmlDictionaryReader reader, AddressingVersion version,
            string actor, bool mustUnderstand, bool relay)
        {
            UniqueId messageId = ReadHeaderValue(reader, version);

            if (actor.Length == 0 && mustUnderstand == mustUnderstandValue && !relay)
            {
                return new MessageIDHeader(messageId, version);
            }
            else
            {
                return new FullMessageIDHeader(messageId, actor, mustUnderstand, relay, version);
            }
        }

        private class FullMessageIDHeader : MessageIDHeader
        {
            private readonly string actor;
            private readonly bool mustUnderstand;
            private readonly bool relay;

            public FullMessageIDHeader(UniqueId messageId, string actor, bool mustUnderstand, bool relay, AddressingVersion version)
                : base(messageId, version)
            {
                this.actor = actor;
                this.mustUnderstand = mustUnderstand;
                this.relay = relay;
            }

            public override string Actor
            {
                get { return actor; }
            }

            public override bool MustUnderstand
            {
                get { return mustUnderstand; }
            }

            public override bool Relay
            {
                get { return relay; }
            }
        }
    }

    internal class RelatesToHeader : AddressingHeader
    {
        private readonly UniqueId messageId;
        private const bool mustUnderstandValue = false;
        internal static readonly Uri ReplyRelationshipType = new Uri(Addressing10Strings.ReplyRelationship);

        private RelatesToHeader(UniqueId messageId, AddressingVersion version)
            : base(version)
        {
            this.messageId = messageId;
        }

        public override XmlDictionaryString DictionaryName
        {
            get { return XD.AddressingDictionary.RelatesTo; }
        }

        public UniqueId UniqueId
        {
            get { return messageId; }
        }

        public override bool MustUnderstand
        {
            get { return mustUnderstandValue; }
        }

        public virtual Uri RelationshipType
        {
            get { return ReplyRelationshipType; }
        }

        public static RelatesToHeader Create(UniqueId messageId, AddressingVersion addressingVersion)
        {
            if (object.ReferenceEquals(messageId, null))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageId));
            }

            if (addressingVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(addressingVersion));
            }

            return new RelatesToHeader(messageId, addressingVersion);
        }

        public static RelatesToHeader Create(UniqueId messageId, AddressingVersion addressingVersion, Uri relationshipType)
        {
            if (object.ReferenceEquals(messageId, null))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageId));
            }

            if (addressingVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(addressingVersion));
            }

            if (relationshipType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(relationshipType));
            }

            if (relationshipType == ReplyRelationshipType)
            {
                return new RelatesToHeader(messageId, addressingVersion);
            }
            else
            {
                return new FullRelatesToHeader(messageId, "", false, false, addressingVersion);
            }
        }

        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            writer.WriteValue(messageId);
        }

        public static void ReadHeaderValue(XmlDictionaryReader reader, AddressingVersion version, out Uri relationshipType, out UniqueId messageId)
        {
            AddressingDictionary addressingDictionary = XD.AddressingDictionary;

            // The RelationshipType attribute has no namespace.
            relationshipType = ReplyRelationshipType;
            /*
            string relation = reader.GetAttribute(addressingDictionary.RelationshipType, addressingDictionary.Empty);
            if (relation == null)
            {
                relationshipType = ReplyRelationshipType;
            }
            else
            {
                relationshipType = new Uri(relation);
            }
            */
            Fx.Assert(reader.IsStartElement(addressingDictionary.RelatesTo, version.DictionaryNamespace), "");
            messageId = reader.ReadElementContentAsUniqueId();
        }

        public static RelatesToHeader ReadHeader(XmlDictionaryReader reader, AddressingVersion version,
            string actor, bool mustUnderstand, bool relay)
        {
            ReadHeaderValue(reader, version, out Uri relationship, out UniqueId messageId);

            if (actor.Length == 0 && mustUnderstand == mustUnderstandValue && !relay && (object)relationship == (object)ReplyRelationshipType)
            {
                return new RelatesToHeader(messageId, version);
            }
            else
            {
                return new FullRelatesToHeader(messageId, actor, mustUnderstand, relay, version);
            }
        }

        private class FullRelatesToHeader : RelatesToHeader
        {
            private readonly string actor;
            private readonly bool mustUnderstand;
            private readonly bool relay;
            //Uri relationship;

            public FullRelatesToHeader(UniqueId messageId, string actor, bool mustUnderstand, bool relay, AddressingVersion version)
                : base(messageId, version)
            {
                //this.relationship = relationship;
                this.actor = actor;
                this.mustUnderstand = mustUnderstand;
                this.relay = relay;
            }

            public override string Actor
            {
                get { return actor; }
            }

            public override bool MustUnderstand
            {
                get { return mustUnderstand; }
            }

            /*
            public override Uri RelationshipType
            {
                get { return relationship; }
            }
            */

            public override bool Relay
            {
                get { return relay; }
            }

            protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                /*
                if ((object)relationship != (object)ReplyRelationshipType)
                {
                    // The RelationshipType attribute has no namespace.
                    writer.WriteStartAttribute(AddressingStrings.RelationshipType, AddressingStrings.Empty);
                    writer.WriteString(relationship.AbsoluteUri);
                    writer.WriteEndAttribute();
                }
                */
                writer.WriteValue(messageId);
            }
        }
    }

}