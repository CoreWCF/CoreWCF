// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;
using CoreWCF.Runtime;

namespace CoreWCF.Security
{
    internal sealed class SecurityVerifiedMessage : DelegatingMessage
    {
        private byte[] decryptedBuffer;
        private XmlDictionaryReader cachedDecryptedBodyContentReader;
        private XmlAttributeHolder[] envelopeAttributes;
        private XmlAttributeHolder[] headerAttributes;
        private XmlAttributeHolder[] bodyAttributes;
        private string envelopePrefix;
        private bool bodyDecrypted;
        private BodyState state = BodyState.Created;
        private string bodyPrefix;
        private bool isDecryptedBodyStatusDetermined;
        private bool isDecryptedBodyFault;
        private bool isDecryptedBodyEmpty;
        private XmlDictionaryReader cachedReaderAtSecurityHeader;
        private readonly ReceiveSecurityHeader securityHeader;
        private XmlBuffer messageBuffer;
        private bool canDelegateCreateBufferedCopyToInnerMessage;

        public SecurityVerifiedMessage(Message messageToProcess, ReceiveSecurityHeader securityHeader)
            : base(messageToProcess)
        {
            this.securityHeader = securityHeader;
            if (securityHeader.RequireMessageProtection)
            {
                XmlDictionaryReader messageReader;
                BufferedMessage bufferedMessage = InnerMessage as BufferedMessage;
                if (bufferedMessage != null && Headers.ContainsOnlyBufferedMessageHeaders)
                {
                    messageReader = bufferedMessage.GetMessageReader();
                }
                else
                {
                    messageBuffer = new XmlBuffer(int.MaxValue);
                    XmlDictionaryWriter writer = messageBuffer.OpenSection(this.securityHeader.ReaderQuotas);
                    InnerMessage.WriteMessage(writer);
                    messageBuffer.CloseSection();
                    messageBuffer.Close();
                    messageReader = messageBuffer.GetReader(0);
                }
                MoveToSecurityHeader(messageReader, securityHeader.HeaderIndex, true);
                cachedReaderAtSecurityHeader = messageReader;
                state = BodyState.Buffered;
            }
            else
            {
                envelopeAttributes = XmlAttributeHolder.emptyArray;
                headerAttributes = XmlAttributeHolder.emptyArray;
                bodyAttributes = XmlAttributeHolder.emptyArray;
                canDelegateCreateBufferedCopyToInnerMessage = true;
            }
        }

        public override bool IsEmpty
        {
            get
            {
                if (IsDisposed)
                {
                    throw TraceUtility.ThrowHelperError(CreateMessageDisposedException(), this);
                }
                if (!bodyDecrypted)
                {
                    return InnerMessage.IsEmpty;
                }

                EnsureDecryptedBodyStatusDetermined();

                return isDecryptedBodyEmpty;
            }
        }

        public override bool IsFault
        {
            get
            {
                if (IsDisposed)
                {
                    throw TraceUtility.ThrowHelperError(CreateMessageDisposedException(), this);
                }
                if (!bodyDecrypted)
                {
                    return InnerMessage.IsFault;
                }

                EnsureDecryptedBodyStatusDetermined();

                return isDecryptedBodyFault;
            }
        }

        internal byte[] PrimarySignatureValue => securityHeader.PrimarySignatureValue;

        internal ReceiveSecurityHeader ReceivedSecurityHeader => securityHeader;

        private Exception CreateBadStateException(string operation)
        {
            return new InvalidOperationException(SR.Format(SR.MessageBodyOperationNotValidInBodyState,
                operation, state));
        }

        public XmlDictionaryReader CreateFullBodyReader()
        {
            switch (state)
            {
                case BodyState.Buffered:
                    return CreateFullBodyReaderFromBufferedState();
                case BodyState.Decrypted:
                    return CreateFullBodyReaderFromDecryptedState();
                default:
                    throw TraceUtility.ThrowHelperError(CreateBadStateException("CreateFullBodyReader"), this);
            }
        }

        private XmlDictionaryReader CreateFullBodyReaderFromBufferedState()
        {
            if (messageBuffer != null)
            {
                XmlDictionaryReader reader = messageBuffer.GetReader(0);
                MoveToBody(reader);
                return reader;
            }
            else
            {
                return ((BufferedMessage)InnerMessage).GetBufferedReaderAtBody();
            }
        }

