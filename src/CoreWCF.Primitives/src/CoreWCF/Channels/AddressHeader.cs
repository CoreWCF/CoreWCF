// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    public abstract class AddressHeader
    {
        private ParameterHeader _header;

        protected AddressHeader()
        {
        }

        internal bool IsReferenceProperty
        {
            get
            {
                BufferedAddressHeader bah = this as BufferedAddressHeader;
                return bah != null && bah.IsReferencePropertyHeader;
            }
        }

        public abstract string Name { get; }
        public abstract string Namespace { get; }

        //public static AddressHeader CreateAddressHeader(object value)
        //{
        //    Type type = GetObjectType(value);
        //    return CreateAddressHeader(value, DataContractSerializerDefaults.CreateSerializer(type, int.MaxValue/*maxItems*/));
        //}

        //public static AddressHeader CreateAddressHeader(object value, XmlObjectSerializer serializer)
        //{
        //    if (serializer == null)
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("serializer"));
        //    return new XmlObjectSerializerAddressHeader(value, serializer);
        //}

        public static AddressHeader CreateAddressHeader(string name, string ns, object value)
        {
            return CreateAddressHeader(name, ns, value, DataContractSerializerDefaults.CreateSerializer(GetObjectType(value), name, ns, int.MaxValue/*maxItems*/));
        }

        internal static AddressHeader CreateAddressHeader(XmlDictionaryString name, XmlDictionaryString ns, object value)
        {
            return new DictionaryAddressHeader(name, ns, value);
        }

        public static AddressHeader CreateAddressHeader(string name, string ns, object value, XmlObjectSerializer serializer)
        {
            if (serializer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serializer));
            }

            return new XmlObjectSerializerAddressHeader(name, ns, value, serializer);
        }

        private static Type GetObjectType(object value)
        {
            return (value == null) ? typeof(object) : value.GetType();
        }

        public override bool Equals(object obj)
        {
            AddressHeader hdr = obj as AddressHeader;
            if (hdr == null)
            {
                return false;
            }

            StringBuilder builder = new StringBuilder();
            string hdr1 = GetComparableForm(builder);

            builder.Remove(0, builder.Length);
            string hdr2 = hdr.GetComparableForm(builder);

            if (hdr1.Length != hdr2.Length)
            {
                return false;
            }

            if (string.CompareOrdinal(hdr1, hdr2) != 0)
            {
                return false;
            }

            return true;
        }

        internal string GetComparableForm()
        {
            return GetComparableForm(new StringBuilder());
        }

        internal string GetComparableForm(StringBuilder builder)
        {
            return EndpointAddressProcessor.GetComparableForm(builder, GetComparableReader());
        }

        public override int GetHashCode()
        {
            return GetComparableForm().GetHashCode();
        }

        public T GetValue<T>()
        {
            return GetValue<T>(DataContractSerializerDefaults.CreateSerializer(typeof(T), Name, Namespace, int.MaxValue/*maxItems*/));
        }

        public T GetValue<T>(XmlObjectSerializer serializer)
        {
            if (serializer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serializer));
            }

            using (XmlDictionaryReader reader = GetAddressHeaderReader())
            {
                if (serializer.IsStartObject(reader))
                {
                    return (T)serializer.ReadObject(reader);
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.ExpectedElementMissing, Name, Namespace)));
                }
            }
        }

        public virtual XmlDictionaryReader GetAddressHeaderReader()
        {
            XmlBuffer buffer = new XmlBuffer(int.MaxValue);
            XmlDictionaryWriter writer = buffer.OpenSection(XmlDictionaryReaderQuotas.Max);
            WriteAddressHeader(writer);
            buffer.CloseSection();
            buffer.Close();
            return buffer.GetReader(0);
        }

        private XmlDictionaryReader GetComparableReader()
        {
            XmlBuffer buffer = new XmlBuffer(int.MaxValue);
            XmlDictionaryWriter writer = buffer.OpenSection(XmlDictionaryReaderQuotas.Max);
            // WSAddressingAugust2004 does not write the IsReferenceParameter attribute, 
            // and that's good for a consistent comparable form. But it's not available for .Net Core
            ParameterHeader.WriteStartHeader(writer, this, AddressingVersion.WSAddressing10);
            //ParameterHeader.WriteStartHeader(writer, this, AddressingVersion.WSAddressingAugust2004);
            ParameterHeader.WriteHeaderContents(writer, this);
            writer.WriteEndElement();
            buffer.CloseSection();
            buffer.Close();
            return buffer.GetReader(0);
        }

        protected virtual void OnWriteStartAddressHeader(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement(Name, Namespace);
        }

        protected abstract void OnWriteAddressHeaderContents(XmlDictionaryWriter writer);

        public MessageHeader ToMessageHeader()
        {
            if (_header == null)
            {
                _header = new ParameterHeader(this);
            }

            return _header;
        }

        public void WriteAddressHeader(XmlWriter writer)
        {
            WriteAddressHeader(XmlDictionaryWriter.CreateDictionaryWriter(writer));
        }

        public void WriteAddressHeader(XmlDictionaryWriter writer)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            WriteStartAddressHeader(writer);
            WriteAddressHeaderContents(writer);
            writer.WriteEndElement();
        }

        public void WriteStartAddressHeader(XmlDictionaryWriter writer)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            OnWriteStartAddressHeader(writer);
        }

        public void WriteAddressHeaderContents(XmlDictionaryWriter writer)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            OnWriteAddressHeaderContents(writer);
        }

        private class ParameterHeader : MessageHeader
        {
            private readonly AddressHeader _parameter;

            public override bool IsReferenceParameter
            {
                get { return true; }
            }

            public override string Name
            {
                get { return _parameter.Name; }
            }

            public override string Namespace
            {
                get { return _parameter.Namespace; }
            }

            public ParameterHeader(AddressHeader parameter)
            {
                _parameter = parameter;
            }

            protected override void OnWriteStartHeader(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                if (messageVersion == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageVersion));
                }

                WriteStartHeader(writer, _parameter, messageVersion.Addressing);
            }

            protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                WriteHeaderContents(writer, _parameter);
            }

            internal static void WriteStartHeader(XmlDictionaryWriter writer, AddressHeader parameter, AddressingVersion addressingVersion)
            {
                parameter.WriteStartAddressHeader(writer);
                if (addressingVersion == AddressingVersion.WSAddressing10)
                {
                    writer.WriteAttributeString(XD.AddressingDictionary.IsReferenceParameter, XD.Addressing10Dictionary.Namespace, "true");
                }
            }

            internal static void WriteHeaderContents(XmlDictionaryWriter writer, AddressHeader parameter)
            {
                parameter.WriteAddressHeaderContents(writer);
            }
        }

        private class XmlObjectSerializerAddressHeader : AddressHeader
        {
            private readonly XmlObjectSerializer _serializer;
            private readonly object _objectToSerialize;
            private readonly string _name;
            private readonly string _ns;

            public XmlObjectSerializerAddressHeader(object objectToSerialize, XmlObjectSerializer serializer)
            {
                _serializer = serializer;
                _objectToSerialize = objectToSerialize;

                throw new PlatformNotSupportedException();

                //Type type = (objectToSerialize == null) ? typeof(object) : objectToSerialize.GetType();
                //XmlQualifiedName rootName = new XsdDataContractExporter().GetRootElementName(type);
                //this.name = rootName.Name;
                //this.ns = rootName.Namespace;
            }

            public XmlObjectSerializerAddressHeader(string name, string ns, object objectToSerialize, XmlObjectSerializer serializer)
            {
                if ((null == name) || (name.Length == 0))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
                }

                _serializer = serializer;
                _objectToSerialize = objectToSerialize;
                _name = name;
                _ns = ns;
            }

            public override string Name
            {
                get { return _name; }
            }

            public override string Namespace
            {
                get { return _ns; }
            }

            private object ThisLock
            {
                get { return this; }
            }

            protected override void OnWriteAddressHeaderContents(XmlDictionaryWriter writer)
            {
                lock (ThisLock)
                {
                    _serializer.WriteObjectContent(writer, _objectToSerialize);
                }
            }
        }

        // astern, This will be kept internal for now.  If the optimization needs to be public, we'll re-evaluate it.
        private class DictionaryAddressHeader : XmlObjectSerializerAddressHeader
        {
            private readonly XmlDictionaryString _name;
            private readonly XmlDictionaryString _ns;

            public DictionaryAddressHeader(XmlDictionaryString name, XmlDictionaryString ns, object value)
                : base(name.Value, ns.Value, value, DataContractSerializerDefaults.CreateSerializer(GetObjectType(value), name, ns, int.MaxValue/*maxItems*/))
            {
                _name = name;
                _ns = ns;
            }

            protected override void OnWriteStartAddressHeader(XmlDictionaryWriter writer)
            {
                writer.WriteStartElement(_name, _ns);
            }
        }
    }

    internal class BufferedAddressHeader : AddressHeader
    {
        private readonly string _name;
        private readonly string _ns;
        private readonly XmlBuffer _buffer;

        public BufferedAddressHeader(XmlDictionaryReader reader)
        {
            _buffer = new XmlBuffer(int.MaxValue);
            XmlDictionaryWriter writer = _buffer.OpenSection(reader.Quotas);
            Fx.Assert(reader.NodeType == XmlNodeType.Element, "");
            _name = reader.LocalName;
            _ns = reader.NamespaceURI;
            Fx.Assert(_name != null, "");
            Fx.Assert(_ns != null, "");
            writer.WriteNode(reader, false);
            _buffer.CloseSection();
            _buffer.Close();
            IsReferencePropertyHeader = false;
        }

        public BufferedAddressHeader(XmlDictionaryReader reader, bool isReferenceProperty)
            : this(reader)
        {
            IsReferencePropertyHeader = isReferenceProperty;
        }

        public bool IsReferencePropertyHeader { get; }

        public override string Name
        {
            get { return _name; }
        }

        public override string Namespace
        {
            get { return _ns; }
        }

        public override XmlDictionaryReader GetAddressHeaderReader()
        {
            return _buffer.GetReader(0);
        }

        protected override void OnWriteStartAddressHeader(XmlDictionaryWriter writer)
        {
            XmlDictionaryReader reader = GetAddressHeaderReader();
            writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
            writer.WriteAttributes(reader, false);
            reader.Dispose();
        }

        protected override void OnWriteAddressHeaderContents(XmlDictionaryWriter writer)
        {
            XmlDictionaryReader reader = GetAddressHeaderReader();
            reader.ReadStartElement();
            while (reader.NodeType != XmlNodeType.EndElement)
            {
                writer.WriteNode(reader, false);
            }

            reader.ReadEndElement();
            reader.Dispose();
        }
    }
}