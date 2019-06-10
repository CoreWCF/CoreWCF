using System;
using System.Collections.ObjectModel;
using System.Xml;
using CoreWCF.Runtime;
using CoreWCF.Channels;

namespace CoreWCF
{
    public class EndpointAddress
    {
        static Uri anonymousUri;
        static Uri noneUri;
        static EndpointAddress anonymousAddress;

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

        AddressingVersion addressingVersion;
        AddressHeaderCollection headers;
        EndpointIdentity identity;
        Uri uri;
        XmlBuffer buffer;  // invariant: each section in the buffer will start with a dummy wrapper element
        int extensionSection;
        int metadataSection;
        int pspSection;
        bool isAnonymous;
        bool isNone;
        // these are the element name/namespace for the dummy wrapper element that wraps each buffer section
        internal const string DummyName = "Dummy";
        internal const string DummyNamespace = "http://Dummy";

        EndpointAddress(AddressingVersion version, Uri uri, EndpointIdentity identity, AddressHeaderCollection headers, XmlBuffer buffer, int metadataSection, int extensionSection, int pspSection)
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

        internal EndpointAddress(Uri newUri, EndpointAddress oldEndpointAddress)
        {
            Init(oldEndpointAddress.addressingVersion, newUri, oldEndpointAddress.identity, oldEndpointAddress.headers, oldEndpointAddress.buffer, oldEndpointAddress.metadataSection, oldEndpointAddress.extensionSection, oldEndpointAddress.pspSection);
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
            PossiblyPopulateBuffer(metadataReader, ref buffer, out metadataSection);

            EndpointIdentity ident2;
            int extSection;
            buffer = ReadExtensions(extensionReader, null, buffer, out ident2, out extSection);

            if (identity != null && ident2 != null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.MultipleIdentities, nameof(extensionReader)));
            }

            PossiblyPopulateBuffer(pspReader, ref buffer, out pspSection);

            if (buffer != null)
            {
                buffer.Close();
            }