        private XmlDictionaryReader CreateFullBodyReaderFromDecryptedState()
        {
            XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(decryptedBuffer, 0, decryptedBuffer.Length, securityHeader.ReaderQuotas);
            MoveToBody(reader);
            return reader;
        }

        private void EnsureDecryptedBodyStatusDetermined()
        {
            if (!isDecryptedBodyStatusDetermined)
            {
                XmlDictionaryReader reader = CreateFullBodyReader();
                if (Message.ReadStartBody(reader, InnerMessage.Version.Envelope, out isDecryptedBodyFault, out isDecryptedBodyEmpty))
                {
                    cachedDecryptedBodyContentReader = reader;
                }
                else
                {
                    reader.Close();
                }
                isDecryptedBodyStatusDetermined = true;
            }
        }

        public XmlAttributeHolder[] GetEnvelopeAttributes()
        {
            return envelopeAttributes;
        }

        public XmlAttributeHolder[] GetHeaderAttributes()
        {
            return headerAttributes;
        }

        private XmlDictionaryReader GetReaderAtEnvelope()
        {
            if (messageBuffer != null)
            {
                return messageBuffer.GetReader(0);
            }
            else
            {
                return ((BufferedMessage)InnerMessage).GetMessageReader();
            }
        }

        public XmlDictionaryReader GetReaderAtFirstHeader()
        {
            XmlDictionaryReader reader = GetReaderAtEnvelope();
            MoveToHeaderBlock(reader, false);
            reader.ReadStartElement();
            return reader;
        }

        public XmlDictionaryReader GetReaderAtSecurityHeader()
        {
            if (cachedReaderAtSecurityHeader != null)
            {
                XmlDictionaryReader result = cachedReaderAtSecurityHeader;
                cachedReaderAtSecurityHeader = null;
                return result;
            }
            return Headers.GetReaderAtHeader(securityHeader.HeaderIndex);
        }

