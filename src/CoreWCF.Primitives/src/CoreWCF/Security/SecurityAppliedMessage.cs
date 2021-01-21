// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;
using IPrefixGenerator = CoreWCF.IdentityModel.IPrefixGenerator;
using ISecurityElement = CoreWCF.IdentityModel.ISecurityElement;
using XmlAttributeHolder = CoreWCF.IdentityModel.XmlAttributeHolder;

namespace CoreWCF.Security
{
    internal sealed class SecurityAppliedMessage : DelegatingMessage
    {
        private string bodyId;
        private bool bodyIdInserted;
        private string bodyPrefix = MessageStrings.Prefix;
        private XmlBuffer fullBodyBuffer;
        private ISecurityElement encryptedBodyContent;
        private XmlAttributeHolder[] bodyAttributes;
        private bool delayedApplicationHandled;
        private readonly MessagePartProtectionMode bodyProtectionMode;
        private BodyState state = BodyState.Created;
        private readonly SendSecurityHeader securityHeader;
        private MemoryStream startBodyFragment;
        private MemoryStream endBodyFragment;
        private byte[] fullBodyFragment;
        private int fullBodyFragmentLength;

        public SecurityAppliedMessage(Message messageToProcess, SendSecurityHeader securityHeader, bool signBody, bool encryptBody)
            : base(messageToProcess)
        {
            Fx.Assert(!(messageToProcess is SecurityAppliedMessage), "SecurityAppliedMessage should not be wrapped");
            this.securityHeader = securityHeader;
            this.bodyProtectionMode = MessagePartProtectionModeHelper.GetProtectionMode(signBody, encryptBody, securityHeader.SignThenEncrypt);
        }

        public string BodyId => this.bodyId;

        public MessagePartProtectionMode BodyProtectionMode => this.bodyProtectionMode;

        internal byte[] PrimarySignatureValue => this.securityHeader.PrimarySignatureValue;

        private Exception CreateBadStateException(string operation)
        {
            return new InvalidOperationException(SR.Format(SR.MessageBodyOperationNotValidInBodyState,
                operation, this.state));
        }

