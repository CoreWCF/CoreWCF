// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF
{
    public class EndpointAddress
    {
        private static Uri s_anonymousUri;
        private static Uri s_noneUri;
        private static EndpointAddress s_anonymousAddress;

        /*
        Conceptually, the agnostic EndpointAddress class represents all of UNION(v200408,v10) data thusly:
         - Address Uri (both versions - the Address)
         - AddressHeaderCollection (both versions - RefProp&RefParam both project into here)
         - PSP blob (200408 - this is PortType, ServiceName, Policy, it is not surfaced in OM)
         - metadata (both versions, but weird semantics in 200408)
         - identity (both versions, this is the one 'extension' that we know about)
         - extensions (both versions, the "any*" stuff at the end)
 
        When reading from 200408:
         - Address is projected into Uri
         - both RefProps and RefParams are projected into AddressHeaderCollection, 
              they (internally) remember 'which kind' they are
         - PortType, ServiceName, Policy are projected into the (internal) PSP blob
         - if we see a wsx:metadata element next, we project that element and that element only into the metadata reader
         - we read the rest, recognizing and fishing out identity if there, projecting rest to extensions reader
        When reading from 10:
         - Address is projected into Uri
         - RefParams are projected into AddressHeaderCollection; they (internally) remember 'which kind' they are
         - nothing is projected into the (internal) PSP blob (it's empty)
         - if there's a wsa10:metadata element, everything inside it projects into metadatareader
         - we read the rest, recognizing and fishing out identity if there, projecting rest to extensions reader
 
        When writing to 200408:
         - Uri is written as Address
         - AddressHeaderCollection is written as RefProps & RefParams, based on what they internally remember selves to be
         - PSP blob is written out verbatim (will have: PortType?, ServiceName?, Policy?)
         - metadata reader is written out verbatim
         - identity is written out as extension
         - extension reader is written out verbatim
        When writing to 10:
         - Uri is written as Address
         - AddressHeaderCollection is all written as RefParams, regardless of what they internally remember selves to be
         - PSP blob is ignored
         - if metadata reader is non-empty, we write its value out verbatim inside a wsa10:metadata element
         - identity is written out as extension
         - extension reader is written out verbatim
 
        EndpointAddressBuilder:
         - you can set metadata to any value you like; we don't (cannot) validate because 10 allows anything
         - you can set any extensions you like
 
        Known Weirdnesses:
         - PSP blob does not surface in OM - it can only roundtrip 200408wire->OM->200408wire
         - RefProperty distinction does not surface in OM - it can only roundtrip 200408wire->OM->200408wire
         - regardless of what metadata in reader, when you roundtrip OM->200408wire->OM, only wsx:metadata
               as first element after PSP will stay in metadata, anything else gets dumped in extensions
         - PSP blob is lost when doing OM->10wire->OM
         - RefProps turn into RefParams when doing OM->10wire->OM
         - Identity is always shuffled to front of extensions when doing anyWire->OM->anyWire
        */

        private AddressingVersion _addressingVersion;
        private AddressHeaderCollection _headers;
        private int _extensionSection;
        private int _metadataSection;
        private int _pspSection;

        // these are the element name/namespace for the dummy wrapper element that wraps each buffer section
        internal const string DummyName = "Dummy";
        internal const string DummyNamespace = "http://Dummy";

        private EndpointAddress(AddressingVersion version, Uri uri, EndpointIdentity identity, AddressHeaderCollection headers, XmlBuffer buffer, int metadataSection, int extensionSection, int pspSection)
        {
            Init(version, uri, identity, headers, buffer, metadataSection, extensionSection, pspSection);
        }

        public EndpointAddress(string uri)
        {
            if (uri == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(uri));
            }

            Uri u = new Uri(uri);

            Init(u, (EndpointIdentity)null, (AddressHeaderCollection)null, null, -1, -1, -1);
        }

        public EndpointAddress(Uri uri, params AddressHeader[] addressHeaders)
            : this(uri, (EndpointIdentity)null, addressHeaders)
        {
        }

        public EndpointAddress(Uri uri, EndpointIdentity identity, params AddressHeader[] addressHeaders)
        {
            if (uri == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(uri));
            }

            Init(uri, identity, addressHeaders);
        }

        public EndpointAddress(Uri newUri, EndpointAddress oldEndpointAddress)
        {
            Init(oldEndpointAddress._addressingVersion, newUri, oldEndpointAddress.Identity, oldEndpointAddress._headers, oldEndpointAddress.Buffer, oldEndpointAddress._metadataSection, oldEndpointAddress._extensionSection, oldEndpointAddress._pspSection);
        }

        //public EndpointAddress(Uri uri, EndpointIdentity identity, AddressHeaderCollection headers)
        //{
        //    if (uri == null)
        //    {
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(uri));
        //    }

        //    Init(uri, identity, headers, null, -1, -1, -1);
        //}

        internal EndpointAddress(Uri uri, EndpointIdentity identity, AddressHeaderCollection headers, XmlDictionaryReader metadataReader, XmlDictionaryReader extensionReader, XmlDictionaryReader pspReader)
        {
            if (uri == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(uri));
            }

            XmlBuffer buffer = null;
            PossiblyPopulateBuffer(metadataReader, ref buffer, out _metadataSection);

            buffer = ReadExtensions(extensionReader, null, buffer, out EndpointIdentity ident2, out int extSection);

            if (identity != null && ident2 != null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.MultipleIdentities, nameof(extensionReader)));
            }

            PossiblyPopulateBuffer(pspReader, ref buffer, out _pspSection);

            if (buffer != null)
            {
                buffer.Close();
            }

            Init(uri, identity ?? ident2, headers, buffer, _metadataSection, extSection, _pspSection);
        }

        // metadataReader and extensionReader are assumed to not have a starting dummy wrapper element
        public EndpointAddress(Uri uri, EndpointIdentity identity, AddressHeaderCollection headers, XmlDictionaryReader metadataReader, XmlDictionaryReader extensionReader)
            : this(uri, identity, headers, metadataReader, extensionReader, null)
        {
        }

        private void Init(Uri uri, EndpointIdentity identity, AddressHeader[] headers)
        {
            if (headers == null || headers.Length == 0)
            {
                Init(uri, identity, (AddressHeaderCollection)null, null, -1, -1, -1);
            }
            else
            {
                Init(uri, identity, new AddressHeaderCollection(headers), null, -1, -1, -1);
            }
        }

        private void Init(Uri uri, EndpointIdentity identity, AddressHeaderCollection headers, XmlBuffer buffer, int metadataSection, int extensionSection, int pspSection)
        {
            Init(null, uri, identity, headers, buffer, metadataSection, extensionSection, pspSection);
        }

        private void Init(AddressingVersion version, Uri uri, EndpointIdentity identity, AddressHeaderCollection headers, XmlBuffer buffer, int metadataSection, int extensionSection, int pspSection)
        {
            if (!uri.IsAbsoluteUri)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(uri), SR.UriMustBeAbsolute);
            }

            _addressingVersion = version;
            Uri = uri;
            Identity = identity;
            _headers = headers;
            Buffer = buffer;
            _metadataSection = metadataSection;
            _extensionSection = extensionSection;
            _pspSection = pspSection;

            if (version != null)
            {
                IsAnonymous = uri == version.AnonymousUri;
                IsNone = uri == version.NoneUri;
            }
            else
            {
                IsAnonymous = ReferenceEquals(uri, AnonymousUri) || uri == AnonymousUri;
                IsNone = ReferenceEquals(uri, NoneUri) || uri == NoneUri;
            }
            if (IsAnonymous)
            {
                Uri = AnonymousUri;
            }
            if (IsNone)
            {
                Uri = NoneUri;
            }
        }

        // TODO: This was internal, I needed to make it public. Is this a problem?
        public static EndpointAddress AnonymousAddress
        {
            get
            {
                if (s_anonymousAddress == null)
                {
                    s_anonymousAddress = new EndpointAddress(AnonymousUri);
                }

                return s_anonymousAddress;
            }
        }

        public static Uri AnonymousUri
        {
            get
            {
                if (s_anonymousUri == null)
                {
                    s_anonymousUri = new Uri(AddressingStrings.AnonymousUri);
                }

                return s_anonymousUri;
            }
        }

        public static Uri NoneUri
        {
            get
            {
                if (s_noneUri == null)
                {
                    s_noneUri = new Uri(AddressingStrings.NoneUri);
                }

                return s_noneUri;
            }
        }

        internal XmlBuffer Buffer { get; private set; }

        public AddressHeaderCollection Headers
        {
            get
            {
                if (_headers == null)
                {
                    _headers = new AddressHeaderCollection();
                }

                return _headers;
            }
        }

        public EndpointIdentity Identity { get; private set; }

        public bool IsAnonymous { get; private set; }

        public bool IsNone { get; private set; }

        public Uri Uri { get; private set; }

        public void ApplyTo(Message message)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            Uri uri = Uri;
            if (IsAnonymous)
            {
                if (message.Version.Addressing == AddressingVersion.WSAddressing10)
                {
                    message.Headers.To = null;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new ProtocolException(SR.Format(SR.AddressingVersionNotSupported, message.Version.Addressing)));
                }
            }
            else if (IsNone)
            {
                message.Headers.To = message.Version.Addressing.NoneUri;
            }
            else
            {
                message.Headers.To = uri;
            }
            message.Properties.Via = message.Headers.To;
            if (_headers != null)
            {
                _headers.AddHeadersTo(message);
            }
        }

        // NOTE: UserInfo, Query, and Fragment are ignored when comparing Uris as addresses
        // this is the WCF logic for comparing Uris that represent addresses
        // this method must be kept in sync with UriGetHashCode
        internal static bool UriEquals(Uri u1, Uri u2, bool ignoreCase, bool includeHostInComparison)
        {
            return UriEquals(u1, u2, ignoreCase, includeHostInComparison, true, true);
        }

        internal static bool UriEquals(Uri u1, Uri u2, bool ignoreCase, bool includeHostInComparison, bool includePortInComparison, bool includeSchemeInComparison)
        {
            // PERF: Equals compares everything but UserInfo and Fragments.  It's more strict than
            //       we are, and faster, so it is done first.
            if (u1.Equals(u2))
            {
                return true;
            }

            if (includeSchemeInComparison)
            {
                if (u1.Scheme != u2.Scheme)  // Uri.Scheme is always lowercase
                {
                    return false;
                }
            }

            if (includePortInComparison)
            {
                if (u1.Port != u2.Port)
                {
                    return false;
                }
            }
            if (includeHostInComparison)
            {
                if (string.Compare(u1.Host, u2.Host, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    return false;
                }
            }

            if (string.Compare(u1.AbsolutePath, u2.AbsolutePath, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) == 0)
            {
                return true;
            }

            // Normalize for trailing slashes
            string u1Path = u1.GetComponents(UriComponents.Path, UriFormat.Unescaped);
            string u2Path = u2.GetComponents(UriComponents.Path, UriFormat.Unescaped);
            int u1Len = (u1Path.Length > 0 && u1Path[u1Path.Length - 1] == '/') ? u1Path.Length - 1 : u1Path.Length;
            int u2Len = (u2Path.Length > 0 && u2Path[u2Path.Length - 1] == '/') ? u2Path.Length - 1 : u2Path.Length;
            if (u2Len != u1Len)
            {
                return false;
            }
            return string.Compare(u1Path, 0, u2Path, 0, u1Len, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) == 0;
        }

        // this method must be kept in sync with UriEquals
        internal static int UriGetHashCode(Uri uri, bool includeHostInComparison)
        {
            return UriGetHashCode(uri, includeHostInComparison, true);
        }

        internal static int UriGetHashCode(Uri uri, bool includeHostInComparison, bool includePortInComparison)
        {
            UriComponents components = UriComponents.Scheme | UriComponents.Path;

            if (includePortInComparison)
            {
                components = components | UriComponents.Port;
            }
            if (includeHostInComparison)
            {
                components = components | UriComponents.Host;
            }

            // Normalize for trailing slashes
            string uriString = uri.GetComponents(components, UriFormat.Unescaped);
            if (uriString.Length > 0 && uriString[uriString.Length - 1] != '/')
            {
                uriString = string.Concat(uriString, "/");
            }

            return StringComparer.OrdinalIgnoreCase.GetHashCode(uriString);
        }

        internal bool EndpointEquals(EndpointAddress endpointAddress)
        {
            if (endpointAddress == null)
            {
                return false;
            }

            if (ReferenceEquals(this, endpointAddress))
            {
                return true;
            }

            Uri thisTo = Uri;
            Uri otherTo = endpointAddress.Uri;

            if (!UriEquals(thisTo, otherTo, false /* ignoreCase */, true /* includeHostInComparison */))
            {
                return false;
            }

            if (Identity == null)
            {
                if (endpointAddress.Identity != null)
                {
                    return false;
                }
            }
            else if (!Identity.Equals(endpointAddress.Identity))
            {
                return false;
            }

            if (!Headers.IsEquivalent(endpointAddress.Headers))
            {
                return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, this))
            {
                return true;
            }

            if (obj == null)
            {
                return false;
            }

            EndpointAddress address = obj as EndpointAddress;
            if (address == null)
            {
                return false;
            }

            return EndpointEquals(address);
        }

        public override int GetHashCode()
        {
            return UriGetHashCode(Uri, true /* includeHostInComparison */);
        }

        // returns reader without starting dummy wrapper element
        internal XmlDictionaryReader GetReaderAtPsp()
        {
            return GetReaderAtSection(Buffer, _pspSection);
        }

        //// returns reader without starting dummy wrapper element
        internal XmlDictionaryReader GetReaderAtMetadata()
        {
            return GetReaderAtSection(Buffer, _metadataSection);
        }

        //// returns reader without starting dummy wrapper element
        internal XmlDictionaryReader GetReaderAtExtensions()
        {
            return GetReaderAtSection(Buffer, _extensionSection);
        }

        private static XmlDictionaryReader GetReaderAtSection(XmlBuffer buffer, int section)
        {
            if (buffer == null || section < 0)
            {
                return null;
            }

            XmlDictionaryReader reader = buffer.GetReader(section);
            reader.MoveToContent();
            Fx.Assert(reader.Name == DummyName, "EndpointAddress: Expected dummy element not found");
            reader.Read(); // consume the dummy wrapper element
            return reader;
        }

        private void PossiblyPopulateBuffer(XmlDictionaryReader reader, ref XmlBuffer buffer, out int section)
        {
            if (reader == null)
            {
                section = -1;
            }
            else
            {
                if (buffer == null)
                {
                    buffer = new XmlBuffer(short.MaxValue);
                }
                section = buffer.SectionCount;
                XmlDictionaryWriter writer = buffer.OpenSection(reader.Quotas);
                writer.WriteStartElement(DummyName, DummyNamespace);
                Copy(writer, reader);
                buffer.CloseSection();
            }
        }

        public static EndpointAddress ReadFrom(XmlDictionaryReader reader)
        {
            AddressingVersion dummyVersion;
            return ReadFrom(reader, out dummyVersion);
        }

        internal static EndpointAddress ReadFrom(XmlDictionaryReader reader, out AddressingVersion version)
        {
            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }

            reader.ReadFullStartElement();
            reader.MoveToContent();

            if (reader.IsNamespaceUri(AddressingVersion.WSAddressing10.DictionaryNamespace))
            {
                version = AddressingVersion.WSAddressing10;
            }
            else if (reader.IsNamespaceUri(AddressingVersion.WSAddressingAugust2004.DictionaryNamespace))
            {
                version = AddressingVersion.WSAddressingAugust2004;
            }
            else if (reader.NodeType != XmlNodeType.Element)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(
                    nameof(reader), SR.CannotDetectAddressingVersion);
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(
                    nameof(reader), SR.Format(SR.AddressingVersionNotSupported, reader.NamespaceURI));
            }

            EndpointAddress ea = ReadFromDriver(version, reader);
            reader.ReadEndElement();
            return ea;
        }

        //public static EndpointAddress ReadFrom(XmlDictionaryReader reader, XmlDictionaryString localName, XmlDictionaryString ns)
        //{
        //    AddressingVersion version;
        //    return ReadFrom(reader, localName, ns, out version);
        //}

        internal static EndpointAddress ReadFrom(XmlDictionaryReader reader, XmlDictionaryString localName, XmlDictionaryString ns, out AddressingVersion version)
        {
            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }

            reader.ReadFullStartElement(localName, ns);
            reader.MoveToContent();

            if (reader.IsNamespaceUri(AddressingVersion.WSAddressing10.DictionaryNamespace))
            {
                version = AddressingVersion.WSAddressing10;
            }
            else if (reader.IsNamespaceUri(AddressingVersion.WSAddressingAugust2004.DictionaryNamespace))
            {
                version = AddressingVersion.WSAddressingAugust2004;
            }
            else if (reader.NodeType != XmlNodeType.Element)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(
                    nameof(reader), SR.CannotDetectAddressingVersion);
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(
                    nameof(reader), SR.Format(SR.AddressingVersionNotSupported, reader.NamespaceURI));
            }

            EndpointAddress ea = ReadFromDriver(version, reader);
            reader.ReadEndElement();
            return ea;
        }

        //public static EndpointAddress ReadFrom(AddressingVersion addressingVersion, XmlReader reader)
        //{
        //    return ReadFrom(addressingVersion, XmlDictionaryReader.CreateDictionaryReader(reader));
        //}

        //public static EndpointAddress ReadFrom(AddressingVersion addressingVersion, XmlReader reader, string localName, string ns)
        //{
        //    if (reader == null)
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
        //    if (addressingVersion == null)
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(addressingVersion));

        //    XmlDictionaryReader dictReader = XmlDictionaryReader.CreateDictionaryReader(reader);
        //    dictReader.ReadFullStartElement(localName, ns);
        //    EndpointAddress ea = ReadFromDriver(addressingVersion, dictReader);
        //    reader.ReadEndElement();
        //    return ea;
        //}

        public static EndpointAddress ReadFrom(AddressingVersion addressingVersion, XmlDictionaryReader reader)
        {
            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }

            if (addressingVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(addressingVersion));
            }

            reader.ReadFullStartElement();
            EndpointAddress ea = ReadFromDriver(addressingVersion, reader);
            reader.ReadEndElement();
            return ea;
        }

        //public static EndpointAddress ReadFrom(AddressingVersion addressingVersion, XmlDictionaryReader reader, XmlDictionaryString localName, XmlDictionaryString ns)
        //{
        //    if (reader == null)
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
        //    if (addressingVersion == null)
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(addressingVersion));

        //    reader.ReadFullStartElement(localName, ns);
        //    EndpointAddress ea = ReadFromDriver(addressingVersion, reader);
        //    reader.ReadEndElement();
        //    return ea;
        //}

        private static EndpointAddress ReadFromDriver(AddressingVersion addressingVersion, XmlDictionaryReader reader)
        {
            AddressHeaderCollection headers;
            EndpointIdentity identity;
            Uri uri;
            XmlBuffer buffer;
            bool isAnonymous;
            int extensionSection;
            int metadataSection;
            int pspSection = -1;

            if (addressingVersion == AddressingVersion.WSAddressing10)
            {
                isAnonymous = ReadContentsFrom10(reader, out uri, out headers, out identity, out buffer, out metadataSection, out extensionSection);
            }
            else if (addressingVersion == AddressingVersion.WSAddressingAugust2004)
            {
                isAnonymous = ReadContentsFrom200408(reader, out uri, out headers, out identity, out buffer, out metadataSection, out extensionSection, out pspSection);
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(addressingVersion),
                    SR.Format(SR.AddressingVersionNotSupported, addressingVersion));
            }

            if (isAnonymous && headers == null && identity == null && buffer == null)
            {
                return AnonymousAddress;
            }
            else
            {
                return new EndpointAddress(addressingVersion, uri, identity, headers, buffer, metadataSection, extensionSection, pspSection);
            }
        }

        internal static XmlBuffer ReadExtensions(XmlDictionaryReader reader, AddressingVersion version, XmlBuffer buffer, out EndpointIdentity identity, out int section)
        {
            if (reader == null)
            {
                identity = null;
                section = -1;
                return buffer;
            }

            // EndpointIdentity and extensions
            identity = null;
            XmlDictionaryWriter bufferWriter = null;
            reader.MoveToContent();
            while (reader.IsStartElement())
            {
                if (reader.IsStartElement(XD.AddressingDictionary.Identity, XD.AddressingDictionary.IdentityExtensionNamespace))
                {
                    if (identity != null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateXmlException(reader, SR.Format(SR.UnexpectedDuplicateElement, XD.AddressingDictionary.Identity.Value, XD.AddressingDictionary.IdentityExtensionNamespace.Value)));
                    }

                    identity = EndpointIdentity.ReadIdentity(reader);
                }
                else if (version != null && reader.NamespaceURI == version.Namespace)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateXmlException(reader, SR.Format(SR.AddressingExtensionInBadNS, reader.LocalName, reader.NamespaceURI)));
                }
                else
                {
                    if (bufferWriter == null)
                    {
                        if (buffer == null)
                        {
                            buffer = new XmlBuffer(short.MaxValue);
                        }

                        bufferWriter = buffer.OpenSection(reader.Quotas);
                        bufferWriter.WriteStartElement(DummyName, DummyNamespace);
                    }

                    bufferWriter.WriteNode(reader, true);
                }
                reader.MoveToContent();
            }

            if (bufferWriter != null)
            {
                bufferWriter.WriteEndElement();
                buffer.CloseSection();
                section = buffer.SectionCount - 1;
            }
            else
            {
                section = -1;
            }

            return buffer;
        }

        private static bool ReadContentsFrom200408(XmlDictionaryReader reader, out Uri uri, out AddressHeaderCollection headers, out EndpointIdentity identity, out XmlBuffer buffer, out int metadataSection, out int extensionSection, out int pspSection)
        {
            buffer = null;
            headers = null;
            extensionSection = -1;
            metadataSection = -1;
            pspSection = -1;

            // Cache address string
            reader.MoveToContent();
            if (!reader.IsStartElement(XD.AddressingDictionary.Address, AddressingVersion.WSAddressingAugust2004.DictionaryNamespace))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateXmlException(reader, SR.Format(SR.UnexpectedElementExpectingElement, reader.LocalName, reader.NamespaceURI, XD.AddressingDictionary.Address.Value, XD.Addressing200408Dictionary.Namespace.Value)));
            }
            string address = reader.ReadElementContentAsString();

            // ReferenceProperites
            reader.MoveToContent();
            if (reader.IsStartElement(XD.AddressingDictionary.ReferenceProperties, AddressingVersion.WSAddressingAugust2004.DictionaryNamespace))
            {
                headers = AddressHeaderCollection.ReadServiceParameters(reader, true);
            }

            // ReferenceParameters
            reader.MoveToContent();
            if (reader.IsStartElement(XD.AddressingDictionary.ReferenceParameters, AddressingVersion.WSAddressingAugust2004.DictionaryNamespace))
            {
                if (headers != null)
                {
                    List<AddressHeader> headerList = new List<AddressHeader>();
                    foreach (AddressHeader ah in headers)
                    {
                        headerList.Add(ah);
                    }
                    AddressHeaderCollection tmp = AddressHeaderCollection.ReadServiceParameters(reader);
                    foreach (AddressHeader ah in tmp)
                    {
                        headerList.Add(ah);
                    }
                    headers = new AddressHeaderCollection(headerList);
                }
                else
                {
                    headers = AddressHeaderCollection.ReadServiceParameters(reader);
                }
            }

            XmlDictionaryWriter bufferWriter = null;

            // PortType
            reader.MoveToContent();
            if (reader.IsStartElement(XD.AddressingDictionary.PortType, AddressingVersion.WSAddressingAugust2004.DictionaryNamespace))
            {
                if (bufferWriter == null)
                {
                    if (buffer == null)
                        buffer = new XmlBuffer(short.MaxValue);
                    bufferWriter = buffer.OpenSection(reader.Quotas);
                    bufferWriter.WriteStartElement(DummyName, DummyNamespace);
                }
                bufferWriter.WriteNode(reader, true);
            }

            // ServiceName
            reader.MoveToContent();
            if (reader.IsStartElement(XD.AddressingDictionary.ServiceName, AddressingVersion.WSAddressingAugust2004.DictionaryNamespace))
            {
                if (bufferWriter == null)
                {
                    if (buffer == null)
                        buffer = new XmlBuffer(short.MaxValue);
                    bufferWriter = buffer.OpenSection(reader.Quotas);
                    bufferWriter.WriteStartElement(DummyName, DummyNamespace);
                }
                bufferWriter.WriteNode(reader, true);
            }

            // Policy
            reader.MoveToContent();
            while (reader.IsNamespaceUri(XD.PolicyDictionary.Namespace))
            {
                if (bufferWriter == null)
                {
                    if (buffer == null)
                        buffer = new XmlBuffer(short.MaxValue);
                    bufferWriter = buffer.OpenSection(reader.Quotas);
                    bufferWriter.WriteStartElement(DummyName, DummyNamespace);
                }
                bufferWriter.WriteNode(reader, true);
                reader.MoveToContent();
            }

            // Finish PSP
            if (bufferWriter != null)
            {
                bufferWriter.WriteEndElement();
                buffer.CloseSection();
                pspSection = buffer.SectionCount - 1;
                bufferWriter = null;
            }
            else
            {
                pspSection = -1;
            }


            // Metadata
            if (reader.IsStartElement(Description.MetadataStrings.MetadataExchangeStrings.Metadata,
                                      Description.MetadataStrings.MetadataExchangeStrings.Namespace))
            {
                if (bufferWriter == null)
                {
                    if (buffer == null)
                        buffer = new XmlBuffer(short.MaxValue);
                    bufferWriter = buffer.OpenSection(reader.Quotas);
                    bufferWriter.WriteStartElement(DummyName, DummyNamespace);
                }
                bufferWriter.WriteNode(reader, true);
            }

            // Finish metadata
            if (bufferWriter != null)
            {
                bufferWriter.WriteEndElement();
                buffer.CloseSection();
                metadataSection = buffer.SectionCount - 1;
            }
            else
            {
                metadataSection = -1;
            }

            // Extensions
            reader.MoveToContent();
            buffer = ReadExtensions(reader, AddressingVersion.WSAddressingAugust2004, buffer, out identity, out extensionSection);

            // Finished reading
            if (buffer != null)
                buffer.Close();

            // Process Address
            if (address == Addressing200408Strings.Anonymous)
            {
                uri = AddressingVersion.WSAddressingAugust2004.AnonymousUri;
                if (headers == null && identity == null)
                    return true;
            }
            else
            {
                if (!Uri.TryCreate(address, UriKind.Absolute, out uri))
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.InvalidUriValue, address, XD.AddressingDictionary.Address.Value, AddressingVersion.WSAddressingAugust2004.Namespace)));
            }

            return false;
        }

        private static bool ReadContentsFrom10(XmlDictionaryReader reader, out Uri uri, out AddressHeaderCollection headers, out EndpointIdentity identity, out XmlBuffer buffer, out int metadataSection, out int extensionSection)
        {
            buffer = null;
            extensionSection = -1;
            metadataSection = -1;

            // Cache address string
            if (!reader.IsStartElement(XD.AddressingDictionary.Address, XD.Addressing10Dictionary.Namespace))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateXmlException(reader, SR.Format(SR.UnexpectedElementExpectingElement, reader.LocalName, reader.NamespaceURI, XD.AddressingDictionary.Address.Value, XD.Addressing10Dictionary.Namespace.Value)));
            }

            string address = reader.ReadElementContentAsString();

            // Headers
            if (reader.IsStartElement(XD.AddressingDictionary.ReferenceParameters, XD.Addressing10Dictionary.Namespace))
            {
                headers = AddressHeaderCollection.ReadServiceParameters(reader);
            }
            else
            {
                headers = null;
            }

            // Metadata
            if (reader.IsStartElement(XD.Addressing10Dictionary.Metadata, XD.Addressing10Dictionary.Namespace))
            {
                reader.ReadFullStartElement();  // the wsa10:Metadata element is never stored in the buffer
                buffer = new XmlBuffer(short.MaxValue);
                metadataSection = 0;
                XmlDictionaryWriter writer = buffer.OpenSection(reader.Quotas);
                writer.WriteStartElement(DummyName, DummyNamespace);
                while (reader.NodeType != XmlNodeType.EndElement && !reader.EOF)
                {
                    writer.WriteNode(reader, true);
                }
                writer.Flush();
                buffer.CloseSection();
                reader.ReadEndElement();
            }

            // Extensions
            buffer = ReadExtensions(reader, AddressingVersion.WSAddressing10, buffer, out identity, out extensionSection);
            if (buffer != null)
            {
                buffer.Close();
            }

            // Process Address
            if (address == Addressing10Strings.Anonymous)
            {
                uri = AddressingVersion.WSAddressing10.AnonymousUri;
                if (headers == null && identity == null)
                {
                    return true;
                }
            }
            else if (address == Addressing10Strings.NoneAddress)
            {
                uri = AddressingVersion.WSAddressing10.NoneUri;
                return false;
            }
            else
            {
                if (!Uri.TryCreate(address, UriKind.Absolute, out uri))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.InvalidUriValue, address, XD.AddressingDictionary.Address.Value, XD.Addressing10Dictionary.Namespace.Value)));
                }
            }
            return false;
        }

        private static XmlException CreateXmlException(XmlDictionaryReader reader, string message)
        {
            if (reader is IXmlLineInfo lineInfo)
            {
                return new XmlException(message, null, lineInfo.LineNumber, lineInfo.LinePosition);
            }

            return new XmlException(message);
        }

        // this function has a side-effect on the reader (MoveToContent)
        private static bool Done(XmlDictionaryReader reader)
        {
            reader.MoveToContent();
            return (reader.NodeType == XmlNodeType.EndElement || reader.EOF);
        }

        // copy all of reader to writer
        internal static void Copy(XmlDictionaryWriter writer, XmlDictionaryReader reader)
        {
            while (!Done(reader))
            {
                writer.WriteNode(reader, true);
            }
        }

        public override string ToString()
        {
            return Uri.ToString();
        }

        public void WriteContentsTo(AddressingVersion addressingVersion, XmlDictionaryWriter writer)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            if (addressingVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(addressingVersion));
            }

            if (addressingVersion == AddressingVersion.WSAddressing10)
            {
                WriteContentsTo10(writer);
            }
            else if (addressingVersion == AddressingVersion.None)
            {
                WriteContentsToNone(writer);
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(addressingVersion),
                    SR.Format(SR.AddressingVersionNotSupported, addressingVersion));
            }
        }

        private void WriteContentsToNone(XmlDictionaryWriter writer)
        {
            writer.WriteString(Uri.AbsoluteUri);
        }

        private void WriteContentsTo10(XmlDictionaryWriter writer)
        {
            // Address
            writer.WriteStartElement(XD.AddressingDictionary.Address, XD.Addressing10Dictionary.Namespace);
            if (IsAnonymous)
            {
                writer.WriteString(XD.Addressing10Dictionary.Anonymous);
            }
            else if (IsNone)
            {
                writer.WriteString(XD.Addressing10Dictionary.NoneAddress);
            }
            else
            {
                writer.WriteString(Uri.AbsoluteUri);
            }
            writer.WriteEndElement();

            // Headers
            if (_headers != null && _headers.Count > 0)
            {
                writer.WriteStartElement(XD.AddressingDictionary.ReferenceParameters, XD.Addressing10Dictionary.Namespace);
                _headers.WriteContentsTo(writer);
                writer.WriteEndElement();
            }

            // Metadata
            if (_metadataSection >= 0)
            {
                XmlDictionaryReader reader = GetReaderAtSection(Buffer, _metadataSection);
                writer.WriteStartElement(XD.Addressing10Dictionary.Metadata, XD.Addressing10Dictionary.Namespace);
                Copy(writer, reader);
                writer.WriteEndElement();
            }

            // EndpointIdentity
            if (Identity != null)
            {
                Identity.WriteTo(writer);
            }

            // Extensions
            if (_extensionSection >= 0)
            {
                XmlDictionaryReader reader = GetReaderAtSection(Buffer, _extensionSection);
                while (reader.IsStartElement())
                {
                    if (reader.NamespaceURI == AddressingVersion.WSAddressing10.Namespace)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateXmlException(reader, SR.Format(SR.AddressingExtensionInBadNS, reader.LocalName, reader.NamespaceURI)));
                    }

                    writer.WriteNode(reader, true);
                }
            }
        }

        public void WriteContentsTo(AddressingVersion addressingVersion, XmlWriter writer)
        {
            XmlDictionaryWriter dictionaryWriter = XmlDictionaryWriter.CreateDictionaryWriter(writer);
            WriteContentsTo(addressingVersion, dictionaryWriter);
        }

        public void WriteTo(AddressingVersion addressingVersion, XmlDictionaryWriter writer, XmlDictionaryString localName, XmlDictionaryString ns)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }
            if (addressingVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(addressingVersion));
            }
            if (localName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(localName));
            }
            if (ns == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(ns));
            }
            writer.WriteStartElement(localName, ns);
            WriteContentsTo(addressingVersion, writer);
            writer.WriteEndElement();
        }

        public void WriteTo(AddressingVersion addressingVersion, XmlWriter writer)
        {
            XmlDictionaryString dictionaryNamespace = addressingVersion.DictionaryNamespace;
            if (dictionaryNamespace == null)
            {
                dictionaryNamespace = XD.AddressingDictionary.Empty;
            }

            WriteTo(addressingVersion, XmlDictionaryWriter.CreateDictionaryWriter(writer),
                XD.AddressingDictionary.EndpointReference, dictionaryNamespace);
        }

        public static bool operator ==(EndpointAddress address1, EndpointAddress address2)
        {
            if (ReferenceEquals(address2, null))
            {
                return (ReferenceEquals(address1, null));
            }

            return address2.Equals(address1);
        }

        public static bool operator !=(EndpointAddress address1, EndpointAddress address2)
        {
            if (ReferenceEquals(address2, null))
            {
                return !ReferenceEquals(address1, null);
            }

            return !address2.Equals(address1);
        }
    }

    public class EndpointAddressBuilder
    {
        private XmlBuffer _extensionBuffer;  // this buffer is wrapped just like in EndpointAddress
        private XmlBuffer _metadataBuffer;   // this buffer is wrapped just like in EndpointAddress
        private bool _hasExtension;
        private bool _hasMetadata;
        private readonly EndpointAddress _epr;

        public EndpointAddressBuilder()
        {
            Headers = new Collection<AddressHeader>();
        }

        public EndpointAddressBuilder(EndpointAddress address)
        {
            _epr = address ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(address));
            Uri = address.Uri;
            Identity = address.Identity;
            Headers = new Collection<AddressHeader>();
            for (int i = 0; i < address.Headers.Count; i++)
            {
                Headers.Add(address.Headers[i]);
            }
        }

        public Uri Uri { get; set; }

        public EndpointIdentity Identity { get; set; }

        public Collection<AddressHeader> Headers { get; }

        public XmlDictionaryReader GetReaderAtMetadata()
        {
            if (!_hasMetadata)
            {
                return _epr?.GetReaderAtMetadata();
            }

            if (_metadataBuffer == null)
            {
                return null;
            }

            XmlDictionaryReader reader = _metadataBuffer.GetReader(0);
            reader.MoveToContent();
            Fx.Assert(reader.Name == EndpointAddress.DummyName, "EndpointAddressBuilder: Expected dummy element not found");
            reader.Read(); // consume the wrapper element
            return reader;
        }

        public void SetMetadataReader(XmlDictionaryReader reader)
        {
            _hasMetadata = true;
            _metadataBuffer = null;
            if (reader != null)
            {
                _metadataBuffer = new XmlBuffer(short.MaxValue);
                XmlDictionaryWriter writer = _metadataBuffer.OpenSection(reader.Quotas);
                writer.WriteStartElement(EndpointAddress.DummyName, EndpointAddress.DummyNamespace);
                EndpointAddress.Copy(writer, reader);
                _metadataBuffer.CloseSection();
                _metadataBuffer.Close();
            }
        }

        public XmlDictionaryReader GetReaderAtExtensions()
        {
            if (!_hasExtension)
            {
                return _epr?.GetReaderAtExtensions();
            }

            if (_extensionBuffer == null)
            {
                return null;
            }

            XmlDictionaryReader reader = _extensionBuffer.GetReader(0);
            reader.MoveToContent();
            Fx.Assert(reader.Name == EndpointAddress.DummyName, "EndpointAddressBuilder: Expected dummy element not found");
            reader.Read(); // consume the wrapper element
            return reader;
        }

        public void SetExtensionReader(XmlDictionaryReader reader)
        {
            _hasExtension = true;
            _extensionBuffer = EndpointAddress.ReadExtensions(reader, null, null, out EndpointIdentity identity, out int tmp);
            if (_extensionBuffer != null)
            {
                _extensionBuffer.Close();
            }
            if (identity != null)
            {
                Identity = identity;
            }
        }

        public EndpointAddress ToEndpointAddress()
        {
            return new EndpointAddress(
                Uri,
                Identity,
                new AddressHeaderCollection(Headers),
                GetReaderAtMetadata(),
                GetReaderAtExtensions(),
                _epr?.GetReaderAtPsp());
        }
    }
}
