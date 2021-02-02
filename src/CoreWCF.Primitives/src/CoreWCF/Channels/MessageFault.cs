// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml;
using CoreWCF.Diagnostics;
using CoreWCF.Dispatcher;

namespace CoreWCF.Channels
{
    public abstract class MessageFault
    {
        internal static MessageFault CreateFault(FaultCode code, string reason)
        {
            return CreateFault(code, new FaultReason(reason));
        }

        internal static MessageFault CreateFault(FaultCode code, FaultReason reason)
        {
            return CreateFault(code, reason, null, null, "", "");
        }

        internal static MessageFault CreateFault(FaultCode code, FaultReason reason, object detail)
        {
            return CreateFault(code, reason, detail, DataContractSerializerDefaults.CreateSerializer(
                (detail == null ? typeof(object) : detail.GetType()), int.MaxValue/*maxItems*/), "", "");
        }

        public static MessageFault CreateFault(FaultCode code, FaultReason reason, object detail, XmlObjectSerializer serializer, string actor, string node)
        {
            if (code == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(code));
            }

            if (reason == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reason));
            }

            if (actor == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(actor));
            }

            if (node == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(node));
            }

            return new XmlObjectSerializerFault(code, reason, detail, serializer, actor, node);
        }

        public static MessageFault CreateFault(Message message, int maxBufferSize)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            XmlDictionaryReader reader = message.GetReaderAtBodyContents();
            using (reader)
            {
                try
                {
                    EnvelopeVersion envelopeVersion = message.Version.Envelope;
                    MessageFault fault;
                    if (envelopeVersion == EnvelopeVersion.Soap12)
                    {
                        fault = ReceivedFault.CreateFault12(reader, maxBufferSize);
                    }
                    else if (envelopeVersion == EnvelopeVersion.Soap11)
                    {
                        fault = ReceivedFault.CreateFault11(reader, maxBufferSize);
                    }
                    else if (envelopeVersion == EnvelopeVersion.None)
                    {
                        fault = ReceivedFault.CreateFaultNone(reader, maxBufferSize);
                    }
                    else
                    {
                        throw TraceUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.EnvelopeVersionUnknown, envelopeVersion.ToString())), message);
                    }
                    message.ReadFromBodyContentsToEnd(reader);
                    return fault;
                }
                catch (InvalidOperationException e)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                        SR.SFxErrorDeserializingFault, e));
                }
                catch (FormatException e)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                        SR.SFxErrorDeserializingFault, e));
                }
                catch (XmlException e)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                        SR.SFxErrorDeserializingFault, e));
                }
            }
        }

        public virtual string Actor
        {
            get
            {
                return "";
            }
        }

        public abstract FaultCode Code { get; }
        public virtual string Node
        {
            get
            {
                return "";
            }
        }

        public abstract bool HasDetail { get; }

        public abstract FaultReason Reason { get; }

        public T GetDetail<T>()
        {
            return GetDetail<T>(DataContractSerializerDefaults.CreateSerializer(typeof(T), int.MaxValue/*maxItems*/));
        }

        public T GetDetail<T>(XmlObjectSerializer serializer)
        {
            if (serializer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serializer));
            }

            XmlDictionaryReader reader = GetReaderAtDetailContents();
            T value = (T)serializer.ReadObject(reader);
            if (!reader.EOF)
            {
                reader.MoveToContent();
                if (reader.NodeType != XmlNodeType.EndElement && !reader.EOF)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.ExtraContentIsPresentInFaultDetail));
                }
            }
            return value;
        }

        public XmlDictionaryReader GetReaderAtDetailContents()
        {
            if (!HasDetail)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FaultDoesNotHaveAnyDetail));
            }

            return OnGetReaderAtDetailContents();
        }

        protected virtual void OnWriteDetail(XmlDictionaryWriter writer, EnvelopeVersion version)
        {
            OnWriteStartDetail(writer, version);
            OnWriteDetailContents(writer);
            writer.WriteEndElement();
        }

        protected virtual void OnWriteStartDetail(XmlDictionaryWriter writer, EnvelopeVersion version)
        {
            if (version == EnvelopeVersion.Soap12)
            {
                writer.WriteStartElement(XD.Message12Dictionary.FaultDetail, XD.Message12Dictionary.Namespace);
            }
            else if (version == EnvelopeVersion.Soap11)
            {
                writer.WriteStartElement(XD.Message11Dictionary.FaultDetail, XD.Message11Dictionary.FaultNamespace);
            }
            else
            {
                writer.WriteStartElement(XD.Message12Dictionary.FaultDetail, XD.MessageDictionary.Namespace);
            }
        }

        protected abstract void OnWriteDetailContents(XmlDictionaryWriter writer);

        protected virtual XmlDictionaryReader OnGetReaderAtDetailContents()
        {
            XmlBuffer detailBuffer = new XmlBuffer(int.MaxValue);
            XmlDictionaryWriter writer = detailBuffer.OpenSection(XmlDictionaryReaderQuotas.Max);
            OnWriteDetail(writer, EnvelopeVersion.Soap12);  // Wrap in soap 1.2 by default
            detailBuffer.CloseSection();
            detailBuffer.Close();
            XmlDictionaryReader reader = detailBuffer.GetReader(0);
            reader.Read(); // Skip the detail element
            return reader;
        }

        internal void WriteTo(XmlWriter writer, EnvelopeVersion version)
        {
            WriteTo(XmlDictionaryWriter.CreateDictionaryWriter(writer), version);
        }

        internal void WriteTo(XmlDictionaryWriter writer, EnvelopeVersion version)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            if (version == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(version));
            }

            if (version == EnvelopeVersion.Soap12)
            {
                WriteTo12(writer);
            }
            else if (version == EnvelopeVersion.Soap11)
            {
                WriteTo11(writer);
            }
            else if (version == EnvelopeVersion.None)
            {
                WriteToNone(writer);
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.EnvelopeVersionUnknown, version.ToString())));
            }
        }

        private void WriteToNone(XmlDictionaryWriter writer)
        {
            WriteTo12Driver(writer, EnvelopeVersion.None);
        }

        private void WriteTo12Driver(XmlDictionaryWriter writer, EnvelopeVersion version)
        {
            writer.WriteStartElement(XD.MessageDictionary.Fault, version.DictionaryNamespace);
            writer.WriteStartElement(XD.Message12Dictionary.FaultCode, version.DictionaryNamespace);
            WriteFaultCode12Driver(writer, Code, version);
            writer.WriteEndElement();
            writer.WriteStartElement(XD.Message12Dictionary.FaultReason, version.DictionaryNamespace);
            FaultReason reason = Reason;
            for (int i = 0; i < reason.Translations.Count; i++)
            {
                FaultReasonText text = reason.Translations[i];
                writer.WriteStartElement(XD.Message12Dictionary.FaultText, version.DictionaryNamespace);
                writer.WriteAttributeString("xml", "lang", XmlUtil.XmlNs, text.XmlLang);
                writer.WriteString(text.Text);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            if (Node.Length > 0)
            {
                writer.WriteElementString(XD.Message12Dictionary.FaultNode, version.DictionaryNamespace, Node);
            }

            if (Actor.Length > 0)
            {
                writer.WriteElementString(XD.Message12Dictionary.FaultRole, version.DictionaryNamespace, Actor);
            }

            if (HasDetail)
            {
                OnWriteDetail(writer, version);
            }
            writer.WriteEndElement();
        }

        private void WriteFaultCode12Driver(XmlDictionaryWriter writer, FaultCode faultCode, EnvelopeVersion version)
        {
            writer.WriteStartElement(XD.Message12Dictionary.FaultValue, version.DictionaryNamespace);
            string name;
            if (faultCode.IsSenderFault)
            {
                name = version.SenderFaultName;
            }
            else if (faultCode.IsReceiverFault)
            {
                name = version.ReceiverFaultName;
            }
            else
            {
                name = faultCode.Name;
            }

            string ns;
            if (faultCode.IsPredefinedFault)
            {
                ns = version.Namespace;
            }
            else
            {
                ns = faultCode.Namespace;
            }

            string prefix = writer.LookupPrefix(ns);
            if (prefix == null)
            {
                writer.WriteAttributeString("xmlns", "a", XmlUtil.XmlNsNs, ns);
            }

            writer.WriteQualifiedName(name, ns);
            writer.WriteEndElement();

            if (faultCode.SubCode != null)
            {
                writer.WriteStartElement(XD.Message12Dictionary.FaultSubcode, version.DictionaryNamespace);
                WriteFaultCode12Driver(writer, faultCode.SubCode, version);
                writer.WriteEndElement();
            }
        }

        private void WriteTo12(XmlDictionaryWriter writer)
        {
            WriteTo12Driver(writer, EnvelopeVersion.Soap12);
        }

        private void WriteTo11(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement(XD.MessageDictionary.Fault, XD.Message11Dictionary.Namespace);
            writer.WriteStartElement(XD.Message11Dictionary.FaultCode, XD.Message11Dictionary.FaultNamespace);

            FaultCode faultCode = Code;
            if (faultCode.SubCode != null)
            {
                faultCode = faultCode.SubCode;
            }

            string name;
            if (faultCode.IsSenderFault)
            {
                name = "Client";
            }
            else if (faultCode.IsReceiverFault)
            {
                name = "Server";
            }
            else
            {
                name = faultCode.Name;
            }

            string ns;
            if (faultCode.IsPredefinedFault)
            {
                ns = Message11Strings.Namespace;
            }
            else
            {
                ns = faultCode.Namespace;
            }

            string prefix = writer.LookupPrefix(ns);
            if (prefix == null)
            {
                writer.WriteAttributeString("xmlns", "a", XmlUtil.XmlNsNs, ns);
            }

            writer.WriteQualifiedName(name, ns);
            writer.WriteEndElement();
            FaultReasonText translation = Reason.Translations[0];
            writer.WriteStartElement(XD.Message11Dictionary.FaultString, XD.Message11Dictionary.FaultNamespace);
            if (translation.XmlLang.Length > 0)
            {
                writer.WriteAttributeString("xml", "lang", XmlUtil.XmlNs, translation.XmlLang);
            }

            writer.WriteString(translation.Text);
            writer.WriteEndElement();
            if (Actor.Length > 0)
            {
                writer.WriteElementString(XD.Message11Dictionary.FaultActor, XD.Message11Dictionary.FaultNamespace, Actor);
            }

            if (HasDetail)
            {
                OnWriteDetail(writer, EnvelopeVersion.Soap11);
            }
            writer.WriteEndElement();
        }
    }

    internal class XmlObjectSerializerFault : MessageFault
    {
        private readonly FaultCode _code;
        private readonly FaultReason _reason;
        private readonly string _actor;
        private readonly string _node;
        private readonly object _detail;
        private readonly XmlObjectSerializer _serializer;

        public XmlObjectSerializerFault(FaultCode code, FaultReason reason, object detail, XmlObjectSerializer serializer, string actor, string node)
        {
            _code = code;
            _reason = reason;
            _detail = detail;
            _serializer = serializer;
            _actor = actor;
            _node = node;
        }

        public override string Actor
        {
            get
            {
                return _actor;
            }
        }

        public override FaultCode Code
        {
            get
            {
                return _code;
            }
        }

        public override bool HasDetail
        {
            get
            {
                return _serializer != null;
            }
        }

        public override string Node
        {
            get
            {
                return _node;
            }
        }

        public override FaultReason Reason
        {
            get
            {
                return _reason;
            }
        }

        private object ThisLock
        {
            get
            {
                return _code;
            }
        }

        protected override void OnWriteDetailContents(XmlDictionaryWriter writer)
        {
            if (_serializer != null)
            {
                lock (ThisLock)
                {
                    _serializer.WriteObject(writer, _detail);
                }
            }
        }
    }

    internal class ReceivedFault : MessageFault
    {
        private readonly FaultCode _code;
        private readonly FaultReason _reason;
        private readonly string _actor;
        private readonly string _node;
        private readonly XmlBuffer _detail;
        private readonly bool _hasDetail;
        private readonly EnvelopeVersion _receivedVersion;

        private ReceivedFault(FaultCode code, FaultReason reason, string actor, string node, XmlBuffer detail, EnvelopeVersion version)
        {
            _code = code;
            _reason = reason;
            _actor = actor;
            _node = node;
            _receivedVersion = version;
            _hasDetail = InferHasDetail(detail);
            _detail = _hasDetail ? detail : null;
        }

        public override string Actor
        {
            get
            {
                return _actor;
            }
        }

        public override FaultCode Code
        {
            get
            {
                return _code;
            }
        }

        public override bool HasDetail
        {
            get
            {
                return _hasDetail;
            }
        }

        public override string Node
        {
            get
            {
                return _node;
            }
        }

        public override FaultReason Reason
        {
            get
            {
                return _reason;
            }
        }

        private bool InferHasDetail(XmlBuffer detail)
        {
            bool hasDetail = false;
            if (detail != null)
            {
                XmlDictionaryReader reader = detail.GetReader(0);
                if (!reader.IsEmptyElement && reader.Read()) // check if the detail element contains data
                {
                    hasDetail = (reader.MoveToContent() != XmlNodeType.EndElement);
                }

                reader.Dispose();
            }
            return hasDetail;
        }

        protected override void OnWriteDetail(XmlDictionaryWriter writer, EnvelopeVersion version)
        {
            using (XmlReader r = _detail.GetReader(0))
            {
                // Start the element
                base.OnWriteStartDetail(writer, version);

                // Copy the attributes
                while (r.MoveToNextAttribute())
                {
                    if (ShouldWriteDetailAttribute(version, r.Prefix, r.LocalName, r.Value))
                    {
                        writer.WriteAttributeString(r.Prefix, r.LocalName, r.NamespaceURI, r.Value);
                    }
                }
                r.MoveToElement();

                r.Read();

                // Copy the contents
                while (r.NodeType != XmlNodeType.EndElement)
                {
                    writer.WriteNode(r, false);
                }

                // End the element
                writer.WriteEndElement();
            }
        }

        protected override void OnWriteStartDetail(XmlDictionaryWriter writer, EnvelopeVersion version)
        {
            using (XmlReader r = _detail.GetReader(0))
            {
                // Start the element
                base.OnWriteStartDetail(writer, version);

                // Copy the attributes
                while (r.MoveToNextAttribute())
                {
                    if (ShouldWriteDetailAttribute(version, r.Prefix, r.LocalName, r.Value))
                    {
                        writer.WriteAttributeString(r.Prefix, r.LocalName, r.NamespaceURI, r.Value);
                    }
                }
            }
        }

        protected override void OnWriteDetailContents(XmlDictionaryWriter writer)
        {
            using (XmlReader r = _detail.GetReader(0))
            {
                r.Read();
                while (r.NodeType != XmlNodeType.EndElement)
                {
                    writer.WriteNode(r, false);
                }
            }
        }

        protected override XmlDictionaryReader OnGetReaderAtDetailContents()
        {
            XmlDictionaryReader reader = _detail.GetReader(0);
            reader.Read(); // Skip the detail element
            return reader;
        }

        private bool ShouldWriteDetailAttribute(EnvelopeVersion targetVersion, string prefix, string localName, string attributeValue)
        {
            // Handle fault detail version conversion from Soap12 to Soap11 -- scope tightly to only conversion from Soap12 -> Soap11
            // SOAP 1.1 specifications allow an arbitrary element within <fault>, hence: 
            // transform this IFF the SOAP namespace specified will affect the namespace of the <detail> element, 
            // AND the namespace specified is exactly the Soap12 Namespace. 
            bool shouldSkip = _receivedVersion == EnvelopeVersion.Soap12    // original incoming version
                                && targetVersion == EnvelopeVersion.Soap11      // version to serialize to
                                && string.IsNullOrEmpty(prefix)                 // attribute prefix
                                && localName == "xmlns"                         // only transform namespace attributes, don't care about others
                                && attributeValue == XD.Message12Dictionary.Namespace.Value;

            return !shouldSkip;
        }

        public static ReceivedFault CreateFaultNone(XmlDictionaryReader reader, int maxBufferSize)
        {
            return CreateFault12Driver(reader, maxBufferSize, EnvelopeVersion.None);
        }

        private static ReceivedFault CreateFault12Driver(XmlDictionaryReader reader, int maxBufferSize, EnvelopeVersion version)
        {
            reader.ReadStartElement(XD.MessageDictionary.Fault, version.DictionaryNamespace);
            reader.ReadStartElement(XD.Message12Dictionary.FaultCode, version.DictionaryNamespace);
            FaultCode code = ReadFaultCode12Driver(reader, version);
            reader.ReadEndElement();
            List<FaultReasonText> translations = new List<FaultReasonText>();
            if (reader.IsEmptyElement)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.AtLeastOneFaultReasonMustBeSpecified));
            }
            else
            {
                reader.ReadStartElement(XD.Message12Dictionary.FaultReason, version.DictionaryNamespace);
                while (reader.IsStartElement(XD.Message12Dictionary.FaultText, version.DictionaryNamespace))
                {
                    translations.Add(ReadTranslation12(reader));
                }

                reader.ReadEndElement();
            }

            string actor = "";
            string node = "";
            if (reader.IsStartElement(XD.Message12Dictionary.FaultNode, version.DictionaryNamespace))
            {
                node = reader.ReadElementContentAsString();
            }

            if (reader.IsStartElement(XD.Message12Dictionary.FaultRole, version.DictionaryNamespace))
            {
                actor = reader.ReadElementContentAsString();
            }

            XmlBuffer detail = null;
            if (reader.IsStartElement(XD.Message12Dictionary.FaultDetail, version.DictionaryNamespace))
            {
                detail = new XmlBuffer(maxBufferSize);
                XmlDictionaryWriter writer = detail.OpenSection(reader.Quotas);
                writer.WriteNode(reader, false);
                detail.CloseSection();
                detail.Close();
            }
            reader.ReadEndElement();
            FaultReason reason = new FaultReason(translations);
            return new ReceivedFault(code, reason, actor, node, detail, version);
        }

        private static FaultCode ReadFaultCode12Driver(XmlDictionaryReader reader, EnvelopeVersion version)
        {
            reader.ReadStartElement(XD.Message12Dictionary.FaultValue, version.DictionaryNamespace);
            XmlUtil.ReadContentAsQName(reader, out string localName, out string ns);
            reader.ReadEndElement();
            if (reader.IsStartElement(XD.Message12Dictionary.FaultSubcode, version.DictionaryNamespace))
            {
                reader.ReadStartElement();
                FaultCode subCode = ReadFaultCode12Driver(reader, version);
                reader.ReadEndElement();
                return new FaultCode(localName, ns, subCode);
            }
            return new FaultCode(localName, ns);
        }

        public static ReceivedFault CreateFault12(XmlDictionaryReader reader, int maxBufferSize)
        {
            return CreateFault12Driver(reader, maxBufferSize, EnvelopeVersion.Soap12);
        }

        private static FaultReasonText ReadTranslation12(XmlDictionaryReader reader)
        {
            string xmlLang = XmlUtil.GetXmlLangAttribute(reader);
            string text = reader.ReadElementContentAsString();
            return new FaultReasonText(text, xmlLang);
        }

        public static ReceivedFault CreateFault11(XmlDictionaryReader reader, int maxBufferSize)
        {
            reader.ReadStartElement(XD.MessageDictionary.Fault, XD.Message11Dictionary.Namespace);
            reader.ReadStartElement(XD.Message11Dictionary.FaultCode, XD.Message11Dictionary.FaultNamespace);
            XmlUtil.ReadContentAsQName(reader, out string name, out string ns);
            FaultCode code = new FaultCode(name, ns);
            reader.ReadEndElement();

            string xmlLang = reader.XmlLang;
            reader.MoveToContent();  // Don't do IsStartElement.  FaultString is required, so let the reader throw.
            string text = reader.ReadElementContentAsString(XD.Message11Dictionary.FaultString.Value, XD.Message11Dictionary.FaultNamespace.Value);
            FaultReasonText translation = new FaultReasonText(text, xmlLang);

            string actor = "";
            if (reader.IsStartElement(XD.Message11Dictionary.FaultActor, XD.Message11Dictionary.FaultNamespace))
            {
                actor = reader.ReadElementContentAsString();
            }

            XmlBuffer detail = null;
            if (reader.IsStartElement(XD.Message11Dictionary.FaultDetail, XD.Message11Dictionary.FaultNamespace))
            {
                detail = new XmlBuffer(maxBufferSize);
                XmlDictionaryWriter writer = detail.OpenSection(reader.Quotas);
                writer.WriteNode(reader, false);
                detail.CloseSection();
                detail.Close();
            }
            reader.ReadEndElement();
            FaultReason reason = new FaultReason(translation);
            return new ReceivedFault(code, reason, actor, actor, detail, EnvelopeVersion.Soap11);
        }
    }
}