        private void EnsureUniqueSecurityApplication()
        {
            if (this.delayedApplicationHandled)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.DelayedSecurityApplicationAlreadyCompleted)));
            }
            this.delayedApplicationHandled = true;
        }

        protected override void OnBodyToString(XmlDictionaryWriter writer)
        {
            if (this.state == BodyState.Created || this.fullBodyFragment != null)
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
            try
            {
                this.InnerMessage.Close();
            }
            finally
            {
                this.fullBodyBuffer = null;
                this.bodyAttributes = null;
                this.encryptedBodyContent = null;
                this.state = BodyState.Disposed;
            }
        }

        protected override void OnWriteStartBody(XmlDictionaryWriter writer)
        {
            if (this.startBodyFragment != null || this.fullBodyFragment != null)
            {
                WriteStartInnerMessageWithId(writer);
                return;
            }

            switch (this.state)
            {
                case BodyState.Created:
                case BodyState.Encrypted:
                    this.InnerMessage.WriteStartBody(writer);
                    return;
                case BodyState.Signed:
                case BodyState.EncryptedThenSigned:
                    XmlDictionaryReader reader = fullBodyBuffer.GetReader(0);
                    writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
                    writer.WriteAttributes(reader, false);
                    reader.Close();
                    return;
                case BodyState.SignedThenEncrypted:
                    writer.WriteStartElement(this.bodyPrefix, XD.MessageDictionary.Body, this.Version.Envelope.DictionaryNamespace);
                    if (this.bodyAttributes != null)
                    {
                        XmlAttributeHolder.WriteAttributes(this.bodyAttributes, writer);
                    }
                    return;
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBadStateException(nameof(OnWriteStartBody)));
            }
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            switch (this.state)
            {
                case BodyState.Created:
                    this.InnerMessage.WriteBodyContents(writer);
                    return;
                case BodyState.Signed:
                case BodyState.EncryptedThenSigned:
                    XmlDictionaryReader reader = fullBodyBuffer.GetReader(0);
                    reader.ReadStartElement();
                    while (reader.NodeType != XmlNodeType.EndElement)
                        writer.WriteNode(reader, false);
                    reader.ReadEndElement();
                    reader.Close();
                    return;
                case BodyState.Encrypted:
                case BodyState.SignedThenEncrypted:
                    this.encryptedBodyContent.WriteTo(writer, ServiceModelDictionaryManager.Instance);
                    break;
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBadStateException(nameof(OnWriteBodyContents)));
            }
        }

        protected override void OnWriteMessage(XmlDictionaryWriter writer)
        {
            // For Kerb one shot, the channel binding will be need to be fished out of the message, cached and added to the
            // token before calling ISC.

            AttachChannelBindingTokenIfFound();

            EnsureUniqueSecurityApplication();

            MessagePrefixGenerator prefixGenerator = new MessagePrefixGenerator(writer);
            this.securityHeader.StartSecurityApplication();

            this.Headers.Add(this.securityHeader);

            this.InnerMessage.WriteStartEnvelope(writer);

            this.Headers.RemoveAt(this.Headers.Count - 1);

            this.securityHeader.ApplyBodySecurity(writer, prefixGenerator);

            this.InnerMessage.WriteStartHeaders(writer);
            this.securityHeader.ApplySecurityAndWriteHeaders(this.Headers, writer, prefixGenerator);

            this.securityHeader.RemoveSignatureEncryptionIfAppropriate();

            this.securityHeader.CompleteSecurityApplication();
            this.securityHeader.WriteHeader(writer, this.Version);
            writer.WriteEndElement();

            if (this.fullBodyFragment != null)
            {
                ((IFragmentCapableXmlDictionaryWriter)writer).WriteFragment(this.fullBodyFragment, 0, this.fullBodyFragmentLength);
            }
            else
            {
                if (this.startBodyFragment != null)
                {
                    ((IFragmentCapableXmlDictionaryWriter)writer).WriteFragment(this.startBodyFragment.GetBuffer(), 0, (int)this.startBodyFragment.Length);
                }
                else
                {
                    OnWriteStartBody(writer);
                }

                OnWriteBodyContents(writer);

                if (this.endBodyFragment != null)
                {
                    ((IFragmentCapableXmlDictionaryWriter)writer).WriteFragment(this.endBodyFragment.GetBuffer(), 0, (int)this.endBodyFragment.Length);
                }
                else
                {
                    writer.WriteEndElement();
                }
            }

            writer.WriteEndElement();
        }

        private void AttachChannelBindingTokenIfFound()
        {
            ChannelBindingMessageProperty cbmp = null;
            ChannelBindingMessageProperty.TryGet(this.InnerMessage, out cbmp);

            if (cbmp != null)
            {
                if (this.securityHeader.ElementContainer != null && this.securityHeader.ElementContainer.EndorsingSupportingTokens != null)
                {
                    foreach (SecurityToken token in this.securityHeader.ElementContainer.EndorsingSupportingTokens)
                    {
                        ProviderBackedSecurityToken pbst = token as ProviderBackedSecurityToken;
                        if (pbst != null)
                        {
                            pbst.ChannelBinding = cbmp.ChannelBinding;
                        }
                    }
                }
            }
        }

        private void SetBodyId()
        {
            this.bodyId = this.InnerMessage.GetBodyAttribute(
                UtilityStrings.IdAttribute,
                this.securityHeader.StandardsManager.IdManager.DefaultIdNamespaceUri);
            if (this.bodyId == null)
            {
                this.bodyId = this.securityHeader.GenerateId();
                this.bodyIdInserted = true;
            }
        }

        public void WriteBodyToEncrypt(EncryptedData encryptedData, SymmetricAlgorithm algorithm)
        {
            encryptedData.Id = this.securityHeader.GenerateId();

            BodyContentHelper helper = new BodyContentHelper();
            XmlDictionaryWriter encryptingWriter = helper.CreateWriter();
            this.InnerMessage.WriteBodyContents(encryptingWriter);
            encryptedData.SetUpEncryption(algorithm, helper.ExtractResult());
            this.encryptedBodyContent = encryptedData;

            this.state = BodyState.Encrypted;
        }

        public void WriteBodyToEncryptThenSign(Stream canonicalStream, EncryptedData encryptedData, SymmetricAlgorithm algorithm)
        {
            encryptedData.Id = this.securityHeader.GenerateId();
            SetBodyId();

            XmlDictionaryWriter encryptingWriter = XmlDictionaryWriter.CreateTextWriter(Stream.Null);
            // The XmlSerializer body formatter would add a
            // document declaration to the body fragment when a fresh writer 
            // is provided. Hence, insert a dummy element here and capture 
            // the body contents as a fragment.
            encryptingWriter.WriteStartElement("a");
            MemoryStream ms = new MemoryStream();
            ((IFragmentCapableXmlDictionaryWriter)encryptingWriter).StartFragment(ms, true);

            this.InnerMessage.WriteBodyContents(encryptingWriter);
            ((IFragmentCapableXmlDictionaryWriter)encryptingWriter).EndFragment();
            encryptingWriter.WriteEndElement();
            ms.Flush();
            encryptedData.SetUpEncryption(algorithm, new ArraySegment<byte>(ms.GetBuffer(), 0, (int)ms.Length));

            this.fullBodyBuffer = new XmlBuffer(int.MaxValue);
            XmlDictionaryWriter canonicalWriter = this.fullBodyBuffer.OpenSection(XmlDictionaryReaderQuotas.Max);

            canonicalWriter.StartCanonicalization(canonicalStream, false, null);
            WriteStartInnerMessageWithId(canonicalWriter);
            encryptedData.WriteTo(canonicalWriter, ServiceModelDictionaryManager.Instance);
            canonicalWriter.WriteEndElement();
            canonicalWriter.EndCanonicalization();
            canonicalWriter.Flush();

            this.fullBodyBuffer.CloseSection();
            this.fullBodyBuffer.Close();

            this.state = BodyState.EncryptedThenSigned;
        }

        public void WriteBodyToSign(Stream canonicalStream)
        {
            SetBodyId();

            this.fullBodyBuffer = new XmlBuffer(int.MaxValue);
            XmlDictionaryWriter canonicalWriter = this.fullBodyBuffer.OpenSection(XmlDictionaryReaderQuotas.Max);
            canonicalWriter.StartCanonicalization(canonicalStream, false, null);
            WriteInnerMessageWithId(canonicalWriter);
            canonicalWriter.EndCanonicalization();
            canonicalWriter.Flush();
            this.fullBodyBuffer.CloseSection();
            this.fullBodyBuffer.Close();

            this.state = BodyState.Signed;
        }

        public void WriteBodyToSignThenEncrypt(Stream canonicalStream, EncryptedData encryptedData, SymmetricAlgorithm algorithm)
        {
            XmlBuffer buffer = new XmlBuffer(int.MaxValue);
            XmlDictionaryWriter fragmentingWriter = buffer.OpenSection(XmlDictionaryReaderQuotas.Max);
            WriteBodyToSignThenEncryptWithFragments(canonicalStream, false, null, encryptedData, algorithm, fragmentingWriter);
            ((IFragmentCapableXmlDictionaryWriter)fragmentingWriter).WriteFragment(this.startBodyFragment.GetBuffer(), 0, (int)this.startBodyFragment.Length);
            ((IFragmentCapableXmlDictionaryWriter)fragmentingWriter).WriteFragment(this.endBodyFragment.GetBuffer(), 0, (int)this.endBodyFragment.Length);
            buffer.CloseSection();
            buffer.Close();

            this.startBodyFragment = null;
            this.endBodyFragment = null;

            XmlDictionaryReader reader = buffer.GetReader(0);
            reader.MoveToContent();
            this.bodyPrefix = reader.Prefix;
            if (reader.HasAttributes)
            {
                this.bodyAttributes = XmlAttributeHolder.ReadAttributes(reader);
            }
            reader.Close();
        }

        public void WriteBodyToSignThenEncryptWithFragments(
            Stream stream, bool includeComments, string[] inclusivePrefixes,
            EncryptedData encryptedData, SymmetricAlgorithm algorithm, XmlDictionaryWriter writer)
        {
            IFragmentCapableXmlDictionaryWriter fragmentingWriter = (IFragmentCapableXmlDictionaryWriter)writer;

            SetBodyId();
            encryptedData.Id = this.securityHeader.GenerateId();

            this.startBodyFragment = new MemoryStream();
            BufferedOutputStream bodyContentFragment = new BufferManagerOutputStream(SR.XmlBufferQuotaExceeded, 1024, int.MaxValue, this.securityHeader.StreamBufferManager);
            this.endBodyFragment = new MemoryStream();

            writer.StartCanonicalization(stream, includeComments, inclusivePrefixes);

            fragmentingWriter.StartFragment(this.startBodyFragment, false);
            WriteStartInnerMessageWithId(writer);
            fragmentingWriter.EndFragment();

            fragmentingWriter.StartFragment(bodyContentFragment, true);
            this.InnerMessage.WriteBodyContents(writer);
            fragmentingWriter.EndFragment();

            fragmentingWriter.StartFragment(this.endBodyFragment, false);
            writer.WriteEndElement();
            fragmentingWriter.EndFragment();

            writer.EndCanonicalization();

            int bodyLength;
            byte[] bodyBuffer = bodyContentFragment.ToArray(out bodyLength);

            encryptedData.SetUpEncryption(algorithm, new ArraySegment<byte>(bodyBuffer, 0, bodyLength));
            this.encryptedBodyContent = encryptedData;

            this.state = BodyState.SignedThenEncrypted;
        }

        public void WriteBodyToSignWithFragments(Stream stream, bool includeComments, string[] inclusivePrefixes, XmlDictionaryWriter writer)
        {
            IFragmentCapableXmlDictionaryWriter fragmentingWriter = (IFragmentCapableXmlDictionaryWriter)writer;

            SetBodyId();
            BufferedOutputStream fullBodyFragment = new BufferManagerOutputStream(SR.XmlBufferQuotaExceeded, 1024, int.MaxValue, this.securityHeader.StreamBufferManager);
            writer.StartCanonicalization(stream, includeComments, inclusivePrefixes);
            fragmentingWriter.StartFragment(fullBodyFragment, false);
            WriteStartInnerMessageWithId(writer);
            this.InnerMessage.WriteBodyContents(writer);
            writer.WriteEndElement();
            fragmentingWriter.EndFragment();
            writer.EndCanonicalization();

            this.fullBodyFragment = fullBodyFragment.ToArray(out this.fullBodyFragmentLength);

            this.state = BodyState.Signed;
        }

        private void WriteInnerMessageWithId(XmlDictionaryWriter writer)
        {
            WriteStartInnerMessageWithId(writer);
            this.InnerMessage.WriteBodyContents(writer);
            writer.WriteEndElement();
        }

        private void WriteStartInnerMessageWithId(XmlDictionaryWriter writer)
        {
            this.InnerMessage.WriteStartBody(writer);
            if (this.bodyIdInserted)
            {
                this.securityHeader.StandardsManager.IdManager.WriteIdAttribute(writer, this.bodyId);
            }
        }

        private enum BodyState
        {
            Created,
            Signed,
            SignedThenEncrypted,
            EncryptedThenSigned,
            Encrypted,
            Disposed,
        }

        private struct BodyContentHelper
        {
            private MemoryStream stream;
            private XmlDictionaryWriter writer;

            public XmlDictionaryWriter CreateWriter()
            {
                this.stream = new MemoryStream();
                this.writer = XmlDictionaryWriter.CreateTextWriter(stream);
                return this.writer;
            }

            public ArraySegment<byte> ExtractResult()
            {
                this.writer.Flush();
                return new ArraySegment<byte>(this.stream.GetBuffer(), 0, (int)this.stream.Length);
            }
        }

        private sealed class MessagePrefixGenerator : IPrefixGenerator
        {
            private XmlWriter writer;

            public MessagePrefixGenerator(XmlWriter writer)
            {
                this.writer = writer;
            }

            public string GetPrefix(string namespaceUri, int depth, bool isForAttribute)
            {
                return this.writer.LookupPrefix(namespaceUri);
            }
        }
    }
}
