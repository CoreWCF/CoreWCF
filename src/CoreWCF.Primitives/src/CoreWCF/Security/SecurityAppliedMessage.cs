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
            bodyProtectionMode = MessagePartProtectionModeHelper.GetProtectionMode(signBody, encryptBody, securityHeader.SignThenEncrypt);
        }

        public string BodyId => bodyId;

        public MessagePartProtectionMode BodyProtectionMode => bodyProtectionMode;

        internal byte[] PrimarySignatureValue => securityHeader.PrimarySignatureValue;

        private Exception CreateBadStateException(string operation)
        {
            return new InvalidOperationException(SR.Format(SR.MessageBodyOperationNotValidInBodyState,
                operation, state));
        }

        private void EnsureUniqueSecurityApplication()
        {
            if (delayedApplicationHandled)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.DelayedSecurityApplicationAlreadyCompleted)));
            }
            delayedApplicationHandled = true;
        }

        protected override void OnBodyToString(XmlDictionaryWriter writer)
        {
            if (state == BodyState.Created || fullBodyFragment != null)
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
                InnerMessage.Close();
            }
            finally
            {
                fullBodyBuffer = null;
                bodyAttributes = null;
                encryptedBodyContent = null;
                state = BodyState.Disposed;
            }
        }

        protected override void OnWriteStartBody(XmlDictionaryWriter writer)
        {
            if (startBodyFragment != null || fullBodyFragment != null)
            {
                WriteStartInnerMessageWithId(writer);
                return;
            }

            switch (state)
            {
                case BodyState.Created:
                case BodyState.Encrypted:
                    InnerMessage.WriteStartBody(writer);
                    return;
                case BodyState.Signed:
                case BodyState.EncryptedThenSigned:
                    XmlDictionaryReader reader = fullBodyBuffer.GetReader(0);
                    writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
                    writer.WriteAttributes(reader, false);
                    reader.Close();
                    return;
                case BodyState.SignedThenEncrypted:
                    writer.WriteStartElement(bodyPrefix, XD.MessageDictionary.Body, Version.Envelope.DictionaryNamespace);
                    if (bodyAttributes != null)
                    {
                        XmlAttributeHolder.WriteAttributes(bodyAttributes, writer);
                    }
                    return;
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBadStateException(nameof(OnWriteStartBody)));
            }
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            switch (state)
            {
                case BodyState.Created:
                    InnerMessage.WriteBodyContents(writer);
                    return;
                case BodyState.Signed:
                case BodyState.EncryptedThenSigned:
                    XmlDictionaryReader reader = fullBodyBuffer.GetReader(0);
                    reader.ReadStartElement();
                    while (reader.NodeType != XmlNodeType.EndElement)
                    {
                        writer.WriteNode(reader, false);
                    }

                    reader.ReadEndElement();
                    reader.Close();
                    return;
                case BodyState.Encrypted:
                case BodyState.SignedThenEncrypted:
                    encryptedBodyContent.WriteTo(writer, ServiceModelDictionaryManager.Instance);
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
            securityHeader.StartSecurityApplication();

            Headers.Add(securityHeader);

            InnerMessage.WriteStartEnvelope(writer);

            Headers.RemoveAt(Headers.Count - 1);

            securityHeader.ApplyBodySecurity(writer, prefixGenerator);

            InnerMessage.WriteStartHeaders(writer);
            securityHeader.ApplySecurityAndWriteHeaders(Headers, writer, prefixGenerator);

            securityHeader.RemoveSignatureEncryptionIfAppropriate();

            securityHeader.CompleteSecurityApplication();
            securityHeader.WriteHeader(writer, Version);
            writer.WriteEndElement();

            if (fullBodyFragment != null)
            {
                ((IFragmentCapableXmlDictionaryWriter)writer).WriteFragment(fullBodyFragment, 0, fullBodyFragmentLength);
            }
            else
            {
                if (startBodyFragment != null)
                {
                    ((IFragmentCapableXmlDictionaryWriter)writer).WriteFragment(startBodyFragment.GetBuffer(), 0, (int)startBodyFragment.Length);
                }
                else
                {
                    OnWriteStartBody(writer);
                }

                OnWriteBodyContents(writer);

                if (endBodyFragment != null)
                {
                    ((IFragmentCapableXmlDictionaryWriter)writer).WriteFragment(endBodyFragment.GetBuffer(), 0, (int)endBodyFragment.Length);
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
            ChannelBindingMessageProperty.TryGet(InnerMessage, out ChannelBindingMessageProperty cbmp);

            if (cbmp != null)
            {
                if (securityHeader.ElementContainer != null && securityHeader.ElementContainer.EndorsingSupportingTokens != null)
                {
                    foreach (SecurityToken token in securityHeader.ElementContainer.EndorsingSupportingTokens)
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
            bodyId = InnerMessage.GetBodyAttribute(
                UtilityStrings.IdAttribute,
                securityHeader.StandardsManager.IdManager.DefaultIdNamespaceUri);
            if (bodyId == null)
            {
                bodyId = securityHeader.GenerateId();
                bodyIdInserted = true;
            }
        }

        public void WriteBodyToEncrypt(EncryptedData encryptedData, SymmetricAlgorithm algorithm)
        {
            encryptedData.Id = securityHeader.GenerateId();

            BodyContentHelper helper = new BodyContentHelper();
            XmlDictionaryWriter encryptingWriter = helper.CreateWriter();
            InnerMessage.WriteBodyContents(encryptingWriter);
            encryptedData.SetUpEncryption(algorithm, helper.ExtractResult());
            encryptedBodyContent = encryptedData;

            state = BodyState.Encrypted;
        }

        public void WriteBodyToEncryptThenSign(Stream canonicalStream, EncryptedData encryptedData, SymmetricAlgorithm algorithm)
        {
            encryptedData.Id = securityHeader.GenerateId();
            SetBodyId();

            XmlDictionaryWriter encryptingWriter = XmlDictionaryWriter.CreateTextWriter(Stream.Null);
            // The XmlSerializer body formatter would add a
            // document declaration to the body fragment when a fresh writer 
            // is provided. Hence, insert a dummy element here and capture 
            // the body contents as a fragment.
            encryptingWriter.WriteStartElement("a");
            MemoryStream ms = new MemoryStream();
            ((IFragmentCapableXmlDictionaryWriter)encryptingWriter).StartFragment(ms, true);

            InnerMessage.WriteBodyContents(encryptingWriter);
            ((IFragmentCapableXmlDictionaryWriter)encryptingWriter).EndFragment();
            encryptingWriter.WriteEndElement();
            ms.Flush();
            encryptedData.SetUpEncryption(algorithm, new ArraySegment<byte>(ms.GetBuffer(), 0, (int)ms.Length));

            fullBodyBuffer = new XmlBuffer(int.MaxValue);
            XmlDictionaryWriter canonicalWriter = fullBodyBuffer.OpenSection(XmlDictionaryReaderQuotas.Max);

            canonicalWriter.StartCanonicalization(canonicalStream, false, null);
            WriteStartInnerMessageWithId(canonicalWriter);
            encryptedData.WriteTo(canonicalWriter, ServiceModelDictionaryManager.Instance);
            canonicalWriter.WriteEndElement();
            canonicalWriter.EndCanonicalization();
            canonicalWriter.Flush();

            fullBodyBuffer.CloseSection();
            fullBodyBuffer.Close();

            state = BodyState.EncryptedThenSigned;
        }

        public void WriteBodyToSign(Stream canonicalStream)
        {
            SetBodyId();

            fullBodyBuffer = new XmlBuffer(int.MaxValue);
            XmlDictionaryWriter canonicalWriter = fullBodyBuffer.OpenSection(XmlDictionaryReaderQuotas.Max);
            canonicalWriter.StartCanonicalization(canonicalStream, false, null);
            WriteInnerMessageWithId(canonicalWriter);
            canonicalWriter.EndCanonicalization();
            canonicalWriter.Flush();
            fullBodyBuffer.CloseSection();
            fullBodyBuffer.Close();

            state = BodyState.Signed;
        }

        public void WriteBodyToSignThenEncrypt(Stream canonicalStream, EncryptedData encryptedData, SymmetricAlgorithm algorithm)
        {
            XmlBuffer buffer = new XmlBuffer(int.MaxValue);
            XmlDictionaryWriter fragmentingWriter = buffer.OpenSection(XmlDictionaryReaderQuotas.Max);
            WriteBodyToSignThenEncryptWithFragments(canonicalStream, false, null, encryptedData, algorithm, fragmentingWriter);
            ((IFragmentCapableXmlDictionaryWriter)fragmentingWriter).WriteFragment(startBodyFragment.GetBuffer(), 0, (int)startBodyFragment.Length);
            ((IFragmentCapableXmlDictionaryWriter)fragmentingWriter).WriteFragment(endBodyFragment.GetBuffer(), 0, (int)endBodyFragment.Length);
            buffer.CloseSection();
            buffer.Close();

            startBodyFragment = null;
            endBodyFragment = null;

            XmlDictionaryReader reader = buffer.GetReader(0);
            reader.MoveToContent();
            bodyPrefix = reader.Prefix;
            if (reader.HasAttributes)
            {
                bodyAttributes = XmlAttributeHolder.ReadAttributes(reader);
            }
            reader.Close();
        }

        public void WriteBodyToSignThenEncryptWithFragments(
            Stream stream, bool includeComments, string[] inclusivePrefixes,
            EncryptedData encryptedData, SymmetricAlgorithm algorithm, XmlDictionaryWriter writer)
        {
            IFragmentCapableXmlDictionaryWriter fragmentingWriter = (IFragmentCapableXmlDictionaryWriter)writer;

            SetBodyId();
            encryptedData.Id = securityHeader.GenerateId();

            startBodyFragment = new MemoryStream();
            BufferedOutputStream bodyContentFragment = new BufferManagerOutputStream(SR.XmlBufferQuotaExceeded, 1024, int.MaxValue, securityHeader.StreamBufferManager);
            endBodyFragment = new MemoryStream();

            writer.StartCanonicalization(stream, includeComments, inclusivePrefixes);

            fragmentingWriter.StartFragment(startBodyFragment, false);
            WriteStartInnerMessageWithId(writer);
            fragmentingWriter.EndFragment();

            fragmentingWriter.StartFragment(bodyContentFragment, true);
            InnerMessage.WriteBodyContents(writer);
            fragmentingWriter.EndFragment();

            fragmentingWriter.StartFragment(endBodyFragment, false);
            writer.WriteEndElement();
            fragmentingWriter.EndFragment();

            writer.EndCanonicalization();

            byte[] bodyBuffer = bodyContentFragment.ToArray(out int bodyLength);

            encryptedData.SetUpEncryption(algorithm, new ArraySegment<byte>(bodyBuffer, 0, bodyLength));
            encryptedBodyContent = encryptedData;

            state = BodyState.SignedThenEncrypted;
        }

        public void WriteBodyToSignWithFragments(Stream stream, bool includeComments, string[] inclusivePrefixes, XmlDictionaryWriter writer)
        {
            IFragmentCapableXmlDictionaryWriter fragmentingWriter = (IFragmentCapableXmlDictionaryWriter)writer;

            SetBodyId();
            BufferedOutputStream fullBodyFragment = new BufferManagerOutputStream(SR.XmlBufferQuotaExceeded, 1024, int.MaxValue, securityHeader.StreamBufferManager);
            writer.StartCanonicalization(stream, includeComments, inclusivePrefixes);
            fragmentingWriter.StartFragment(fullBodyFragment, false);
            WriteStartInnerMessageWithId(writer);
            InnerMessage.WriteBodyContents(writer);
            writer.WriteEndElement();
            fragmentingWriter.EndFragment();
            writer.EndCanonicalization();

            this.fullBodyFragment = fullBodyFragment.ToArray(out fullBodyFragmentLength);

            state = BodyState.Signed;
        }

        private void WriteInnerMessageWithId(XmlDictionaryWriter writer)
        {
            WriteStartInnerMessageWithId(writer);
            InnerMessage.WriteBodyContents(writer);
            writer.WriteEndElement();
        }

        private void WriteStartInnerMessageWithId(XmlDictionaryWriter writer)
        {
            InnerMessage.WriteStartBody(writer);
            if (bodyIdInserted)
            {
                securityHeader.StandardsManager.IdManager.WriteIdAttribute(writer, bodyId);
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
                stream = new MemoryStream();
                writer = XmlDictionaryWriter.CreateTextWriter(stream);
                return writer;
            }

            public ArraySegment<byte> ExtractResult()
            {
                writer.Flush();
                return new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Length);
            }
        }

        private sealed class MessagePrefixGenerator : IPrefixGenerator
        {
            private readonly XmlWriter writer;

            public MessagePrefixGenerator(XmlWriter writer)
            {
                this.writer = writer;
            }

            public string GetPrefix(string namespaceUri, int depth, bool isForAttribute)
            {
                return writer.LookupPrefix(namespaceUri);
            }
        }
    }
}