        private void MoveToBody(XmlDictionaryReader reader)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.MoveToContent();
            }
            reader.ReadStartElement();
            if (reader.IsStartElement(XD.MessageDictionary.Header, Version.Envelope.DictionaryNamespace))
            {
                reader.Skip();
            }
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.MoveToContent();
            }
        }

        private void MoveToHeaderBlock(XmlDictionaryReader reader, bool captureAttributes)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.MoveToContent();
            }
            if (captureAttributes)
            {
                envelopePrefix = reader.Prefix;
                envelopeAttributes = XmlAttributeHolder.ReadAttributes(reader);
            }
            reader.ReadStartElement();
            reader.MoveToStartElement(XD.MessageDictionary.Header, Version.Envelope.DictionaryNamespace);
            if (captureAttributes)
            {
                headerAttributes = XmlAttributeHolder.ReadAttributes(reader);
            }
        }

        private void MoveToSecurityHeader(XmlDictionaryReader reader, int headerIndex, bool captureAttributes)
        {
            MoveToHeaderBlock(reader, captureAttributes);
            reader.ReadStartElement();
            while (true)
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.MoveToContent();
                }
                if (headerIndex == 0)
                {
                    break;
                }
                reader.Skip();
                headerIndex--;
            }
        }

        protected override void OnBodyToString(XmlDictionaryWriter writer)
        {
            if (state == BodyState.Created)
            {
                base.OnBodyToString(writer);
            }
            else
            {
                OnWriteBodyContents(writer);
            }
        }

        protected override void OnClose()
        {
            if (cachedDecryptedBodyContentReader != null)
            {
                try
                {
                    cachedDecryptedBodyContentReader.Close();
                }
                catch (System.IO.IOException exception)
                {
                    //
                    // We only want to catch and log the I/O exception here 
                    // assuming reader only throw those exceptions  
                    //
                    DiagnosticUtility.TraceHandledException(exception, TraceEventType.Warning);
                }
                finally
                {
                    cachedDecryptedBodyContentReader = null;
                }
            }

            if (cachedReaderAtSecurityHeader != null)
            {
                try
                {
                    cachedReaderAtSecurityHeader.Close();
                }
                catch (System.IO.IOException exception)
                {
                    //
                    // We only want to catch and log the I/O exception here 
                    // assuming reader only throw those exceptions  
                    //
                    DiagnosticUtility.TraceHandledException(exception, TraceEventType.Warning);
                }
                finally
                {
                    cachedReaderAtSecurityHeader = null;
                }
            }

            messageBuffer = null;
            decryptedBuffer = null;
            state = BodyState.Disposed;
            InnerMessage.Close();
        }

        protected override XmlDictionaryReader OnGetReaderAtBodyContents()
        {
            if (state == BodyState.Created)
            {
                return InnerMessage.GetReaderAtBodyContents();
            }
            if (bodyDecrypted)
            {
                EnsureDecryptedBodyStatusDetermined();
            }
            if (cachedDecryptedBodyContentReader != null)
            {
                XmlDictionaryReader result = cachedDecryptedBodyContentReader;
                cachedDecryptedBodyContentReader = null;
                return result;
            }
            else
            {
                XmlDictionaryReader reader = CreateFullBodyReader();
                reader.ReadStartElement();
                reader.MoveToContent();
                return reader;
            }
        }

        protected override MessageBuffer OnCreateBufferedCopy(int maxBufferSize)
        {
            if (canDelegateCreateBufferedCopyToInnerMessage && InnerMessage is BufferedMessage)
            {
                return InnerMessage.CreateBufferedCopy(maxBufferSize);
            }
            else
            {
                return base.OnCreateBufferedCopy(maxBufferSize);
            }
        }

        internal void OnMessageProtectionPassComplete(bool atLeastOneHeaderOrBodyEncrypted)
        {
            canDelegateCreateBufferedCopyToInnerMessage = !atLeastOneHeaderOrBodyEncrypted;
        }

        internal void OnUnencryptedPart(string name, string ns)
        {
            if (ns == null)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.RequiredMessagePartNotEncrypted, name)), this);
            }
            else
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.RequiredMessagePartNotEncryptedNs, name, ns)), this);
            }
        }

        internal void OnUnsignedPart(string name, string ns)
        {
            if (ns == null)
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.RequiredMessagePartNotSigned, name)), this);
            }
            else
            {
                throw TraceUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.RequiredMessagePartNotSignedNs, name, ns)), this);
            }
        }

        protected override void OnWriteStartBody(XmlDictionaryWriter writer)
        {
            if (state == BodyState.Created)
            {
                InnerMessage.WriteStartBody(writer);
                return;
            }

            XmlDictionaryReader reader = CreateFullBodyReader();
            reader.MoveToContent();
            writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
            writer.WriteAttributes(reader, false);
            reader.Close();
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            if (state == BodyState.Created)
            {
                InnerMessage.WriteBodyContents(writer);
                return;
            }

            XmlDictionaryReader reader = CreateFullBodyReader();
            reader.ReadStartElement();
            while (reader.NodeType != XmlNodeType.EndElement)
            {
                writer.WriteNode(reader, false);
            }

            reader.ReadEndElement();
            reader.Close();
        }

        public void SetBodyPrefixAndAttributes(XmlDictionaryReader bodyReader)
        {
            bodyPrefix = bodyReader.Prefix;
            bodyAttributes = XmlAttributeHolder.ReadAttributes(bodyReader);
        }

        public void SetDecryptedBody(byte[] decryptedBodyContent)
        {
            if (state != BodyState.Buffered)
            {
                throw TraceUtility.ThrowHelperError(CreateBadStateException("SetDecryptedBody"), this);
            }

            MemoryStream stream = new MemoryStream();
            XmlDictionaryWriter writer = XmlDictionaryWriter.CreateTextWriter(stream);

            writer.WriteStartElement(envelopePrefix, XD.MessageDictionary.Envelope, Version.Envelope.DictionaryNamespace);
            XmlAttributeHolder.WriteAttributes(envelopeAttributes, writer);

            writer.WriteStartElement(bodyPrefix, XD.MessageDictionary.Body, Version.Envelope.DictionaryNamespace);
            XmlAttributeHolder.WriteAttributes(bodyAttributes, writer);
            writer.WriteString(" "); // ensure non-empty element
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.Flush();

            decryptedBuffer = ContextImportHelper.SpliceBuffers(decryptedBodyContent, stream.GetBuffer(), (int)stream.Length, 2);

            bodyDecrypted = true;
            state = BodyState.Decrypted;
        }

        private enum BodyState
        {
            Created,
            Buffered,
            Decrypted,
            Disposed,
        }
    }

    //TODO investigate
    // Adding wrapping tags using a writer is a temporary feature to
    // support interop with a partner.  Eventually, the serialization
    // team will add a feature to XmlUTF8TextReader to directly
    // support the addition of outer namespaces before creating a
    // Reader.  This roundabout way of supporting context-sensitive
    // decryption can then be removed.
    internal static class ContextImportHelper
    {
        internal static XmlDictionaryReader CreateSplicedReader(byte[] decryptedBuffer,
            XmlAttributeHolder[] outerContext1, XmlAttributeHolder[] outerContext2, XmlAttributeHolder[] outerContext3, XmlDictionaryReaderQuotas quotas)
        {
            const string wrapper1 = "x";
            const string wrapper2 = "y";
            const string wrapper3 = "z";
            const int wrappingDepth = 3;

            MemoryStream stream = new MemoryStream();
            XmlDictionaryWriter writer = XmlDictionaryWriter.CreateTextWriter(stream);
            writer.WriteStartElement(wrapper1);
            WriteNamespaceDeclarations(outerContext1, writer);
            writer.WriteStartElement(wrapper2);
            WriteNamespaceDeclarations(outerContext2, writer);
            writer.WriteStartElement(wrapper3);
            WriteNamespaceDeclarations(outerContext3, writer);
            writer.WriteString(" "); // ensure non-empty element
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.Flush();

            byte[] splicedBuffer = SpliceBuffers(decryptedBuffer, stream.GetBuffer(), (int)stream.Length, wrappingDepth);
            XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(splicedBuffer, quotas);
            reader.ReadStartElement(wrapper1);
            reader.ReadStartElement(wrapper2);
            reader.ReadStartElement(wrapper3);
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.MoveToContent();
            }
            return reader;
        }

        internal static string GetPrefixIfNamespaceDeclaration(string prefix, string localName)
        {
            if (prefix == "xmlns")
            {
                return localName;
            }
            if (prefix.Length == 0 && localName == "xmlns")
            {
                return string.Empty;
            }
            return null;
        }

        private static bool IsNamespaceDeclaration(string prefix, string localName)
        {
            return GetPrefixIfNamespaceDeclaration(prefix, localName) != null;
        }

        internal static byte[] SpliceBuffers(byte[] middle, byte[] wrapper, int wrapperLength, int wrappingDepth)
        {
            const byte openChar = (byte)'<';
            int openCharsFound = 0;
            int openCharIndex;
            for (openCharIndex = wrapperLength - 1; openCharIndex >= 0; openCharIndex--)
            {
                if (wrapper[openCharIndex] == openChar)
                {
                    openCharsFound++;
                    if (openCharsFound == wrappingDepth)
                    {
                        break;
                    }
                }
            }

            Fx.Assert(openCharIndex > 0, "");

            byte[] splicedBuffer = Fx.AllocateByteArray(checked(middle.Length + wrapperLength - 1));
            int offset = 0;
            int count = openCharIndex - 1;
            Buffer.BlockCopy(wrapper, 0, splicedBuffer, offset, count);
            offset += count;
            count = middle.Length;
            Buffer.BlockCopy(middle, 0, splicedBuffer, offset, count);
            offset += count;
            count = wrapperLength - openCharIndex;
            Buffer.BlockCopy(wrapper, openCharIndex, splicedBuffer, offset, count);

            return splicedBuffer;
        }

        private static void WriteNamespaceDeclarations(XmlAttributeHolder[] attributes, XmlWriter writer)
        {
            if (attributes != null)
            {
                for (int i = 0; i < attributes.Length; i++)
                {
                    XmlAttributeHolder a = attributes[i];
                    if (IsNamespaceDeclaration(a.Prefix, a.LocalName))
                    {
                        a.WriteTo(writer);
                    }
                }
            }
        }
    }
}
