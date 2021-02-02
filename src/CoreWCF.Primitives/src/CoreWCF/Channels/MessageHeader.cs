// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    public abstract class MessageHeader : MessageHeaderInfo
    {
        private const bool DefaultRelayValue = false;
        private const bool DefaultMustUnderstandValue = false;
        private const string DefaultActorValue = "";

        public override string Actor
        {
            get { return ""; }
        }

        public override bool IsReferenceParameter
        {
            get { return false; }
        }

        public override bool MustUnderstand
        {
            get { return DefaultMustUnderstandValue; }
        }

        public override bool Relay
        {
            get { return DefaultRelayValue; }
        }

        public virtual bool IsMessageVersionSupported(MessageVersion messageVersion)
        {
            if (messageVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageVersion));
            }

            return true;
        }

        public override string ToString()
        {
            XmlWriterSettings xmlSettings = new XmlWriterSettings() { Indent = true };
            using (StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture))
            {
                using (XmlWriter textWriter = XmlWriter.Create(stringWriter, xmlSettings))
                {
                    using (XmlDictionaryWriter writer = XmlDictionaryWriter.CreateDictionaryWriter(textWriter))
                    {
                        if (IsMessageVersionSupported(MessageVersion.Soap12WSAddressing10))
                        {
                            WriteHeader(writer, MessageVersion.Soap12WSAddressing10);
                        }
                        //else if (IsMessageVersionSupported(MessageVersion.Soap12WSAddressingAugust2004))
                        //{
                        //    WriteHeader(writer, MessageVersion.Soap12WSAddressingAugust2004);
                        //}
                        //else if (IsMessageVersionSupported(MessageVersion.Soap11WSAddressing10))
                        //{
                        //    WriteHeader(writer, MessageVersion.Soap11WSAddressing10);
                        //}
                        //else if (IsMessageVersionSupported(MessageVersion.Soap11WSAddressingAugust2004))
                        //{
                        //    WriteHeader(writer, MessageVersion.Soap11WSAddressingAugust2004);
                        //}
                        //else if (IsMessageVersionSupported(MessageVersion.Soap12))
                        //{
                        //    WriteHeader(writer, MessageVersion.Soap12);
                        //}
                        else if (IsMessageVersionSupported(MessageVersion.Soap11))
                        {
                            WriteHeader(writer, MessageVersion.Soap11);
                        }
                        else
                        {
                            WriteHeader(writer, MessageVersion.None);
                        }

                        writer.Flush();
                        return stringWriter.ToString();
                    }
                }
            }
        }

        public void WriteHeader(XmlWriter writer, MessageVersion messageVersion)
        {
            WriteHeader(XmlDictionaryWriter.CreateDictionaryWriter(writer), messageVersion);
        }

        public void WriteHeader(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            if (messageVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageVersion));
            }

            OnWriteStartHeader(writer, messageVersion);
            OnWriteHeaderContents(writer, messageVersion);
            writer.WriteEndElement();
        }

        public void WriteStartHeader(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            if (messageVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageVersion));
            }

            OnWriteStartHeader(writer, messageVersion);
        }

        protected virtual void OnWriteStartHeader(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            writer.WriteStartElement(Name, Namespace);
            WriteHeaderAttributes(writer, messageVersion);
        }

        public void WriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            if (messageVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageVersion));
            }

            OnWriteHeaderContents(writer, messageVersion);
        }

        protected abstract void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion);

        protected void WriteHeaderAttributes(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            string actor = Actor;
            if (actor.Length > 0)
            {
                writer.WriteAttributeString(messageVersion.Envelope.DictionaryActor, messageVersion.Envelope.DictionaryNamespace, actor);
            }

            if (MustUnderstand)
            {
                writer.WriteAttributeString(XD.MessageDictionary.MustUnderstand, messageVersion.Envelope.DictionaryNamespace, "1");
            }

            if (Relay && messageVersion.Envelope == EnvelopeVersion.Soap12)
            {
                writer.WriteAttributeString(XD.Message12Dictionary.Relay, XD.Message12Dictionary.Namespace, "1");
            }
        }

        public static MessageHeader CreateHeader(string name, string ns, object value)
        {
            return CreateHeader(name, ns, value, DefaultMustUnderstandValue, DefaultActorValue, DefaultRelayValue);
        }

        public static MessageHeader CreateHeader(string name, string ns, object value, bool mustUnderstand)
        {
            return CreateHeader(name, ns, value, mustUnderstand, DefaultActorValue, DefaultRelayValue);
        }

        public static MessageHeader CreateHeader(string name, string ns, object value, bool mustUnderstand, string actor)
        {
            return CreateHeader(name, ns, value, mustUnderstand, actor, DefaultRelayValue);
        }

        public static MessageHeader CreateHeader(string name, string ns, object value, bool mustUnderstand, string actor, bool relay)
        {
            return new XmlObjectSerializerHeader(name, ns, value, null, mustUnderstand, actor, relay);
        }

        public static MessageHeader CreateHeader(string name, string ns, object value, XmlObjectSerializer serializer)
        {
            return CreateHeader(name, ns, value, serializer, DefaultMustUnderstandValue, DefaultActorValue, DefaultRelayValue);
        }

        public static MessageHeader CreateHeader(string name, string ns, object value, XmlObjectSerializer serializer, bool mustUnderstand)
        {
            return CreateHeader(name, ns, value, serializer, mustUnderstand, DefaultActorValue, DefaultRelayValue);
        }

        public static MessageHeader CreateHeader(string name, string ns, object value, XmlObjectSerializer serializer, bool mustUnderstand, string actor)
        {
            return CreateHeader(name, ns, value, serializer, mustUnderstand, actor, DefaultRelayValue);
        }

        public static MessageHeader CreateHeader(string name, string ns, object value, XmlObjectSerializer serializer, bool mustUnderstand, string actor, bool relay)
        {
            if (serializer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serializer));
            }

            return new XmlObjectSerializerHeader(name, ns, value, serializer, mustUnderstand, actor, relay);
        }

        internal static void GetHeaderAttributes(XmlDictionaryReader reader, MessageVersion version,
            out string actor, out bool mustUnderstand, out bool relay, out bool isReferenceParameter)
        {
            int attributeCount = reader.AttributeCount;

            if (attributeCount == 0)
            {
                mustUnderstand = false;
                actor = string.Empty;
                relay = false;
                isReferenceParameter = false;
            }
            else
            {
                string mustUnderstandString = reader.GetAttribute(XD.MessageDictionary.MustUnderstand, version.Envelope.DictionaryNamespace);
                if (mustUnderstandString != null && ToBoolean(mustUnderstandString))
                {
                    mustUnderstand = true;
                }
                else
                {
                    mustUnderstand = false;
                }

                if (mustUnderstand && attributeCount == 1)
                {
                    actor = string.Empty;
                    relay = false;
                }
                else
                {
                    actor = reader.GetAttribute(version.Envelope.DictionaryActor, version.Envelope.DictionaryNamespace);
                    if (actor == null)
                    {
                        actor = "";
                    }

                    if (version.Envelope == EnvelopeVersion.Soap12)
                    {
                        string relayString = reader.GetAttribute(XD.Message12Dictionary.Relay, version.Envelope.DictionaryNamespace);
                        if (relayString != null && ToBoolean(relayString))
                        {
                            relay = true;
                        }
                        else
                        {
                            relay = false;
                        }
                    }
                    else
                    {
                        relay = false;
                    }
                }

                isReferenceParameter = false;
                if (version.Addressing == AddressingVersion.WSAddressing10)
                {
                    string refParam = reader.GetAttribute(XD.AddressingDictionary.IsReferenceParameter, version.Addressing.DictionaryNamespace);
                    if (refParam != null)
                    {
                        isReferenceParameter = ToBoolean(refParam);
                    }
                }
            }
        }

        private static bool ToBoolean(string value)
        {
            if (value.Length == 1)
            {
                char ch = value[0];
                if (ch == '1')
                {
                    return true;
                }
                if (ch == '0')
                {
                    return false;
                }
            }
            else
            {
                if (value == "true")
                {
                    return true;
                }
                else if (value == "false")
                {
                    return false;
                }
            }
            try
            {
                return XmlConvert.ToBoolean(value);
            }
            catch (FormatException exception)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(exception.Message, null));
            }
        }
    }

    // TODO: This needed to be made public for NetTcp, investigate making it internal again
    public abstract class DictionaryHeader : MessageHeader
    {
        public override string Name
        {
            get { return DictionaryName.Value; }
        }

        public override string Namespace
        {
            get { return DictionaryNamespace.Value; }
        }

        public abstract XmlDictionaryString DictionaryName { get; }
        public abstract XmlDictionaryString DictionaryNamespace { get; }

        protected override void OnWriteStartHeader(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            writer.WriteStartElement(DictionaryName, DictionaryNamespace);
            WriteHeaderAttributes(writer, messageVersion);
        }
    }

    internal class XmlObjectSerializerHeader : MessageHeader
    {
        private XmlObjectSerializer _serializer;
        private readonly bool _mustUnderstand;
        private readonly bool _relay;
        private readonly bool _isOneTwoSupported;
        private readonly bool _isOneOneSupported;
        private readonly bool _isNoneSupported;
        private readonly object _objectToSerialize;
        private readonly string _name;
        private readonly string _ns;
        private readonly string _actor;
        private readonly object _syncRoot = new object();

        private XmlObjectSerializerHeader(XmlObjectSerializer serializer, bool mustUnderstand, string actor, bool relay)
        {
            _mustUnderstand = mustUnderstand;
            _relay = relay;
            _serializer = serializer;
            _actor = actor ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(actor));
            if (actor == EnvelopeVersion.Soap12.UltimateDestinationActor)
            {
                _isOneOneSupported = false;
                _isOneTwoSupported = true;
            }
            else if (actor == EnvelopeVersion.Soap12.NextDestinationActorValue)
            {
                _isOneOneSupported = false;
                _isOneTwoSupported = true;
            }
            else if (actor == EnvelopeVersion.Soap11.NextDestinationActorValue)
            {
                _isOneOneSupported = true;
                _isOneTwoSupported = false;
            }
            else
            {
                _isOneOneSupported = true;
                _isOneTwoSupported = true;
                _isNoneSupported = true;
            }
        }

        public XmlObjectSerializerHeader(string name, string ns, object objectToSerialize, XmlObjectSerializer serializer, bool mustUnderstand, string actor, bool relay)
            : this(serializer, mustUnderstand, actor, relay)
        {
            if (null == name)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            }

            if (name.Length == 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.SFXHeaderNameCannotBeNullOrEmpty, nameof(name)));
            }

            if (ns == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(ns));
            }
            if (ns.Length > 0)
            {
                NamingHelper.CheckUriParameter(ns, nameof(ns));
            }
            _objectToSerialize = objectToSerialize;
            _name = name;
            _ns = ns;
        }

        public override bool IsMessageVersionSupported(MessageVersion messageVersion)
        {
            if (messageVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageVersion));
            }

            if (messageVersion.Envelope == EnvelopeVersion.Soap12)
            {
                return _isOneTwoSupported;
            }
            else if (messageVersion.Envelope == EnvelopeVersion.Soap11)
            {
                return _isOneOneSupported;
            }
            else if (messageVersion.Envelope == EnvelopeVersion.None)
            {
                return _isNoneSupported;
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.EnvelopeVersionUnknown, messageVersion.Envelope.ToString())));
            }
        }

        public override string Name
        {
            get { return _name; }
        }

        public override string Namespace
        {
            get { return _ns; }
        }

        public override bool MustUnderstand
        {
            get { return _mustUnderstand; }
        }

        public override bool Relay
        {
            get { return _relay; }
        }

        public override string Actor
        {
            get { return _actor; }
        }

        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            lock (_syncRoot)
            {
                if (_serializer == null)
                {
                    _serializer = DataContractSerializerDefaults.CreateSerializer(
                        (_objectToSerialize == null ? typeof(object) : _objectToSerialize.GetType()), Name, Namespace, int.MaxValue/*maxItems*/);
                }

                _serializer.WriteObjectContent(writer, _objectToSerialize);
            }
        }
    }

    internal abstract class ReadableMessageHeader : MessageHeader
    {
        public abstract XmlDictionaryReader GetHeaderReader();

        protected override void OnWriteStartHeader(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            if (!IsMessageVersionSupported(messageVersion))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.MessageHeaderVersionNotSupported, GetType().FullName, messageVersion.ToString()), "version"));
            }

            XmlDictionaryReader reader = GetHeaderReader();
            writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
            writer.WriteAttributes(reader, false);
            reader.Dispose();
        }

        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            XmlDictionaryReader reader = GetHeaderReader();
            reader.ReadStartElement();
            while (reader.NodeType != XmlNodeType.EndElement)
            {
                writer.WriteNode(reader, false);
            }

            reader.ReadEndElement();
            reader.Dispose();
        }
    }

    internal interface IMessageHeaderWithSharedNamespace
    {
        XmlDictionaryString SharedNamespace { get; }
        XmlDictionaryString SharedPrefix { get; }
    }

    internal class BufferedHeader : ReadableMessageHeader
    {
        private readonly MessageVersion _version;
        private readonly XmlBuffer _buffer;
        private readonly int _bufferIndex;
        private readonly string _actor;
        private readonly bool _relay;
        private readonly bool _mustUnderstand;
        private readonly string _name;
        private readonly string _ns;
        private readonly bool _streamed;
        private readonly bool _isRefParam;

        public BufferedHeader(MessageVersion version, XmlBuffer buffer, int bufferIndex, string name, string ns, bool mustUnderstand, string actor, bool relay, bool isRefParam)
        {
            _version = version;
            _buffer = buffer;
            _bufferIndex = bufferIndex;
            _name = name;
            _ns = ns;
            _mustUnderstand = mustUnderstand;
            _actor = actor;
            _relay = relay;
            _isRefParam = isRefParam;
        }

        public BufferedHeader(MessageVersion version, XmlBuffer buffer, int bufferIndex, MessageHeaderInfo headerInfo)
        {
            _version = version;
            _buffer = buffer;
            _bufferIndex = bufferIndex;
            _actor = headerInfo.Actor;
            _relay = headerInfo.Relay;
            _name = headerInfo.Name;
            _ns = headerInfo.Namespace;
            _isRefParam = headerInfo.IsReferenceParameter;
            _mustUnderstand = headerInfo.MustUnderstand;
        }

        public BufferedHeader(MessageVersion version, XmlBuffer buffer, XmlDictionaryReader reader, XmlAttributeHolder[] envelopeAttributes, XmlAttributeHolder[] headerAttributes)
        {
            _streamed = true;
            _buffer = buffer;
            _version = version;
            GetHeaderAttributes(reader, version, out _actor, out _mustUnderstand, out _relay, out _isRefParam);
            _name = reader.LocalName;
            _ns = reader.NamespaceURI;
            Fx.Assert(_name != null, "");
            Fx.Assert(_ns != null, "");
            _bufferIndex = buffer.SectionCount;
            XmlDictionaryWriter writer = buffer.OpenSection(reader.Quotas);

            // Write an enclosing Envelope tag
            writer.WriteStartElement(MessageStrings.Envelope);
            if (envelopeAttributes != null)
            {
                XmlAttributeHolder.WriteAttributes(envelopeAttributes, writer);
            }

            // Write and enclosing Header tag
            writer.WriteStartElement(MessageStrings.Header);
            if (headerAttributes != null)
            {
                XmlAttributeHolder.WriteAttributes(headerAttributes, writer);
            }

            writer.WriteNode(reader, false);

            writer.WriteEndElement();
            writer.WriteEndElement();

            buffer.CloseSection();
        }

        public override string Actor
        {
            get { return _actor; }
        }

        public override bool IsReferenceParameter
        {
            get { return _isRefParam; }
        }

        public override string Name
        {
            get { return _name; }
        }

        public override string Namespace
        {
            get { return _ns; }
        }

        public override bool MustUnderstand
        {
            get { return _mustUnderstand; }
        }

        public override bool Relay
        {
            get { return _relay; }
        }

        public override bool IsMessageVersionSupported(MessageVersion messageVersion)
        {
            if (messageVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageVersion));
            }

            return messageVersion == _version;
        }

        public override XmlDictionaryReader GetHeaderReader()
        {
            XmlDictionaryReader reader = _buffer.GetReader(_bufferIndex);
            // See if we need to move past the enclosing envelope/header
            if (_streamed)
            {
                reader.MoveToContent();
                reader.Read(); // Envelope
                reader.Read(); // Header
                reader.MoveToContent();
            }
            return reader;
        }
    }
}