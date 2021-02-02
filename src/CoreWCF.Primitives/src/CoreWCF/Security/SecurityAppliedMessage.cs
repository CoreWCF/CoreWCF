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
        private bool _bodyIdInserted;
        private string _bodyPrefix = MessageStrings.Prefix;
        private XmlBuffer _fullBodyBuffer;
        private ISecurityElement _encryptedBodyContent;
        private XmlAttributeHolder[] _bodyAttributes;
        private bool _delayedApplicationHandled;
        private BodyState _state = BodyState.Created;
        private readonly SendSecurityHeader _securityHeader;
        private MemoryStream _startBodyFragment;
        private MemoryStream _endBodyFragment;
        private byte[] _fullBodyFragment;
        private int _fullBodyFragmentLength;

        public SecurityAppliedMessage(Message messageToProcess, SendSecurityHeader securityHeader, bool signBody, bool encryptBody)
            : base(messageToProcess)
        {
            Fx.Assert(!(messageToProcess is SecurityAppliedMessage), "SecurityAppliedMessage should not be wrapped");
            _securityHeader = securityHeader;
            BodyProtectionMode = MessagePartProtectionModeHelper.GetProtectionMode(signBody, encryptBody, securityHeader.SignThenEncrypt);
        }

        public string BodyId { get; private set; }

        public MessagePartProtectionMode BodyProtectionMode { get; }

        internal byte[] PrimarySignatureValue => _securityHeader.PrimarySignatureValue;

        private Exception CreateBadStateException(string operation)
        {
            return new InvalidOperationException(SR.Format(SR.MessageBodyOperationNotValidInBodyState,
                operation, _state));
        }

        private void EnsureUniqueSecurityApplication()
        {
            if (_delayedApplicationHandled)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.DelayedSecurityApplicationAlreadyCompleted)));
            }
            _delayedApplicationHandled = true;
        }

        protected override void OnBodyToString(XmlDictionaryWriter writer)
        {
            if (_state == BodyState.Created || _fullBodyFragment != null)
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
                _fullBodyBuffer = null;
                _bodyAttributes = null;
                _encryptedBodyContent = null;
                _state = BodyState.Disposed;
            }
        }

        protected override void OnWriteStartBody(XmlDictionaryWriter writer)
        {
            if (_startBodyFragment != null || _fullBodyFragment != null)
            {
                WriteStartInnerMessageWithId(writer);
                return;
            }

            switch (_state)
            {
                case BodyState.Created:
                case BodyState.Encrypted:
                    InnerMessage.WriteStartBody(writer);
                    return;
                case BodyState.Signed:
                case BodyState.EncryptedThenSigned:
                    XmlDictionaryReader reader = _fullBodyBuffer.GetReader(0);
                    writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
                    writer.WriteAttributes(reader, false);
                    reader.Close();
                    return;
                case BodyState.SignedThenEncrypted:
                    writer.WriteStartElement(_bodyPrefix, XD.MessageDictionary.Body, Version.Envelope.DictionaryNamespace);
                    if (_bodyAttributes != null)
                    {
                        XmlAttributeHolder.WriteAttributes(_bodyAttributes, writer);
                    }
                    return;
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBadStateException(nameof(OnWriteStartBody)));
            }
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            switch (_state)
            {
                case BodyState.Created:
                    InnerMessage.WriteBodyContents(writer);
                    return;
                case BodyState.Signed:
                case BodyState.EncryptedThenSigned:
                    XmlDictionaryReader reader = _fullBodyBuffer.GetReader(0);
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
                    _encryptedBodyContent.WriteTo(writer, ServiceModelDictionaryManager.Instance);
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
            _securityHeader.StartSecurityApplication();

            Headers.Add(_securityHeader);

            InnerMessage.WriteStartEnvelope(writer);

            Headers.RemoveAt(Headers.Count - 1);

            _securityHeader.ApplyBodySecurity(writer, prefixGenerator);

            InnerMessage.WriteStartHeaders(writer);
            _securityHeader.ApplySecurityAndWriteHeaders(Headers, writer, prefixGenerator);

            _securityHeader.RemoveSignatureEncryptionIfAppropriate();

            _securityHeader.CompleteSecurityApplication();
            _securityHeader.WriteHeader(writer, Version);
            writer.WriteEndElement();

            if (_fullBodyFragment != null)
            {
                ((IFragmentCapableXmlDictionaryWriter)writer).WriteFragment(_fullBodyFragment, 0, _fullBodyFragmentLength);
            }
            else
            {
                if (_startBodyFragment != null)
                {
                    ((IFragmentCapableXmlDictionaryWriter)writer).WriteFragment(_startBodyFragment.GetBuffer(), 0, (int)_startBodyFragment.Length);
                }
                else
                {
                    OnWriteStartBody(writer);
                }

                OnWriteBodyContents(writer);

                if (_endBodyFragment != null)
                {
                    ((IFragmentCapableXmlDictionaryWriter)writer).WriteFragment(_endBodyFragment.GetBuffer(), 0, (int)_endBodyFragment.Length);
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
                if (_securityHeader.ElementContainer != null && _securityHeader.ElementContainer.EndorsingSupportingTokens != null)
                {
                    foreach (SecurityToken token in _securityHeader.ElementContainer.EndorsingSupportingTokens)
                    {
                        if (token is ProviderBackedSecurityToken pbst)
                        {
                            pbst.ChannelBinding = cbmp.ChannelBinding;
                        }
                    }
                }
            }
        }

        private void SetBodyId()
        {
            BodyId = InnerMessage.GetBodyAttribute(
                UtilityStrings.IdAttribute,
                _securityHeader.StandardsManager.IdManager.DefaultIdNamespaceUri);
            if (BodyId == null)
            {
                BodyId = _securityHeader.GenerateId();
                _bodyIdInserted = true;
            }
        }

        public void WriteBodyToEncrypt(EncryptedData encryptedData, SymmetricAlgorithm algorithm)
        {
            encryptedData.Id = _securityHeader.GenerateId();

            BodyContentHelper helper = new BodyContentHelper();
            XmlDictionaryWriter encryptingWriter = helper.CreateWriter();
            InnerMessage.WriteBodyContents(encryptingWriter);
            encryptedData.SetUpEncryption(algorithm, helper.ExtractResult());
            _encryptedBodyContent = encryptedData;

            _state = BodyState.Encrypted;
        }

        public void WriteBodyToEncryptThenSign(Stream canonicalStream, EncryptedData encryptedData, SymmetricAlgorithm algorithm)
        {
            encryptedData.Id = _securityHeader.GenerateId();
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

            _fullBodyBuffer = new XmlBuffer(int.MaxValue);
            XmlDictionaryWriter canonicalWriter = _fullBodyBuffer.OpenSection(XmlDictionaryReaderQuotas.Max);

            canonicalWriter.StartCanonicalization(canonicalStream, false, null);
            WriteStartInnerMessageWithId(canonicalWriter);
            encryptedData.WriteTo(canonicalWriter, ServiceModelDictionaryManager.Instance);
            canonicalWriter.WriteEndElement();
            canonicalWriter.EndCanonicalization();
            canonicalWriter.Flush();

            _fullBodyBuffer.CloseSection();
            _fullBodyBuffer.Close();

            _state = BodyState.EncryptedThenSigned;
        }

        public void WriteBodyToSign(Stream canonicalStream)
        {
            SetBodyId();

            _fullBodyBuffer = new XmlBuffer(int.MaxValue);
            XmlDictionaryWriter canonicalWriter = _fullBodyBuffer.OpenSection(XmlDictionaryReaderQuotas.Max);
            canonicalWriter.StartCanonicalization(canonicalStream, false, null);
            WriteInnerMessageWithId(canonicalWriter);
            canonicalWriter.EndCanonicalization();
            canonicalWriter.Flush();
            _fullBodyBuffer.CloseSection();
            _fullBodyBuffer.Close();

            _state = BodyState.Signed;
        }

        public void WriteBodyToSignThenEncrypt(Stream canonicalStream, EncryptedData encryptedData, SymmetricAlgorithm algorithm)
        {
            XmlBuffer buffer = new XmlBuffer(int.MaxValue);
            XmlDictionaryWriter fragmentingWriter = buffer.OpenSection(XmlDictionaryReaderQuotas.Max);
            WriteBodyToSignThenEncryptWithFragments(canonicalStream, false, null, encryptedData, algorithm, fragmentingWriter);
            ((IFragmentCapableXmlDictionaryWriter)fragmentingWriter).WriteFragment(_startBodyFragment.GetBuffer(), 0, (int)_startBodyFragment.Length);
            ((IFragmentCapableXmlDictionaryWriter)fragmentingWriter).WriteFragment(_endBodyFragment.GetBuffer(), 0, (int)_endBodyFragment.Length);
            buffer.CloseSection();
            buffer.Close();

            _startBodyFragment = null;
            _endBodyFragment = null;

            XmlDictionaryReader reader = buffer.GetReader(0);
            reader.MoveToContent();
            _bodyPrefix = reader.Prefix;
            if (reader.HasAttributes)
            {
                _bodyAttributes = XmlAttributeHolder.ReadAttributes(reader);
            }
            reader.Close();
        }

        public void WriteBodyToSignThenEncryptWithFragments(
            Stream stream, bool includeComments, string[] inclusivePrefixes,
            EncryptedData encryptedData, SymmetricAlgorithm algorithm, XmlDictionaryWriter writer)
        {
            IFragmentCapableXmlDictionaryWriter fragmentingWriter = (IFragmentCapableXmlDictionaryWriter)writer;

            SetBodyId();
            encryptedData.Id = _securityHeader.GenerateId();

            _startBodyFragment = new MemoryStream();
            BufferedOutputStream bodyContentFragment = new BufferManagerOutputStream(SR.XmlBufferQuotaExceeded, 1024, int.MaxValue, _securityHeader.StreamBufferManager);
            _endBodyFragment = new MemoryStream();

            writer.StartCanonicalization(stream, includeComments, inclusivePrefixes);

            fragmentingWriter.StartFragment(_startBodyFragment, false);
            WriteStartInnerMessageWithId(writer);
            fragmentingWriter.EndFragment();

            fragmentingWriter.StartFragment(bodyContentFragment, true);
            InnerMessage.WriteBodyContents(writer);
            fragmentingWriter.EndFragment();

            fragmentingWriter.StartFragment(_endBodyFragment, false);
            writer.WriteEndElement();
            fragmentingWriter.EndFragment();

            writer.EndCanonicalization();

            byte[] bodyBuffer = bodyContentFragment.ToArray(out int bodyLength);

            encryptedData.SetUpEncryption(algorithm, new ArraySegment<byte>(bodyBuffer, 0, bodyLength));
            _encryptedBodyContent = encryptedData;

            _state = BodyState.SignedThenEncrypted;
        }

        public void WriteBodyToSignWithFragments(Stream stream, bool includeComments, string[] inclusivePrefixes, XmlDictionaryWriter writer)
        {
            IFragmentCapableXmlDictionaryWriter fragmentingWriter = (IFragmentCapableXmlDictionaryWriter)writer;

            SetBodyId();
            BufferedOutputStream fullBodyFragment = new BufferManagerOutputStream(SR.XmlBufferQuotaExceeded, 1024, int.MaxValue, _securityHeader.StreamBufferManager);
            writer.StartCanonicalization(stream, includeComments, inclusivePrefixes);
            fragmentingWriter.StartFragment(fullBodyFragment, false);
            WriteStartInnerMessageWithId(writer);
            InnerMessage.WriteBodyContents(writer);
            writer.WriteEndElement();
            fragmentingWriter.EndFragment();
            writer.EndCanonicalization();

            _fullBodyFragment = fullBodyFragment.ToArray(out _fullBodyFragmentLength);

            _state = BodyState.Signed;
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
            if (_bodyIdInserted)
            {
                _securityHeader.StandardsManager.IdManager.WriteIdAttribute(writer, BodyId);
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
            private MemoryStream _stream;
            private XmlDictionaryWriter _writer;

            public XmlDictionaryWriter CreateWriter()
            {
                _stream = new MemoryStream();
                _writer = XmlDictionaryWriter.CreateTextWriter(_stream);
                return _writer;
            }

            public ArraySegment<byte> ExtractResult()
            {
                _writer.Flush();
                return new ArraySegment<byte>(_stream.GetBuffer(), 0, (int)_stream.Length);
            }
        }

        private sealed class MessagePrefixGenerator : IPrefixGenerator
        {
            private readonly XmlWriter _writer;

            public MessagePrefixGenerator(XmlWriter writer)
            {
                _writer = writer;
            }

            public string GetPrefix(string namespaceUri, int depth, bool isForAttribute)
            {
                return _writer.LookupPrefix(namespaceUri);
            }
        }
    }
}