            Init(uri, identity ?? ident2, headers, buffer, metadataSection, extSection, pspSection);
        }

        //// metadataReader and extensionReader are assumed to not have a starting dummy wrapper element
        //public EndpointAddress(Uri uri, EndpointIdentity identity, AddressHeaderCollection headers, XmlDictionaryReader metadataReader, XmlDictionaryReader extensionReader)
        //    : this(uri, identity, headers, metadataReader, extensionReader, null)
        //{
        //}

        void Init(Uri uri, EndpointIdentity identity, AddressHeader[] headers)
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

        void Init(Uri uri, EndpointIdentity identity, AddressHeaderCollection headers, XmlBuffer buffer, int metadataSection, int extensionSection, int pspSection)
        {
            Init(null, uri, identity, headers, buffer, metadataSection, extensionSection, pspSection);
        }

        void Init(AddressingVersion version, Uri uri, EndpointIdentity identity, AddressHeaderCollection headers, XmlBuffer buffer, int metadataSection, int extensionSection, int pspSection)
        {
            if (!uri.IsAbsoluteUri)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("uri", SR.UriMustBeAbsolute);

            addressingVersion = version;
            this.uri = uri;
            this.identity = identity;
            this.headers = headers;
            this.buffer = buffer;
            this.metadataSection = metadataSection;
            this.extensionSection = extensionSection;
            this.pspSection = pspSection;

            if (version != null)
            {
                isAnonymous = uri == version.AnonymousUri;
                isNone = uri == version.NoneUri;
            }
            else
            {
                isAnonymous = object.ReferenceEquals(uri, AnonymousUri) || uri == AnonymousUri;
                isNone = object.ReferenceEquals(uri, NoneUri) || uri == NoneUri;
            }
            if (isAnonymous)
            {
                this.uri = AnonymousUri;
            }
            if (isNone)
            {
                this.uri = NoneUri;
            }
        }

        // TODO: This was internal, I needed to make it public. Is this a problem?
        public static EndpointAddress AnonymousAddress
        {
            get
            {
                if (anonymousAddress == null)
                    anonymousAddress = new EndpointAddress(AnonymousUri);
                return anonymousAddress;
            }
        }

        public static Uri AnonymousUri
        {
            get
            {
                if (anonymousUri == null)
                    anonymousUri = new Uri(AddressingStrings.AnonymousUri);
                return anonymousUri;
            }
        }

        public static Uri NoneUri
        {
            get
            {
                if (noneUri == null)
                    noneUri = new Uri(AddressingStrings.NoneUri);
                return noneUri;
            }
        }

        internal XmlBuffer Buffer
        {
            get
            {
                return buffer;
            }
        }

        public AddressHeaderCollection Headers
        {
            get
            {
                if (headers == null)
                {
                    headers = new AddressHeaderCollection();
                }

                return headers;
            }
        }

        public EndpointIdentity Identity
        {
            get
            {
                return identity;
            }
        }

        public bool IsAnonymous
        {
            get
            {
                return isAnonymous;
            }
        }

        public bool IsNone
        {
            get
            {
                return isNone;
            }
        }

        public Uri Uri
        {
            get
            {
                return uri;
            }
        }

        public void ApplyTo(Message message)
        {
            if (message == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));

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
            if (headers != null)
            {
                headers.AddHeadersTo(message);
            }
        }

        // NOTE: UserInfo, Query, and Fragment are ignored when comparing Uris as addresses
        // this is the WCF logic for comparing Uris that represent addresses
        // this method must be kept in sync with UriGetHashCode
        internal static bool UriEquals(Uri u1, Uri u2, bool ignoreCase, bool includeHostInComparison)
        {
            return UriEquals(u1, u2, ignoreCase, includeHostInComparison, true);
        }

        internal static bool UriEquals(Uri u1, Uri u2, bool ignoreCase, bool includeHostInComparison, bool includePortInComparison)
        {
            // PERF: Equals compares everything but UserInfo and Fragments.  It's more strict than
            //       we are, and faster, so it is done first.
            if (u1.Equals(u2))
            {
                return true;
            }

            if (u1.Scheme != u2.Scheme)  // Uri.Scheme is always lowercase
            {
                return false;
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
                uriString = string.Concat(uriString, "/");

            return StringComparer.OrdinalIgnoreCase.GetHashCode(uriString);
        }

        internal bool EndpointEquals(EndpointAddress endpointAddress)
        {
            if (endpointAddress == null)
            {
                return false;
            }

            if (object.ReferenceEquals(this, endpointAddress))
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
            if (object.ReferenceEquals(obj, this))
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
            return UriGetHashCode(uri, true /* includeHostInComparison */);
        }

        // returns reader without starting dummy wrapper element
        internal XmlDictionaryReader GetReaderAtPsp()
        {
            return GetReaderAtSection(buffer, pspSection);
        }

        //// returns reader without starting dummy wrapper element
        internal XmlDictionaryReader GetReaderAtMetadata()
        {
            return GetReaderAtSection(buffer, metadataSection);
        }

        //// returns reader without starting dummy wrapper element
        internal XmlDictionaryReader GetReaderAtExtensions()
        {
            return GetReaderAtSection(buffer, extensionSection);
        }

        static XmlDictionaryReader GetReaderAtSection(XmlBuffer buffer, int section)
        {
            if (buffer == null || section < 0)
                return null;

            XmlDictionaryReader reader = buffer.GetReader(section);
            reader.MoveToContent();
            Fx.Assert(reader.Name == DummyName, "EndpointAddress: Expected dummy element not found");
            reader.Read(); // consume the dummy wrapper element
            return reader;
        }

        void PossiblyPopulateBuffer(XmlDictionaryReader reader, ref XmlBuffer buffer, out int section)
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

        //public static EndpointAddress ReadFrom(XmlDictionaryReader reader)
        //{
        //    AddressingVersion dummyVersion;
        //    return ReadFrom(reader, out dummyVersion);
        //}

        internal static EndpointAddress ReadFrom(XmlDictionaryReader reader, out AddressingVersion version)
        {
            if (reader == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));

            reader.ReadFullStartElement();
            reader.MoveToContent();

            if (reader.IsNamespaceUri(AddressingVersion.WSAddressing10.DictionaryNamespace))
            {
                version = AddressingVersion.WSAddressing10;
            }
            //else if (reader.IsNamespaceUri(AddressingVersion.WSAddressingAugust2004.DictionaryNamespace))
            //{
            //    version = AddressingVersion.WSAddressingAugust2004;
            //}
            else if (reader.NodeType != XmlNodeType.Element)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(
                    "reader", SR.CannotDetectAddressingVersion);
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(
                    "reader", SR.Format(SR.AddressingVersionNotSupported, reader.NamespaceURI));
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));

            reader.ReadFullStartElement(localName, ns);
            reader.MoveToContent();

            if (reader.IsNamespaceUri(AddressingVersion.WSAddressing10.DictionaryNamespace))
            {
                version = AddressingVersion.WSAddressing10;
            }
            //else if (reader.IsNamespaceUri(AddressingVersion.WSAddressingAugust2004.DictionaryNamespace))
            //{
            //    version = AddressingVersion.WSAddressingAugust2004;
            //}
            else if (reader.NodeType != XmlNodeType.Element)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(
                    "reader", SR.CannotDetectAddressingVersion);
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(
                    "reader", SR.Format(SR.AddressingVersionNotSupported, reader.NamespaceURI));
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            if (addressingVersion == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(addressingVersion));

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

        static EndpointAddress ReadFromDriver(AddressingVersion addressingVersion, XmlDictionaryReader reader)
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
            //else if (addressingVersion == AddressingVersion.WSAddressingAugust2004)
            //{
            //    isAnonymous = ReadContentsFrom200408(reader, out uri, out headers, out identity, out buffer, out metadataSection, out extensionSection, out pspSection);
            //}
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("addressingVersion",
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
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateXmlException(reader, SR.Format(SR.UnexpectedDuplicateElement, XD.AddressingDictionary.Identity.Value, XD.AddressingDictionary.IdentityExtensionNamespace.Value)));
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
                            buffer = new XmlBuffer(short.MaxValue);
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

        static bool ReadContentsFrom10(XmlDictionaryReader reader, out Uri uri, out AddressHeaderCollection headers, out EndpointIdentity identity, out XmlBuffer buffer, out int metadataSection, out int extensionSection)
        {
            buffer = null;
            extensionSection = -1;
            metadataSection = -1;

            // Cache address string
            if (!reader.IsStartElement(XD.AddressingDictionary.Address, XD.Addressing10Dictionary.Namespace))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateXmlException(reader, SR.Format(SR.UnexpectedElementExpectingElement, reader.LocalName, reader.NamespaceURI, XD.AddressingDictionary.Address.Value, XD.Addressing10Dictionary.Namespace.Value)));
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

        static XmlException CreateXmlException(XmlDictionaryReader reader, string message)
        {
            IXmlLineInfo lineInfo = reader as IXmlLineInfo;
            if (lineInfo != null)
            {
                return new XmlException(message, null, lineInfo.LineNumber, lineInfo.LinePosition);
            }

            return new XmlException(message);
        }

        // this function has a side-effect on the reader (MoveToContent)
        static bool Done(XmlDictionaryReader reader)
        {
            reader.MoveToContent();
            return (reader.NodeType == XmlNodeType.EndElement || reader.EOF);
        }

        // copy all of reader to writer
        static internal void Copy(XmlDictionaryWriter writer, XmlDictionaryReader reader)
        {
            while (!Done(reader))
            {
                writer.WriteNode(reader, true);
            }
        }

        public override string ToString()
        {
            return uri.ToString();
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("addressingVersion",
                    SR.Format(SR.AddressingVersionNotSupported, addressingVersion));
            }
        }

        void WriteContentsToNone(XmlDictionaryWriter writer)
        {
            writer.WriteString(Uri.AbsoluteUri);
        }

        void WriteContentsTo10(XmlDictionaryWriter writer)
        {
            // Address
            writer.WriteStartElement(XD.AddressingDictionary.Address, XD.Addressing10Dictionary.Namespace);
            if (isAnonymous)
            {
                writer.WriteString(XD.Addressing10Dictionary.Anonymous);
            }
            else if (isNone)
            {
                writer.WriteString(XD.Addressing10Dictionary.NoneAddress);
            }
            else
            {
                writer.WriteString(Uri.AbsoluteUri);
            }
            writer.WriteEndElement();

            // Headers
            if (headers != null && headers.Count > 0)
            {
                writer.WriteStartElement(XD.AddressingDictionary.ReferenceParameters, XD.Addressing10Dictionary.Namespace);
                headers.WriteContentsTo(writer);
                writer.WriteEndElement();
            }

            // Metadata
            if (metadataSection >= 0)
            {
                XmlDictionaryReader reader = GetReaderAtSection(buffer, metadataSection);
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
            if (extensionSection >= 0)
            {
                XmlDictionaryReader reader = GetReaderAtSection(buffer, extensionSection);
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

        public static bool operator ==(EndpointAddress address1, EndpointAddress address2)
        {
            if (object.ReferenceEquals(address2, null))
            {
                return (object.ReferenceEquals(address1, null));
            }

            return address2.Equals(address1);
        }

        public static bool operator !=(EndpointAddress address1, EndpointAddress address2)
        {
            if (object.ReferenceEquals(address2, null))
            {
                return !object.ReferenceEquals(address1, null);
            }

            return !address2.Equals(address1);
        }
    }

    public class EndpointAddressBuilder
    {
        Uri uri;
        EndpointIdentity identity;
        Collection<AddressHeader> headers;
        XmlBuffer extensionBuffer;  // this buffer is wrapped just like in EndpointAddress
        XmlBuffer metadataBuffer;   // this buffer is wrapped just like in EndpointAddress
        bool hasExtension;
        bool hasMetadata;
        EndpointAddress epr;

        public EndpointAddressBuilder()
        {
            headers = new Collection<AddressHeader>();
        }

        public EndpointAddressBuilder(EndpointAddress address)
        {
            if (address == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(address));
            }

            epr = address;
            uri = address.Uri;
            identity = address.Identity;
            headers = new Collection<AddressHeader>();
            for (int i = 0; i < address.Headers.Count; i++)
            {
                headers.Add(address.Headers[i]);
            }
        }

        public Uri Uri
        {
            get { return uri; }
            set { uri = value; }
        }

        public EndpointIdentity Identity
        {
            get { return identity; }
            set { identity = value; }
        }

        public Collection<AddressHeader> Headers
        {
            get { return headers; }
        }

        public XmlDictionaryReader GetReaderAtMetadata()
        {
            if (!hasMetadata)
            {
                return epr == null ? null : epr.GetReaderAtMetadata();
            }

            if (metadataBuffer == null)
            {
                return null;
            }

            XmlDictionaryReader reader = metadataBuffer.GetReader(0);
            reader.MoveToContent();
            Fx.Assert(reader.Name == EndpointAddress.DummyName, "EndpointAddressBuilder: Expected dummy element not found");
            reader.Read(); // consume the wrapper element
            return reader;
        }

        public void SetMetadataReader(XmlDictionaryReader reader)
        {
            hasMetadata = true;
            metadataBuffer = null;
            if (reader != null)
            {
                metadataBuffer = new XmlBuffer(short.MaxValue);
                XmlDictionaryWriter writer = metadataBuffer.OpenSection(reader.Quotas);
                writer.WriteStartElement(EndpointAddress.DummyName, EndpointAddress.DummyNamespace);
                EndpointAddress.Copy(writer, reader);
                metadataBuffer.CloseSection();
                metadataBuffer.Close();
            }
        }

        public XmlDictionaryReader GetReaderAtExtensions()
        {
            if (!hasExtension)
            {
                return epr == null ? null : epr.GetReaderAtExtensions();
            }

            if (extensionBuffer == null)
            {
                return null;
            }

            XmlDictionaryReader reader = extensionBuffer.GetReader(0);
            reader.MoveToContent();
            Fx.Assert(reader.Name == EndpointAddress.DummyName, "EndpointAddressBuilder: Expected dummy element not found");
            reader.Read(); // consume the wrapper element
            return reader;
        }

        public void SetExtensionReader(XmlDictionaryReader reader)
        {
            hasExtension = true;
            EndpointIdentity identity;
            int tmp;
            extensionBuffer = EndpointAddress.ReadExtensions(reader, null, null, out identity, out tmp);
            if (extensionBuffer != null)
            {
                extensionBuffer.Close();
            }
            if (identity != null)
            {
                this.identity = identity;
            }
        }

        public EndpointAddress ToEndpointAddress()
        {
            return new EndpointAddress(
                uri,
                identity,
                new AddressHeaderCollection(headers),
                GetReaderAtMetadata(),
                GetReaderAtExtensions(),
                epr == null ? null : epr.GetReaderAtPsp());
        }
    }

}