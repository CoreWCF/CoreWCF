// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    public class WSSecurityTokenSerializer : SecurityTokenSerializer
    {
        private const int DefaultMaximumKeyDerivationOffset = 64; // bytes 
        private const int DefaultMaximumKeyDerivationLabelLength = 128; // bytes
        private const int DefaultMaximumKeyDerivationNonceLength = 128; // bytes

        private static WSSecurityTokenSerializer s_instance;
        private readonly List<SerializerEntries> _serializerEntries;
        private readonly WSSecureConversation _secureConversation;
        private readonly List<TokenEntry> _tokenEntries = new List<TokenEntry>();
        private readonly KeyInfoSerializer _keyInfoSerializer;

        public WSSecurityTokenSerializer()
            : this(SecurityVersion.WSSecurity11)
        {
        }

        public WSSecurityTokenSerializer(bool emitBspRequiredAttributes)
            : this(SecurityVersion.WSSecurity11, emitBspRequiredAttributes)
        {
        }

        public WSSecurityTokenSerializer(SecurityVersion securityVersion)
            : this(securityVersion, false)
        {
        }

        public WSSecurityTokenSerializer(SecurityVersion securityVersion, bool emitBspRequiredAttributes)
            : this(securityVersion, emitBspRequiredAttributes, null)
        {
        }

        public WSSecurityTokenSerializer(SecurityVersion securityVersion, bool emitBspRequiredAttributes, SamlSerializer samlSerializer)
            : this(securityVersion, emitBspRequiredAttributes, samlSerializer, null, null)
        {
        }

        public WSSecurityTokenSerializer(SecurityVersion securityVersion, bool emitBspRequiredAttributes, SamlSerializer samlSerializer, SecurityStateEncoder securityStateEncoder, IEnumerable<Type> knownTypes)
            : this(securityVersion, emitBspRequiredAttributes, samlSerializer, securityStateEncoder, knownTypes, DefaultMaximumKeyDerivationOffset, DefaultMaximumKeyDerivationLabelLength, DefaultMaximumKeyDerivationNonceLength)
        {
        }

        public WSSecurityTokenSerializer(SecurityVersion securityVersion, TrustVersion trustVersion, SecureConversationVersion secureConversationVersion, bool emitBspRequiredAttributes, SamlSerializer samlSerializer, SecurityStateEncoder securityStateEncoder, IEnumerable<Type> knownTypes)
            : this(securityVersion, trustVersion, secureConversationVersion, emitBspRequiredAttributes, samlSerializer, securityStateEncoder, knownTypes, DefaultMaximumKeyDerivationOffset, DefaultMaximumKeyDerivationLabelLength, DefaultMaximumKeyDerivationNonceLength)
        {
        }

        public WSSecurityTokenSerializer(SecurityVersion securityVersion, bool emitBspRequiredAttributes, SamlSerializer samlSerializer, SecurityStateEncoder securityStateEncoder, IEnumerable<Type> knownTypes,
            int maximumKeyDerivationOffset, int maximumKeyDerivationLabelLength, int maximumKeyDerivationNonceLength)
            : this(securityVersion, TrustVersion.Default, SecureConversationVersion.Default, emitBspRequiredAttributes, samlSerializer, securityStateEncoder, knownTypes, maximumKeyDerivationOffset, maximumKeyDerivationLabelLength, maximumKeyDerivationNonceLength)
        {
        }

        public WSSecurityTokenSerializer(SecurityVersion securityVersion, TrustVersion trustVersion, SecureConversationVersion secureConversationVersion, bool emitBspRequiredAttributes, SamlSerializer samlSerializer, SecurityStateEncoder securityStateEncoder, IEnumerable<Type> knownTypes,
            int maximumKeyDerivationOffset, int maximumKeyDerivationLabelLength, int maximumKeyDerivationNonceLength)
        {
            if (maximumKeyDerivationOffset < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(maximumKeyDerivationOffset), SRCommon.ValueMustBeNonNegative));
            }
            if (maximumKeyDerivationLabelLength < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(maximumKeyDerivationLabelLength), SRCommon.ValueMustBeNonNegative));
            }
            if (maximumKeyDerivationNonceLength <= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(maximumKeyDerivationNonceLength), SR.ValueMustBeGreaterThanZero));
            }

            SecurityVersion = securityVersion ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(securityVersion)));
            EmitBspRequiredAttributes = emitBspRequiredAttributes;
            MaximumKeyDerivationOffset = maximumKeyDerivationOffset;
            MaximumKeyDerivationNonceLength = maximumKeyDerivationNonceLength;
            MaximumKeyDerivationLabelLength = maximumKeyDerivationLabelLength;

            _serializerEntries = new List<SerializerEntries>();

            if (secureConversationVersion == SecureConversationVersion.WSSecureConversationFeb2005)
            {
                _secureConversation = new WSSecureConversationFeb2005(this, securityStateEncoder, knownTypes, maximumKeyDerivationOffset, maximumKeyDerivationLabelLength, maximumKeyDerivationNonceLength);
            }
            else if (secureConversationVersion == SecureConversationVersion.WSSecureConversation13)
            {
                _secureConversation = new WSSecureConversationDec2005(this, securityStateEncoder, knownTypes, maximumKeyDerivationOffset, maximumKeyDerivationLabelLength, maximumKeyDerivationNonceLength);
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
            }

            if (securityVersion == SecurityVersion.WSSecurity10)
            {
                _serializerEntries.Add(new WSSecurityJan2004(this, samlSerializer));
            }
            else if (securityVersion == SecurityVersion.WSSecurity11)
            {
                _serializerEntries.Add(new WSSecurityXXX2005(this, samlSerializer));
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(securityVersion), SR.MessageSecurityVersionOutOfRange));
            }
            _serializerEntries.Add(_secureConversation);
            //TODO later
            TrustDictionary trustDictionary;
            if (trustVersion == TrustVersion.WSTrustFeb2005)
            {
                _serializerEntries.Add(new WSTrustFeb2005(this));
                trustDictionary = DXD.TrustDec2005Dictionary;
            }
            else if (trustVersion == TrustVersion.WSTrust13)
            {
                _serializerEntries.Add(new WSTrustDec2005(this));
                trustDictionary = DXD.TrustDec2005Dictionary;
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
            }

            _tokenEntries = new List<TokenEntry>();

            for (int i = 0; i < _serializerEntries.Count; ++i)
            {
                SerializerEntries serializerEntry = _serializerEntries[i];
                serializerEntry.PopulateTokenEntries(_tokenEntries);
            }

            DictionaryManager dictionaryManager = new DictionaryManager(ServiceModelDictionary.CurrentVersion)
            {
                SecureConversationDec2005Dictionary = DXD.SecureConversationDec2005Dictionary,
                SecurityAlgorithmDec2005Dictionary = DXD.SecurityAlgorithmDec2005Dictionary
            };

            _keyInfoSerializer = new WSKeyInfoSerializer(EmitBspRequiredAttributes, dictionaryManager, trustDictionary, this, securityVersion, secureConversationVersion);
        }

        public static WSSecurityTokenSerializer DefaultInstance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new WSSecurityTokenSerializer();
                }

                return s_instance;
            }
        }

        public bool EmitBspRequiredAttributes { get; }

        public SecurityVersion SecurityVersion { get; }

        public int MaximumKeyDerivationOffset { get; }

        public int MaximumKeyDerivationLabelLength { get; }

        public int MaximumKeyDerivationNonceLength { get; }

        private bool ShouldWrapException(Exception e)
        {
            if (Fx.IsFatal(e))
            {
                return false;
            }
            return ((e is ArgumentException) || (e is FormatException) || (e is InvalidOperationException));
        }

        protected override bool CanReadTokenCore(XmlReader reader)
        {
            XmlDictionaryReader localReader = XmlDictionaryReader.CreateDictionaryReader(reader);
            for (int i = 0; i < _tokenEntries.Count; i++)
            {
                TokenEntry tokenEntry = _tokenEntries[i];
                if (tokenEntry.CanReadTokenCore(localReader))
                {
                    return true;
                }
            }
            return false;
        }

        protected override SecurityToken ReadTokenCore(XmlReader reader, SecurityTokenResolver tokenResolver)
        {
            XmlDictionaryReader localReader = XmlDictionaryReader.CreateDictionaryReader(reader);
            for (int i = 0; i < _tokenEntries.Count; i++)
            {
                TokenEntry tokenEntry = _tokenEntries[i];
                if (tokenEntry.CanReadTokenCore(localReader))
                {
                    try
                    {
                        return tokenEntry.ReadTokenCore(localReader, tokenResolver);
                    }
                    catch (Exception e)
                    {
                        if (!ShouldWrapException(e))
                        {
                            throw;
                        }
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.ErrorDeserializingTokenXml, e));
                    }
                }
            }
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.CannotReadToken, reader.LocalName, reader.NamespaceURI, localReader.GetAttribute(XD.SecurityJan2004Dictionary.ValueType, null))));
        }

        protected override bool CanWriteTokenCore(SecurityToken token)
        {
            for (int i = 0; i < _tokenEntries.Count; i++)
            {
                TokenEntry tokenEntry = _tokenEntries[i];
                if (tokenEntry.SupportsCore(token.GetType()))
                {
                    return true;
                }
            }
            return false;
        }

        protected override void WriteTokenCore(XmlWriter writer, SecurityToken token)
        {
            bool wroteToken = false;
            XmlDictionaryWriter localWriter = XmlDictionaryWriter.CreateDictionaryWriter(writer);
            for (int i = 0; i < _tokenEntries.Count; i++)
            {
                TokenEntry tokenEntry = _tokenEntries[i];
                if (tokenEntry.SupportsCore(token.GetType()))
                {
                    try
                    {
                        tokenEntry.WriteTokenCore(localWriter, token);
                    }
                    catch (Exception e)
                    {
                        if (!ShouldWrapException(e))
                        {
                            throw;
                        }
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.ErrorSerializingSecurityToken), e));
                    }
                    wroteToken = true;
                    break;
                }
            }

            if (!wroteToken)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.StandardsManagerCannotWriteObject, token.GetType())));
            }

            localWriter.Flush();
        }

        protected override bool CanReadKeyIdentifierCore(XmlReader reader)
        {
            throw new PlatformNotSupportedException();
        }

        protected override SecurityKeyIdentifier ReadKeyIdentifierCore(XmlReader reader)
        {
            try
            {
                return _keyInfoSerializer.ReadKeyIdentifier(reader);
            }
            catch (Exception ex)
            {
                throw Fx.Exception.AsError(new MessageSecurityException(ex.Message));
            }
        }

        protected override bool CanWriteKeyIdentifierCore(SecurityKeyIdentifier keyIdentifier)
        {
            throw new PlatformNotSupportedException();
        }

        protected override void WriteKeyIdentifierCore(XmlWriter writer, SecurityKeyIdentifier keyIdentifier)
        {
            try
            {
                _keyInfoSerializer.WriteKeyIdentifier(writer, keyIdentifier);
            }
            catch (Exception ex)
            {
                throw Fx.Exception.AsError(new MessageSecurityException(ex.Message));
            }
        }

        protected override bool CanReadKeyIdentifierClauseCore(XmlReader reader)
        {
            try
            {
                return _keyInfoSerializer.CanReadKeyIdentifierClause(reader);
            }
            catch (Exception ex)
            {
                throw Fx.Exception.AsError(new MessageSecurityException(ex.Message));
            }
        }

        protected override SecurityKeyIdentifierClause ReadKeyIdentifierClauseCore(XmlReader reader)
        {
            try
            {
                return _keyInfoSerializer.ReadKeyIdentifierClause(reader);
            }
            catch (Exception ex)
            {
                throw Fx.Exception.AsError(new MessageSecurityException(ex.Message));
            }
        }

        protected override bool CanWriteKeyIdentifierClauseCore(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            throw new PlatformNotSupportedException();
        }

        protected override void WriteKeyIdentifierClauseCore(XmlWriter writer, SecurityKeyIdentifierClause keyIdentifierClause)
        {
            try
            {
                _keyInfoSerializer.WriteKeyIdentifierClause(writer, keyIdentifierClause);
            }
            catch (Exception ex)
            {
                throw Fx.Exception.AsError(new MessageSecurityException(ex.Message));
            }
        }

        internal Type[] GetTokenTypes(string tokenTypeUri)
        {
            if (tokenTypeUri != null)
            {
                for (int i = 0; i < _tokenEntries.Count; i++)
                {
                    TokenEntry tokenEntry = _tokenEntries[i];

                    if (tokenEntry.SupportsTokenTypeUri(tokenTypeUri))
                    {
                        return tokenEntry.GetTokenTypes();
                    }
                }
            }
            return null;
        }

        protected internal virtual string GetTokenTypeUri(Type tokenType)
        {
            if (tokenType != null)
            {
                for (int i = 0; i < _tokenEntries.Count; i++)
                {
                    TokenEntry tokenEntry = _tokenEntries[i];

                    if (tokenEntry.SupportsCore(tokenType))
                    {
                        return tokenEntry.TokenTypeUri;
                    }
                }
            }
            return null;
        }

        public virtual bool TryCreateKeyIdentifierClauseFromTokenXml(XmlElement element, SecurityTokenReferenceStyle tokenReferenceStyle, out SecurityKeyIdentifierClause securityKeyIdentifierClause)
        {
            if (element == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
            }

            securityKeyIdentifierClause = null;

            try
            {
                securityKeyIdentifierClause = CreateKeyIdentifierClauseFromTokenXml(element, tokenReferenceStyle);
            }
            catch (XmlException)
            {
                return false;
            }

            return true;
        }

        public virtual SecurityKeyIdentifierClause CreateKeyIdentifierClauseFromTokenXml(XmlElement element, SecurityTokenReferenceStyle tokenReferenceStyle)
        {
            if (element == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
            }

            for (int i = 0; i < _tokenEntries.Count; i++)
            {
                TokenEntry tokenEntry = _tokenEntries[i];
                if (tokenEntry.CanReadTokenCore(element))
                {
                    try
                    {
                        return tokenEntry.CreateKeyIdentifierClauseFromTokenXmlCore(element, tokenReferenceStyle);
                    }
                    catch (Exception e)
                    {
                        if (!ShouldWrapException(e))
                        {
                            throw;
                        }
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.ErrorDeserializingKeyIdentifierClauseFromTokenXml, e));
                    }
                }
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.CannotReadToken, element.LocalName, element.NamespaceURI, element.GetAttribute(SecurityJan2004Strings.ValueType, null))));
        }

        internal new abstract class TokenEntry
        {
            private Type[] _tokenTypes = null;
            protected abstract XmlDictionaryString LocalName { get; }
            protected abstract XmlDictionaryString NamespaceUri { get; }
            public Type TokenType => GetTokenTypes()[0];
            public abstract string TokenTypeUri { get; }
            protected abstract string ValueTypeUri { get; }

            protected abstract Type[] GetTokenTypesCore();

            public Type[] GetTokenTypes()
            {
                if (_tokenTypes == null)
                {
                    _tokenTypes = GetTokenTypesCore();
                }

                return _tokenTypes;
            }

            public bool SupportsCore(Type tokenType)
            {
                Type[] tokenTypes = GetTokenTypes();
                for (int i = 0; i < tokenTypes.Length; ++i)
                {
                    if (tokenTypes[i].IsAssignableFrom(tokenType))
                    {
                        return true;
                    }
                }
                return false;
            }

            public virtual bool SupportsTokenTypeUri(string tokenTypeUri)
            {
                return (TokenTypeUri == tokenTypeUri);
            }

            protected static SecurityKeyIdentifierClause CreateDirectReference(XmlElement issuedTokenXml, string idAttributeLocalName, string idAttributeNamespace, Type tokenType)
            {
                string id = issuedTokenXml.GetAttribute(idAttributeLocalName, idAttributeNamespace);
                if (id == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.RequiredAttributeMissing, idAttributeLocalName, issuedTokenXml.LocalName)));
                }
                return new LocalIdKeyIdentifierClause(id, tokenType);
            }

            public virtual bool CanReadTokenCore(XmlElement element)
            {
                string valueTypeUri = null;

                if (element.HasAttribute(SecurityJan2004Strings.ValueType, null))
                {
                    valueTypeUri = element.GetAttribute(SecurityJan2004Strings.ValueType, null);
                }

                return element.LocalName == LocalName.Value && element.NamespaceURI == NamespaceUri.Value && valueTypeUri == ValueTypeUri;
            }

            public virtual bool CanReadTokenCore(XmlDictionaryReader reader)
            {
                return reader.IsStartElement(LocalName, NamespaceUri) &&
                       reader.GetAttribute(XD.SecurityJan2004Dictionary.ValueType, null) == ValueTypeUri;
            }

            public virtual Task<SecurityToken> ReadTokenCoreAsync(XmlDictionaryReader reader, SecurityTokenResolver tokenResolver)
            {
                return Task.FromResult(ReadTokenCore(reader, tokenResolver));
            }

            public abstract SecurityKeyIdentifierClause CreateKeyIdentifierClauseFromTokenXmlCore(XmlElement issuedTokenXml, SecurityTokenReferenceStyle tokenReferenceStyle);

            public abstract SecurityToken ReadTokenCore(XmlDictionaryReader reader, SecurityTokenResolver tokenResolver);

            public abstract void WriteTokenCore(XmlDictionaryWriter writer, SecurityToken token);
        }

        internal new abstract class SerializerEntries
        {
            public virtual void PopulateTokenEntries(IList<TokenEntry> tokenEntries) { }
        }

        internal class CollectionDictionary : IXmlDictionary
        {
            private readonly List<XmlDictionaryString> _dictionaryStrings;

            public CollectionDictionary(List<XmlDictionaryString> dictionaryStrings)
            {
                _dictionaryStrings = dictionaryStrings ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(dictionaryStrings)));
            }

            public bool TryLookup(string value, out XmlDictionaryString result)
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
                }

                for (int i = 0; i < _dictionaryStrings.Count; ++i)
                {
                    if (_dictionaryStrings[i].Value.Equals(value))
                    {
                        result = _dictionaryStrings[i];
                        return true;
                    }
                }
                result = null;
                return false;
            }

            public bool TryLookup(int key, out XmlDictionaryString result)
            {
                for (int i = 0; i < _dictionaryStrings.Count; ++i)
                {
                    if (_dictionaryStrings[i].Key == key)
                    {
                        result = _dictionaryStrings[i];
                        return true;
                    }
                }
                result = null;
                return false;
            }

            public bool TryLookup(XmlDictionaryString value, out XmlDictionaryString result)
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
                }

                for (int i = 0; i < _dictionaryStrings.Count; ++i)
                {
                    if ((_dictionaryStrings[i].Key == value.Key) &&
                        (_dictionaryStrings[i].Value.Equals(value.Value)))
                    {
                        result = _dictionaryStrings[i];
                        return true;
                    }
                }
                result = null;
                return false;
            }
        }
    }
}
